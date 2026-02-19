using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Scans assemblies for Java peer types using System.Reflection.Metadata.
/// Two-phase architecture:
///   Phase 1: Build per-assembly indices (fast, O(1) lookups)
///   Phase 2: Analyze types using cached indices
/// </summary>
sealed class JavaPeerScanner : IDisposable
{
	readonly Dictionary<string, AssemblyIndex> assemblyCache = new (StringComparer.Ordinal);
	readonly Dictionary<(string typeName, string assemblyName), ActivationCtorInfo> activationCtorCache = new ();

	/// <summary>
	/// Resolves a type name + assembly name to a TypeDefinitionHandle + AssemblyIndex.
	/// Checks the specified assembly (by name) in the assembly cache.
	/// </summary>
	bool TryResolveType (string typeName, string assemblyName, out TypeDefinitionHandle handle, [NotNullWhen (true)] out AssemblyIndex? resolvedIndex)
	{
		if (assemblyCache.TryGetValue (assemblyName, out resolvedIndex) &&
		    resolvedIndex.TypesByFullName.TryGetValue (typeName, out handle)) {
			return true;
		}
		handle = default;
		resolvedIndex = null;
		return false;
	}

	/// <summary>
	/// Resolves a TypeReferenceHandle to (fullName, assemblyName), correctly handling
	/// nested types whose ResolutionScope is another TypeReference.
	/// </summary>
	static (string fullName, string assemblyName) ResolveTypeReference (TypeReferenceHandle handle, AssemblyIndex index)
	{
		var typeRef = index.Reader.GetTypeReference (handle);
		var name = index.Reader.GetString (typeRef.Name);
		var ns = index.Reader.GetString (typeRef.Namespace);

		var scope = typeRef.ResolutionScope;
		switch (scope.Kind) {
		case HandleKind.AssemblyReference: {
			var asmRef = index.Reader.GetAssemblyReference ((AssemblyReferenceHandle)scope);
			var fullName = MetadataTypeNameResolver.JoinNamespaceAndName (ns, name);
			return (fullName, index.Reader.GetString (asmRef.Name));
		}
		case HandleKind.TypeReference: {
			// Nested type: recurse to get the declaring type's full name and assembly
			var (parentFullName, assemblyName) = ResolveTypeReference ((TypeReferenceHandle)scope, index);
			return (MetadataTypeNameResolver.JoinNestedTypeName (parentFullName, name), assemblyName);
		}
		default: {
			var fullName = MetadataTypeNameResolver.JoinNamespaceAndName (ns, name);
			return (fullName, index.AssemblyName);
		}
		}
	}

	/// <summary>
	/// Looks up the [Register] JNI name for a type identified by name + assembly.
	/// </summary>
	string? ResolveRegisterJniName (string typeName, string assemblyName)
	{
		if (TryResolveType (typeName, assemblyName, out var handle, out var resolvedIndex) &&
		    resolvedIndex.RegisterInfoByType.TryGetValue (handle, out var regInfo)) {
			return regInfo.JniName;
		}
		return null;
	}

	/// <summary>
	/// Phase 1: Build indices for all assemblies.
	/// Phase 2: Scan all types and produce JavaPeerInfo records.
	/// </summary>
	public List<JavaPeerInfo> Scan (IReadOnlyList<string> assemblyPaths)
	{
		// Phase 1: Build indices for all assemblies
		foreach (var path in assemblyPaths) {
			var index = AssemblyIndex.Create (path);
			assemblyCache [index.AssemblyName] = index;
		}

		// Phase 2: Analyze types using cached indices
		var resultsByManagedName = new Dictionary<string, JavaPeerInfo> (StringComparer.Ordinal);

		foreach (var index in assemblyCache.Values) {
			ScanAssembly (index, resultsByManagedName);
		}

		// Phase 3: Force unconditional on types referenced by [Application] attributes
		ForceUnconditionalCrossReferences (resultsByManagedName, assemblyCache);

		return new List<JavaPeerInfo> (resultsByManagedName.Values);
	}

	/// <summary>
	/// Types referenced by [Application(BackupAgent = typeof(X))] or
	/// [Application(ManageSpaceActivity = typeof(X))] must be unconditional,
	/// because the manifest will reference them even if nothing else does.
	/// </summary>
	static void ForceUnconditionalCrossReferences (Dictionary<string, JavaPeerInfo> resultsByManagedName, Dictionary<string, AssemblyIndex> assemblyCache)
	{
		foreach (var index in assemblyCache.Values) {
			foreach (var attrInfo in index.AttributesByType.Values) {
				if (attrInfo is ApplicationAttributeInfo applicationAttributeInfo) {
					ForceUnconditionalIfPresent (resultsByManagedName, applicationAttributeInfo.BackupAgent);
					ForceUnconditionalIfPresent (resultsByManagedName, applicationAttributeInfo.ManageSpaceActivity);
				}
			}
		}
	}

	static void ForceUnconditionalIfPresent (Dictionary<string, JavaPeerInfo> resultsByManagedName, string? managedTypeName)
	{
		if (managedTypeName is null) {
			return;
		}

		managedTypeName = managedTypeName.Trim ();
		if (managedTypeName.Length == 0) {
			return;
		}

		// Try exact match first (handles both plain and assembly-qualified names)
		if (resultsByManagedName.TryGetValue (managedTypeName, out var peer)) {
			resultsByManagedName [managedTypeName] = peer with { IsUnconditional = true };
			return;
		}

		// TryGetTypeProperty may return assembly-qualified names like "Ns.Type, Assembly, ..."
		// Strip to just the type name for lookup
		var commaIndex = managedTypeName.IndexOf (',');
		if (commaIndex <= 0) {
			return;
		}

		var typeName = managedTypeName.Substring (0, commaIndex).Trim ();
		if (typeName.Length > 0 && resultsByManagedName.TryGetValue (typeName, out peer)) {
			resultsByManagedName [typeName] = peer with { IsUnconditional = true };
		}
	}

	void ScanAssembly (AssemblyIndex index, Dictionary<string, JavaPeerInfo> results)
	{
		foreach (var typeHandle in index.Reader.TypeDefinitions) {
			var typeDef = index.Reader.GetTypeDefinition (typeHandle);

			// Skip module-level types
			if (index.Reader.GetString (typeDef.Name) == "<Module>") {
				continue;
			}

			// Determine the JNI name and whether this is a known Java peer.
			// Priority:
			//   1. [Register] attribute → use JNI name from attribute
			//   2. Component attribute Name property → convert dots to slashes
			//   3. Extends a known Java peer → auto-compute JNI name via CRC64
			//   4. None of the above → not a Java peer, skip
			string? jniName = null;
			string? compatJniName = null;
			bool doNotGenerateAcw = false;

			index.RegisterInfoByType.TryGetValue (typeHandle, out var registerInfo);
			index.AttributesByType.TryGetValue (typeHandle, out var attrInfo);

			if (registerInfo is not null && !string.IsNullOrEmpty (registerInfo.JniName)) {
				jniName = registerInfo.JniName;
				compatJniName = jniName;
				doNotGenerateAcw = registerInfo.DoNotGenerateAcw;
			} else if (attrInfo?.JniName is not null) {
				// User type with [Activity(Name = "...")] but no [Register]
				jniName = attrInfo.JniName;
				compatJniName = jniName;
			} else {
				// No explicit JNI name — check if this type extends a known Java peer.
				// If so, auto-compute JNI name from the managed type name via CRC64.
				if (ExtendsJavaPeer (typeDef, index)) {
					(jniName, compatJniName) = ComputeAutoJniNames (typeDef, index);
				} else {
					continue;
				}
			}

			var fullName = MetadataTypeNameResolver.GetFullName (typeDef, index.Reader);

			var isInterface = (typeDef.Attributes & TypeAttributes.Interface) != 0;
			var isAbstract = (typeDef.Attributes & TypeAttributes.Abstract) != 0;
			var isGenericDefinition = typeDef.GetGenericParameters ().Count > 0;

			var isUnconditional = attrInfo is not null;
			string? invokerTypeName = null;

			// Collect marshal methods (including constructors) in a single pass over methods
			var marshalMethods = CollectMarshalMethods (typeDef, index);

			// Resolve activation constructor
			var activationCtor = ResolveActivationCtor (fullName, typeDef, index);

			// For interfaces/abstract types, try to find invoker type name
			if (isInterface || isAbstract) {
				invokerTypeName = TryFindInvokerTypeName (fullName, typeHandle, index);
			}

			var peer = new JavaPeerInfo {
				JavaName = jniName,
				CompatJniName = compatJniName,
				ManagedTypeName = fullName,
				ManagedTypeNamespace = ExtractNamespace (fullName),
				ManagedTypeShortName = ExtractShortName (fullName),
				AssemblyName = index.AssemblyName,
				IsInterface = isInterface,
				IsAbstract = isAbstract,
				DoNotGenerateAcw = doNotGenerateAcw,
				IsUnconditional = isUnconditional,
				MarshalMethods = marshalMethods,
				ActivationCtor = activationCtor,
				InvokerTypeName = invokerTypeName,
				IsGenericDefinition = isGenericDefinition,
			};

			results [fullName] = peer;
		}
	}

	List<MarshalMethodInfo> CollectMarshalMethods (TypeDefinition typeDef, AssemblyIndex index)
	{
		var methods = new List<MarshalMethodInfo> ();

		// Single pass over methods: collect marshal methods (including constructors)
		foreach (var methodHandle in typeDef.GetMethods ()) {
			var methodDef = index.Reader.GetMethodDefinition (methodHandle);
			if (!TryGetMethodRegisterInfo (methodDef, index, out var registerInfo, out var exportInfo) || registerInfo is null) {
				continue;
			}

			AddMarshalMethod (methods, registerInfo, methodDef, index, exportInfo);
		}

		// Collect [Register] from properties (attribute is on the property, not the getter)
		foreach (var propHandle in typeDef.GetProperties ()) {
			var propDef = index.Reader.GetPropertyDefinition (propHandle);
			var propRegister = TryGetPropertyRegisterInfo (propDef, index);
			if (propRegister is null) {
				continue;
			}

			var accessors = propDef.GetAccessors ();
			if (!accessors.Getter.IsNil) {
				var getterDef = index.Reader.GetMethodDefinition (accessors.Getter);
				AddMarshalMethod (methods, propRegister, getterDef, index);
			}
		}

		return methods;
	}

	static void AddMarshalMethod (List<MarshalMethodInfo> methods, RegisterInfo registerInfo, MethodDefinition methodDef, AssemblyIndex index, ExportInfo? exportInfo = null)
	{
		// Skip methods that are just the JNI name (type-level [Register])
		if (registerInfo.Signature is null && registerInfo.Connector is null) {
			return;
		}

		methods.Add (new MarshalMethodInfo {
			JniName = registerInfo.JniName,
			JniSignature = registerInfo.Signature ?? "()V",
			Connector = registerInfo.Connector,
			ManagedMethodName = index.Reader.GetString (methodDef.Name),
			NativeCallbackName = $"n_{index.Reader.GetString (methodDef.Name)}",
			JniReturnType = JniSignatureHelper.ParseReturnTypeString (registerInfo.Signature ?? "()V"),
			Parameters = ParseJniParameters (registerInfo.Signature ?? "()V"),
			IsConstructor = registerInfo.JniName == "<init>" || registerInfo.JniName == ".ctor",
			ThrownNames = exportInfo?.ThrownNames,
			SuperArgumentsString = exportInfo?.SuperArgumentsString,
		});
	}

	static bool TryGetMethodRegisterInfo (MethodDefinition methodDef, AssemblyIndex index, out RegisterInfo? registerInfo, out ExportInfo? exportInfo)
	{
		exportInfo = null;
		foreach (var caHandle in methodDef.GetCustomAttributes ()) {
			var ca = index.Reader.GetCustomAttribute (caHandle);
			var attrName = AssemblyIndex.GetCustomAttributeName (ca, index.Reader);

			if (attrName == "RegisterAttribute") {
				registerInfo = index.ParseRegisterAttribute (ca);
				return true;
			}

			if (attrName == "ExportAttribute") {
				(registerInfo, exportInfo) = ParseExportAttribute (ca, methodDef, index);
				return true;
			}
		}
		registerInfo = null;
		return false;
	}

	static RegisterInfo? TryGetPropertyRegisterInfo (PropertyDefinition propDef, AssemblyIndex index)
	{
		foreach (var caHandle in propDef.GetCustomAttributes ()) {
			var ca = index.Reader.GetCustomAttribute (caHandle);
			var attrName = AssemblyIndex.GetCustomAttributeName (ca, index.Reader);

			if (attrName == "RegisterAttribute") {
				return index.ParseRegisterAttribute (ca);
			}
		}
		return null;
	}

	static (RegisterInfo registerInfo, ExportInfo exportInfo) ParseExportAttribute (CustomAttribute ca, MethodDefinition methodDef, AssemblyIndex index)
	{
		var value = index.DecodeAttribute (ca);

		// [Export("name")] or [Export] (uses method name)
		string? exportName = null;
		if (value.FixedArguments.Length > 0) {
			exportName = (string?)value.FixedArguments [0].Value;
		}

		List<string>? thrownNames = null;
		string? superArguments = null;

		// Check Named arguments
		foreach (var named in value.NamedArguments) {
			if (named.Name == "Name" && named.Value is string name) {
				exportName = name;
			} else if (named.Name == "ThrownNames" && named.Value is ImmutableArray<CustomAttributeTypedArgument<string>> names) {
				thrownNames = new List<string> (names.Length);
				foreach (var item in names) {
					if (item.Value is string s) {
						thrownNames.Add (s);
					}
				}
			} else if (named.Name == "SuperArgumentsString" && named.Value is string superArgs) {
				superArguments = superArgs;
			}
		}

		if (string.IsNullOrEmpty (exportName)) {
			exportName = index.Reader.GetString (methodDef.Name);
		}
		string resolvedExportName = exportName ?? throw new InvalidOperationException ("Export name should not be null at this point.");

		// Build JNI signature from method signature
		var sig = methodDef.DecodeSignature (SignatureTypeProvider.Instance, genericContext: default);
		var jniSig = BuildJniSignatureFromManaged (sig);

		return (
			new RegisterInfo { JniName = resolvedExportName, Signature = jniSig, Connector = null, DoNotGenerateAcw = false },
			new ExportInfo { ThrownNames = thrownNames, SuperArgumentsString = superArguments }
		);
	}

	static string BuildJniSignatureFromManaged (MethodSignature<string> sig)
	{
		var sb = new System.Text.StringBuilder ();
		sb.Append ('(');
		foreach (var param in sig.ParameterTypes) {
			sb.Append (ManagedTypeToJniDescriptor (param));
		}
		sb.Append (')');
		sb.Append (ManagedTypeToJniDescriptor (sig.ReturnType));
		return sb.ToString ();
	}

	static string ManagedTypeToJniDescriptor (string managedType)
	{
		switch (managedType) {
		case "System.Void": return "V";
		case "System.Boolean": return "Z";
		case "System.Byte":
		case "System.SByte": return "B";
		case "System.Char": return "C";
		case "System.Int16":
		case "System.UInt16": return "S";
		case "System.Int32":
		case "System.UInt32": return "I";
		case "System.Int64":
		case "System.UInt64": return "J";
		case "System.Single": return "F";
		case "System.Double": return "D";
		case "System.String": return "Ljava/lang/String;";
		default:
			if (managedType.EndsWith ("[]")) {
				return $"[{ManagedTypeToJniDescriptor (managedType.Substring (0, managedType.Length - 2))}";
			}
			return "Ljava/lang/Object;";
		}
	}

	ActivationCtorInfo? ResolveActivationCtor (string typeName, TypeDefinition typeDef, AssemblyIndex index)
	{
		var cacheKey = (typeName, index.AssemblyName);
		if (activationCtorCache.TryGetValue (cacheKey, out var cached)) {
			return cached;
		}

		// Check this type's constructors
		var ownCtor = FindActivationCtorOnType (typeDef, index);
		if (ownCtor is not null) {
			var info = new ActivationCtorInfo { DeclaringTypeName = typeName, DeclaringAssemblyName = index.AssemblyName, Style = ownCtor.Value };
			activationCtorCache [cacheKey] = info;
			return info;
		}

		// Walk base type hierarchy
		var baseInfo = GetBaseTypeInfo (typeDef, index);
		if (baseInfo is not null) {
			var (baseTypeName, baseAssemblyName) = baseInfo.Value;
			if (TryResolveType (baseTypeName, baseAssemblyName, out var baseHandle, out var baseIndex)) {
				var baseTypeDef = baseIndex.Reader.GetTypeDefinition (baseHandle);
				var result = ResolveActivationCtor (baseTypeName, baseTypeDef, baseIndex);
				if (result is not null) {
					activationCtorCache [cacheKey] = result;
				}
				return result;
			}
		}

		return null;
	}

	static ActivationCtorStyle? FindActivationCtorOnType (TypeDefinition typeDef, AssemblyIndex index)
	{
		foreach (var methodHandle in typeDef.GetMethods ()) {
			var method = index.Reader.GetMethodDefinition (methodHandle);
			var name = index.Reader.GetString (method.Name);

			if (name != ".ctor") {
				continue;
			}

			var sig = method.DecodeSignature (SignatureTypeProvider.Instance, genericContext: default);

			// XI style: (IntPtr, JniHandleOwnership)
			if (sig.ParameterTypes.Length == 2 &&
			    sig.ParameterTypes [0] == "System.IntPtr" &&
			    sig.ParameterTypes [1] == "Android.Runtime.JniHandleOwnership") {
				return ActivationCtorStyle.XamarinAndroid;
			}

			// JI style: (ref JniObjectReference, JniObjectReferenceOptions)
			if (sig.ParameterTypes.Length == 2 &&
			    (sig.ParameterTypes [0] == "Java.Interop.JniObjectReference&" || sig.ParameterTypes [0] == "Java.Interop.JniObjectReference") &&
			    sig.ParameterTypes [1] == "Java.Interop.JniObjectReferenceOptions") {
				return ActivationCtorStyle.JavaInterop;
			}
		}

		return null;
	}

	/// <summary>
	/// Resolves a TypeSpecificationHandle (generic instantiation) to the underlying
	/// type's (fullName, assemblyName) by reading the raw signature blob.
	/// </summary>
	static (string fullName, string assemblyName)? ResolveTypeSpecification (TypeSpecificationHandle specHandle, AssemblyIndex index)
	{
		var typeSpec = index.Reader.GetTypeSpecification (specHandle);
		var blobReader = index.Reader.GetBlobReader (typeSpec.Signature);

		// Generic instantiation blob: GENERICINST (CLASS|VALUETYPE) coded-token count args...
		var elementType = blobReader.ReadByte ();
		if (elementType != 0x15) { // ELEMENT_TYPE_GENERICINST
			return null;
		}

		var classOrValueType = blobReader.ReadByte ();
		if (classOrValueType != 0x12 && classOrValueType != 0x11) { // CLASS or VALUETYPE
			return null;
		}

		// TypeDefOrRefOrSpec coded index: 2 tag bits (0=TypeDef, 1=TypeRef, 2=TypeSpec)
		var codedToken = blobReader.ReadCompressedInteger ();
		var tag = codedToken & 0x3;
		var row = codedToken >> 2;

		switch (tag) {
		case 0: { // TypeDef
			var handle = MetadataTokens.TypeDefinitionHandle (row);
			var baseDef = index.Reader.GetTypeDefinition (handle);
			return (MetadataTypeNameResolver.GetFullName (baseDef, index.Reader), index.AssemblyName);
		}
		case 1: // TypeRef
			return ResolveTypeReference (MetadataTokens.TypeReferenceHandle (row), index);
		default:
			return null;
		}
	}

	/// <summary>
	/// Resolves an EntityHandle (TypeDef, TypeRef, or TypeSpec) to (typeName, assemblyName).
	/// Shared by base type resolution, interface resolution, and any handle-to-name lookup.
	/// </summary>
	(string typeName, string assemblyName)? ResolveEntityHandle (EntityHandle handle, AssemblyIndex index)
	{
		switch (handle.Kind) {
		case HandleKind.TypeDefinition: {
			var td = index.Reader.GetTypeDefinition ((TypeDefinitionHandle)handle);
			return (MetadataTypeNameResolver.GetFullName (td, index.Reader), index.AssemblyName);
		}
		case HandleKind.TypeReference:
			return ResolveTypeReference ((TypeReferenceHandle)handle, index);
		case HandleKind.TypeSpecification:
			return ResolveTypeSpecification ((TypeSpecificationHandle)handle, index);
		default:
			return null;
		}
	}

	(string typeName, string assemblyName)? GetBaseTypeInfo (TypeDefinition typeDef, AssemblyIndex index)
	{
		return typeDef.BaseType.IsNil ? null : ResolveEntityHandle (typeDef.BaseType, index);
	}

	string? TryFindInvokerTypeName (string typeName, TypeDefinitionHandle typeHandle, AssemblyIndex index)
	{
		// First, check the [Register] attribute's connector arg (3rd arg).
		// In real Mono.Android, interfaces have [Register("jni/name", "", "InvokerTypeName, Assembly")]
		// where the connector contains the assembly-qualified invoker type name.
		if (index.RegisterInfoByType.TryGetValue (typeHandle, out var registerInfo) && registerInfo.Connector is not null) {
			var connector = registerInfo.Connector;
			// The connector may be "TypeName" or "TypeName, Assembly, Version=..., Culture=..., PublicKeyToken=..."
			// We want just the type name (before the first comma, if any)
			var commaIndex = connector.IndexOf (',');
			if (commaIndex > 0) {
				return connector.Substring (0, commaIndex).Trim ();
			}
			if (connector.Length > 0) {
				return connector;
			}
		}

		// Fallback: convention-based lookup — invoker type is TypeName + "Invoker"
		var invokerName = $"{typeName}Invoker";
		if (index.TypesByFullName.ContainsKey (invokerName)) {
			return invokerName;
		}
		return null;
	}

	public void Dispose ()
	{
		foreach (var index in assemblyCache.Values) {
			index.Dispose ();
		}
		assemblyCache.Clear ();
	}

	readonly Dictionary<string, bool> extendsJavaPeerCache = new (StringComparer.Ordinal);

	/// <summary>
	/// Check if a type extends a known Java peer (has [Register] or component attribute)
	/// by walking the base type chain. Results are cached; false-before-recurse prevents cycles.
	/// </summary>
	bool ExtendsJavaPeer (TypeDefinition typeDef, AssemblyIndex index)
	{
		var fullName = MetadataTypeNameResolver.GetFullName (typeDef, index.Reader);
		var key = $"{index.AssemblyName}:{fullName}";

		if (extendsJavaPeerCache.TryGetValue (key, out var cached)) {
			return cached;
		}

		// Mark as false to prevent cycles, then compute
		extendsJavaPeerCache [key] = false;

		var baseInfo = GetBaseTypeInfo (typeDef, index);
		if (baseInfo is null) {
			return false;
		}

		var (baseTypeName, baseAssemblyName) = baseInfo.Value;

		if (!TryResolveType (baseTypeName, baseAssemblyName, out var baseHandle, out var baseIndex)) {
			return false;
		}

		// Direct hit: base has [Register] or component attribute
		if (baseIndex.RegisterInfoByType.ContainsKey (baseHandle)) {
			extendsJavaPeerCache [key] = true;
			return true;
		}
		if (baseIndex.AttributesByType.ContainsKey (baseHandle)) {
			extendsJavaPeerCache [key] = true;
			return true;
		}

		// Recurse up the hierarchy
		var baseDef = baseIndex.Reader.GetTypeDefinition (baseHandle);
		var result = ExtendsJavaPeer (baseDef, baseIndex);
		extendsJavaPeerCache [key] = result;
		return result;
	}

	/// <summary>
	/// Compute both JNI name and compat JNI name for a type without [Register] or component Name.
	/// JNI name uses CRC64 hash of "namespace:assemblyName" for the package.
	/// Compat JNI name uses the raw managed namespace (lowercased).
	/// If a declaring type has [Register], its JNI name is used as prefix for both.
	/// Generic backticks are replaced with _.
	/// </summary>
	static (string jniName, string compatJniName) ComputeAutoJniNames (TypeDefinition typeDef, AssemblyIndex index)
	{
		var (typeName, parentJniName, ns) = ComputeTypeNameParts (typeDef, index);

		if (parentJniName is not null) {
			var name = $"{parentJniName}_{typeName}";
			return (name, name);
		}

		var packageName = GetCrc64PackageName (ns, index.AssemblyName);
		var jniName = $"{packageName}/{typeName}";

		string compatName = ns.Length == 0
			? typeName
			: $"{ns.ToLowerInvariant ().Replace ('.', '/')}/{typeName}";

		return (jniName, compatName);
	}

	/// <summary>
	/// Builds the type name part (handling nesting) and returns either a parent's
	/// registered JNI name or the outermost namespace.
	/// Matches JavaNativeTypeManager.ToJniName behavior: walks up declaring types
	/// and if a parent has [Register] or a component attribute JNI name, uses that
	/// as prefix instead of computing CRC64 from the namespace.
	/// </summary>
	static (string typeName, string? parentJniName, string ns) ComputeTypeNameParts (TypeDefinition typeDef, AssemblyIndex index)
	{
		var firstName = index.Reader.GetString (typeDef.Name).Replace ('`', '_');

		// Fast path: non-nested types (the vast majority)
		if (!typeDef.IsNested) {
			return (firstName, null, index.Reader.GetString (typeDef.Namespace));
		}

		// Nested type: walk up declaring types, collecting name parts
		var nameParts = new List<string> (4) { firstName };
		var current = typeDef;
		string? parentJniName = null;

		do {
			var parentHandle = current.GetDeclaringType ();
			current = index.Reader.GetTypeDefinition (parentHandle);

			// Check if the parent has a registered JNI name
			if (index.RegisterInfoByType.TryGetValue (parentHandle, out var parentRegister) && !string.IsNullOrEmpty (parentRegister.JniName)) {
				parentJniName = parentRegister.JniName;
				break;
			}
			if (index.AttributesByType.TryGetValue (parentHandle, out var parentAttr) && parentAttr.JniName is not null) {
				parentJniName = parentAttr.JniName;
				break;
			}

			nameParts.Add (index.Reader.GetString (current.Name).Replace ('`', '_'));
		} while (current.IsNested);

		nameParts.Reverse ();
		var typeName = string.Join ("_", nameParts);
		var ns = index.Reader.GetString (current.Namespace);

		return (typeName, parentJniName, ns);
	}

	static string GetCrc64PackageName (string ns, string assemblyName)
	{
		// Only Mono.Android preserves the namespace directly
		if (assemblyName == "Mono.Android") {
			return ns.ToLowerInvariant ().Replace ('.', '/');
		}

		var data = System.Text.Encoding.UTF8.GetBytes ($"{ns}:{assemblyName}");
		var hash = System.IO.Hashing.Crc64.Hash (data);
		return $"crc64{BitConverter.ToString (hash).Replace ("-", "").ToLowerInvariant ()}";
	}

	static string ExtractNamespace (string fullName)
	{
		int lastDot = fullName.LastIndexOf ('.');
		return lastDot >= 0 ? fullName.Substring (0, lastDot) : "";
	}

	static string ExtractShortName (string fullName)
	{
		int lastDot = fullName.LastIndexOf ('.');
		string typePart = lastDot >= 0 ? fullName.Substring (lastDot + 1) : fullName;
		int lastPlus = typePart.LastIndexOf ('+');
		return lastPlus >= 0 ? typePart.Substring (lastPlus + 1) : typePart;
	}

	static List<JniParameterInfo> ParseJniParameters (string jniSignature)
	{
		var typeStrings = JniSignatureHelper.ParseParameterTypeStrings (jniSignature);
		var result = new List<JniParameterInfo> (typeStrings.Count);
		foreach (var t in typeStrings) {
			result.Add (new JniParameterInfo { JniType = t });
		}
		return result;
	}
}
