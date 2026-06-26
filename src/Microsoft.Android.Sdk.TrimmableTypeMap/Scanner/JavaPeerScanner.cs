using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Scans assemblies for Java peer types using System.Reflection.Metadata.
/// Two-phase architecture:
///   Phase 1: Build per-assembly indices (fast, O(1) lookups)
///   Phase 2: Analyze types using cached indices
/// </summary>
public sealed class JavaPeerScanner : IDisposable
{
	enum HashedPackageNamingPolicy {
		Crc64,
		LowercaseCrc64,
	}

	readonly Dictionary<string, AssemblyIndex> assemblyCache = new (StringComparer.Ordinal);
	readonly Dictionary<(string typeName, string assemblyName), ActivationCtorInfo> activationCtorCache = new ();
	readonly ITrimmableTypeMapLogger? logger;
	readonly HashedPackageNamingPolicy packageNamingPolicy;
	readonly HashSet<string> frameworkAssemblyNames;

	public JavaPeerScanner (string? packageNamingPolicy = null, ITrimmableTypeMapLogger? logger = null, HashSet<string>? frameworkAssemblyNames = null)
	{
		this.packageNamingPolicy = ParsePackageNamingPolicy (packageNamingPolicy);
		this.logger = logger;
		this.frameworkAssemblyNames = frameworkAssemblyNames ?? new HashSet<string> (StringComparer.OrdinalIgnoreCase);
	}

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
	public List<JavaPeerInfo> Scan (IReadOnlyList<(string Name, PEReader Reader)> assemblies)
		=> Scan (assemblies.Select (a => new AssemblyInput (a.Name, "", a.Reader)));

	public List<JavaPeerInfo> Scan (IEnumerable<AssemblyInput> assemblies)
	{
		foreach (var assembly in assemblies) {
			var index = AssemblyIndex.Create (assembly.Reader, assembly.Name, assembly.Path);
			assemblyCache [index.AssemblyName] = index;
		}

		// Key by (managedTypeName, assemblyName) to avoid collisions when two assemblies
		// define a type with the same managed name (e.g. Java.Lang.Throwable in both
		// Java.Interop and Mono.Android).
		var resultsByQualifiedName = new Dictionary<(string ManagedName, string AssemblyName), JavaPeerInfo> ();
		foreach (var index in assemblyCache.Values) {
			ScanAssembly (index, resultsByQualifiedName);
		}
		ForceUnconditionalCrossReferences (resultsByQualifiedName, assemblyCache);
		MarkFrameworkArrayEntryPeers (resultsByQualifiedName.Values);
		return new List<JavaPeerInfo> (resultsByQualifiedName.Values);
	}

	void MarkFrameworkArrayEntryPeers (IEnumerable<JavaPeerInfo> peers)
	{
		var referencedFrameworkTypes = new HashSet<string> (StringComparer.Ordinal);
		foreach (var index in assemblyCache.Values) {
			if (frameworkAssemblyNames.Contains (index.AssemblyName)) {
				continue;
			}
			foreach (var referencedTypeNames in index.ReferencedTypeNamesByAssembly) {
				if (frameworkAssemblyNames.Contains (referencedTypeNames.Key)) {
					referencedFrameworkTypes.UnionWith (referencedTypeNames.Value);
				}
			}
		}

		foreach (var peer in peers) {
			if (!peer.IsFrameworkAssembly) {
				continue;
			}

			peer.GenerateArrayEntries = referencedFrameworkTypes.Contains (peer.ManagedTypeName);
		}
	}

	/// <summary>
	/// Scans all loaded assemblies for assembly-level manifest attributes.
	/// Must be called after <see cref="Scan"/>.
	/// </summary>
	internal AssemblyManifestInfo ScanAssemblyManifestInfo ()
	{
		var info = new AssemblyManifestInfo ();
		foreach (var index in assemblyCache.Values) {
			index.ScanAssemblyAttributes (info);
		}
		return info;
	}

	/// <summary>
	/// Types referenced by [Application(BackupAgent = typeof(X))] or
	/// [Application(ManageSpaceActivity = typeof(X))] must be unconditional,
	/// because the manifest will reference them even if nothing else does.
	/// </summary>
	static void ForceUnconditionalCrossReferences (Dictionary<(string ManagedName, string AssemblyName), JavaPeerInfo> results, Dictionary<string, AssemblyIndex> assemblyCache)
	{
		foreach (var index in assemblyCache.Values) {
			foreach (var attrInfo in index.AttributesByType.Values) {
				if (attrInfo is ApplicationAttributeInfo applicationAttributeInfo) {
					ForceUnconditionalIfPresent (results, applicationAttributeInfo.BackupAgent);
					ForceUnconditionalIfPresent (results, applicationAttributeInfo.ManageSpaceActivity);
				}
			}
		}
	}

	static void ForceUnconditionalIfPresent (Dictionary<(string ManagedName, string AssemblyName), JavaPeerInfo> results, string? managedTypeName)
	{
		if (managedTypeName is null) {
			return;
		}

		managedTypeName = managedTypeName.Trim ();
		if (managedTypeName.Length == 0) {
			return;
		}

		// TryGetTypeProperty may return assembly-qualified names like "Ns.Type, Assembly, ..."
		// Strip to just the type name for lookup
		var commaIndex = managedTypeName.IndexOf (',');
		if (commaIndex > 0) {
			managedTypeName = managedTypeName.Substring (0, commaIndex).Trim ();
		}

		if (managedTypeName.Length == 0) {
			return;
		}

		// Search by managed type name across all assemblies (BackupAgent/ManageSpaceActivity
		// attribute values are not assembly-qualified).
		foreach (var key in results.Keys) {
			if (string.Equals (key.ManagedName, managedTypeName, StringComparison.Ordinal)) {
				results [key] = results [key] with { IsUnconditional = true };
			}
		}
	}

	void ScanAssembly (AssemblyIndex index, Dictionary<(string ManagedName, string AssemblyName), JavaPeerInfo> results)
	{
		foreach (var typeHandle in index.Reader.TypeDefinitions) {
			var typeDef = index.Reader.GetTypeDefinition (typeHandle);

			// Skip module-level types
			if (index.Reader.GetString (typeDef.Name) == "<Module>") {
				continue;
			}

			var fullName = MetadataTypeNameResolver.GetFullName (typeDef, index.Reader);

			// Temporarily allow [JniAddNativeMethodRegistrationAttribute] while we investigate
			// which scenarios fail later in the trimmable typemap pipeline.
			// if (index.MayUseJniAddNativeMethodRegistrationAttribute &&
			//     !IsBuiltInJniAddNativeMethodRegistrationType (fullName, index) &&
			//     HasJniAddNativeMethodRegistrationAttribute (typeDef, index)) {
			// 	logger?.LogJniAddNativeMethodRegistrationAttributeError (fullName);
			// }

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
				// [JniTypeSignature] with ArrayRank > 0 represents a JNI array wrapper
				// (e.g., JavaBooleanArray, JavaObjectArray<T>, JavaPrimitiveArray<T>).
				// These are handled by the built-in tables in JniRuntime.JniTypeManager
				// and must not be added to the typemap — keyword types (Z, B, etc.)
				// would collide with GetPrimitiveArrayTypesForSimpleReference, and
				// non-keyword array types would add unnecessary aliases.
				if (registerInfo.IsArrayType) {
					continue;
				}
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

			var isInterface = (typeDef.Attributes & TypeAttributes.Interface) != 0;
			var isAbstract = (typeDef.Attributes & TypeAttributes.Abstract) != 0;
			var isGenericDefinition = typeDef.GetGenericParameters ().Count > 0;

			var isUnconditional = attrInfo is not null;
			var cannotRegisterInStaticConstructor = attrInfo is ApplicationAttributeInfo or InstrumentationAttributeInfo;
			string? invokerTypeName = null;
			ActivationCtorStyle? invokerActivationCtorStyle = null;

			// Resolve base Java type name
			var baseJavaName = ResolveBaseJavaName (typeDef, index, results);

			// Resolve implemented Java interface names
			var implementedInterfaces = ResolveImplementedInterfaceJavaNames (typeDef, index);

			// Collect marshal methods (including constructors).
			// Override and interface detection is only for user ACW class types:
			// - MCW types (DoNotGenerateAcw) already have [Register] on every method
			// - Interface types don't implement other interfaces' methods in JCWs
			var (marshalMethods, exportFields) = CollectMarshalMethods (typeDef, index, detectBaseOverrides: !doNotGenerateAcw && !isInterface);

			// Resolve activation constructor
			var activationCtor = ResolveActivationCtor (fullName, typeDef, index);

			// For interfaces/abstract types, try to find invoker type name
			if (isInterface || isAbstract) {
				invokerTypeName = TryFindInvokerTypeName (fullName, typeHandle, index);
			}

			// Interface/abstract peers create their invoker, not the target type.
			// Keep ActivationCtor scoped to the target/base hierarchy for legacy parity,
			// and store the invoker ctor style separately for CreateInstance emission.
			if (invokerTypeName is not null) {
				invokerActivationCtorStyle = TryResolveActivationCtorOnInvoker (invokerTypeName)?.Style;
			}

			var peer = new JavaPeerInfo {
				JavaName = jniName,
				CompatJniName = compatJniName,
				ManagedTypeName = fullName,
				ManagedTypeNamespace = ExtractNamespace (fullName),
				ManagedTypeShortName = ExtractShortName (fullName),
				AssemblyName = index.AssemblyName,
				IsFrameworkAssembly = frameworkAssemblyNames.Contains (index.AssemblyName),
				GenerateArrayEntries = !frameworkAssemblyNames.Contains (index.AssemblyName),
				BaseJavaName = baseJavaName,
				ImplementedInterfaceJavaNames = implementedInterfaces,
				IsInterface = isInterface,
				IsAbstract = isAbstract,
				DoNotGenerateAcw = doNotGenerateAcw,
				IsFromJniTypeSignature = registerInfo?.IsFromJniTypeSignature ?? false,
				IsUnconditional = isUnconditional,
				CannotRegisterInStaticConstructor = cannotRegisterInStaticConstructor,
				MarshalMethods = marshalMethods,
				JavaConstructors = BuildJavaConstructors (marshalMethods, typeDef, index),
				JavaFields = exportFields,
				ActivationCtor = activationCtor,
				InvokerTypeName = invokerTypeName,
				InvokerActivationCtorStyle = invokerActivationCtorStyle,
				IsGenericDefinition = isGenericDefinition,
				ComponentAttribute = ToComponentInfo (attrInfo),
			};

			results [(fullName, index.AssemblyName)] = peer;
		}
	}

	(List<MarshalMethodInfo>, List<JavaFieldInfo>) CollectMarshalMethods (TypeDefinition typeDef, AssemblyIndex index, bool detectBaseOverrides)
	{
		var methods = new List<MarshalMethodInfo> ();
		var fields = new List<JavaFieldInfo> ();
		var registeredMethodKeys = new HashSet<string> (StringComparer.Ordinal);

		// Pass 1: collect methods with [Register], [Export], or [ExportField] directly on them
		foreach (var methodHandle in typeDef.GetMethods ()) {
			var methodDef = index.Reader.GetMethodDefinition (methodHandle);

			// Check for [ExportField] — produces both a marshal method AND a field
			CollectExportField (methodDef, index, fields);

			if (!TryGetMethodRegisterInfo (methodDef, index, out var registerInfo, out var exportInfo) || registerInfo is null) {
				continue;
			}

			AddMarshalMethod (methods, registerInfo, methodDef, index, exportInfo);
			// Only [Register]-direct (and [JniConstructorSignature]) registrations
			// should preempt Pass 3 base-override detection. [Export]/[ExportField]
			// are orthogonal to a [Register]-driven override on the same method —
			// e.g., `[Export("foo")] public override void OnCreate(...)` needs both
			// the [Register]-driven override entry (Get*Handler connector) AND the
			// [Export]-driven entry. Skip the dedup key for [Export]/[ExportField].
			if (exportInfo is null) {
				var sig = methodDef.DecodeSignature (SignatureTypeProvider.Instance, genericContext: default);
				registeredMethodKeys.Add ($"{index.Reader.GetString (methodDef.Name)}({string.Join (",", sig.ParameterTypes)})");
			}
		}

		// Pass 2: collect [Register] from properties (attribute is on the property, not the getter)
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
				var sig = getterDef.DecodeSignature (SignatureTypeProvider.Instance, genericContext: default);
				registeredMethodKeys.Add ($"{index.Reader.GetString (getterDef.Name)}({string.Join (",", sig.ParameterTypes)})");
			}
		}

		// Pass 3–4: detect overrides and constructors from base hierarchy.
		// Only for user ACW types — MCW types (DoNotGenerateAcw=true) already have
		// [Register] on every method that matters. Running override detection on them
		// would incorrectly pick up internal overrides (e.g., JavaObject.equals).
		if (detectBaseOverrides) {
			CollectBaseMethodOverrides (typeDef, index, methods, registeredMethodKeys);
		}

		// Pass 4: detect interface method implementations.
		// When a type implements a Java interface (e.g., IOnClickListener), the
		// implementing method may not have [Register]. The legacy pipeline adds
		// these via the interface loop in CecilImporter.cs lines 100-120.
		if (detectBaseOverrides) {
			CollectInterfaceMethodImplementations (typeDef, index, methods, registeredMethodKeys);
		}

		// Pass 5: detect Java constructors that chain from base registered ctors.
		if (detectBaseOverrides) {
			CollectBaseConstructorChain (typeDef, index, methods);
		}

		return (methods, fields);
	}

	static bool HasJniAddNativeMethodRegistrationAttribute (TypeDefinition typeDef, AssemblyIndex index)
	{
		const string JniAddNativeMethodRegistrationAttribute = "JniAddNativeMethodRegistrationAttribute";
		const string JavaInteropNamespace = "Java.Interop";

		foreach (var methodHandle in typeDef.GetMethods ()) {
			var methodDef = index.Reader.GetMethodDefinition (methodHandle);
			foreach (var attrHandle in methodDef.GetCustomAttributes ()) {
				var attr = index.Reader.GetCustomAttribute (attrHandle);
				if (AssemblyIndex.IsCustomAttributeMatch (attr, index.Reader, JavaInteropNamespace, JniAddNativeMethodRegistrationAttribute)) {
					return true;
				}
			}
		}
		return false;
	}

	static bool IsBuiltInJniAddNativeMethodRegistrationType (string fullName, AssemblyIndex index)
	{
		return string.Equals (index.AssemblyName, "Java.Interop", StringComparison.Ordinal) &&
			string.Equals (fullName, "Java.Interop.JavaProxyObject", StringComparison.Ordinal);
	}

	/// <summary>
	/// For each virtual override method on <paramref name="typeDef"/> that wasn't already
	/// collected (no direct [Register]), walks up the base type hierarchy to find a
	/// registered base method with matching name and compatible signature. If found,
	/// adds the registration info as a marshal method with the declaring type set to
	/// the base type that owns the [Register] attribute.
	/// </summary>
	void CollectBaseMethodOverrides (TypeDefinition typeDef, AssemblyIndex index,
		List<MarshalMethodInfo> methods, HashSet<string> alreadyRegistered)
	{
		foreach (var methodHandle in typeDef.GetMethods ()) {
			var methodDef = index.Reader.GetMethodDefinition (methodHandle);
			var attrs = methodDef.Attributes;

			// Only virtual overrides: must be Virtual and NOT NewSlot (new keyword).
			// NewSlot means a new virtual method, not an override.
			if ((attrs & MethodAttributes.Virtual) == 0 ||
			    (attrs & MethodAttributes.NewSlot) != 0 ||
			    (attrs & MethodAttributes.Static) != 0) {
				continue;
			}

			var methodName = index.Reader.GetString (methodDef.Name);

			// Skip constructors
			if (methodName == ".ctor" || methodName == ".cctor") {
				continue;
			}

			// Build a unique key from the managed signature to allow multiple
			// overloads with the same name (e.g., Read(), Read(byte[]), Read(byte[],int,int))
			var sig = methodDef.DecodeSignature (SignatureTypeProvider.Instance, genericContext: default);
			var sigKey = $"{methodName}({string.Join (",", sig.ParameterTypes)})";

			// Skip methods already collected from direct [Register]
			if (alreadyRegistered.Contains (sigKey)) {
				continue;
			}

			// Walk base types looking for a registered method with this name
			var baseRegistration = FindBaseRegisteredMethod (typeDef, index, methodName, methodDef);
			if (baseRegistration is not null) {
				methods.Add (baseRegistration);
				alreadyRegistered.Add (sigKey);
			}
		}

		// Also check property overrides: a derived type may override a property
		// whose getter is registered on a base type (e.g., Throwable.Message)
		CollectBasePropertyOverrides (typeDef, index, methods, alreadyRegistered);
	}

	/// <summary>
	/// Checks for property overrides where the base property has [Register] on the property
	/// definition. Property [Register] attributes are on the PropertyDefinition, not the getter,
	/// so we need separate handling.
	/// </summary>
	void CollectBasePropertyOverrides (TypeDefinition typeDef, AssemblyIndex index,
		List<MarshalMethodInfo> methods, HashSet<string> alreadyRegistered)
	{
		foreach (var propHandle in typeDef.GetProperties ()) {
			var propDef = index.Reader.GetPropertyDefinition (propHandle);
			var accessors = propDef.GetAccessors ();
			if (accessors.Getter.IsNil) {
				continue;
			}

			var getterDef = index.Reader.GetMethodDefinition (accessors.Getter);
			var attrs = getterDef.Attributes;

			if ((attrs & MethodAttributes.Virtual) == 0 ||
			    (attrs & MethodAttributes.NewSlot) != 0 ||
			    (attrs & MethodAttributes.Static) != 0) {
				continue;
			}

			var getterName = index.Reader.GetString (getterDef.Name);
			var sig = getterDef.DecodeSignature (SignatureTypeProvider.Instance, genericContext: default);
			var sigKey = $"{getterName}({string.Join (",", sig.ParameterTypes)})";
			if (alreadyRegistered.Contains (sigKey)) {
				continue;
			}

			var baseRegistration = FindBaseRegisteredProperty (typeDef, index, getterName, getterDef);
			if (baseRegistration is not null) {
				methods.Add (baseRegistration);
				alreadyRegistered.Add (sigKey);
			}
		}
	}

	/// <summary>
	/// Detects methods from implemented Java interfaces that aren't directly [Register]'d
	/// on the implementing type. Mirrors the legacy CecilImporter interface loop (lines 100-120):
	/// for each implemented interface with [Register], adds its registered methods to the type.
	/// </summary>
	void CollectInterfaceMethodImplementations (TypeDefinition typeDef, AssemblyIndex index,
		List<MarshalMethodInfo> methods, HashSet<string> alreadyRegistered)
	{
		foreach (var implHandle in typeDef.GetInterfaceImplementations ()) {
			var impl = index.Reader.GetInterfaceImplementation (implHandle);
			var resolved = ResolveEntityHandle (impl.Interface, index);
			if (resolved is null) {
				continue;
			}

			var ifaceTypeName = resolved.ManagedTypeName;
			var ifaceAssemblyName = resolved.AssemblyName;
			if (!TryResolveType (ifaceTypeName, ifaceAssemblyName, out var ifaceHandle, out var ifaceIndex)) {
				continue;
			}

			// Only process interfaces that are Java peers (have [Register])
			if (!ifaceIndex.RegisterInfoByType.ContainsKey (ifaceHandle)) {
				continue;
			}

			var ifaceTypeDef = ifaceIndex.Reader.GetTypeDefinition (ifaceHandle);

			// Add registered methods from this interface
			foreach (var ifaceMethodHandle in ifaceTypeDef.GetMethods ()) {
				var ifaceMethodDef = ifaceIndex.Reader.GetMethodDefinition (ifaceMethodHandle);

				if ((ifaceMethodDef.Attributes & MethodAttributes.Static) != 0) {
					continue;
				}

				if (!TryGetMethodRegisterInfo (ifaceMethodDef, ifaceIndex, out var registerInfo, out _) || registerInfo is null) {
					continue;
				}

				// Skip type-level [Register] (no signature = just the JNI name)
				if (registerInfo.Signature is null && registerInfo.Connector is null) {
					continue;
				}

				string jniSignature = registerInfo.Signature ?? "()V";
				var jniKey = $"{registerInfo.JniName}:{jniSignature}";

				if (alreadyRegistered.Contains (jniKey)) {
					continue;
				}

				// Also check by managed signature to avoid duplicates from
				// direct [Register] that used different dedup keys
				var managedName = ifaceIndex.Reader.GetString (ifaceMethodDef.Name);
				var sig = ifaceMethodDef.DecodeSignature (SignatureTypeProvider.Instance, genericContext: default);
				var managedKey = $"{managedName}({string.Join (",", sig.ParameterTypes)})";
				if (alreadyRegistered.Contains (managedKey)) {
					continue;
				}

				AddMarshalMethod (methods, registerInfo, ifaceMethodDef, ifaceIndex, isInterfaceImplementation: true);

				alreadyRegistered.Add (jniKey);
				alreadyRegistered.Add (managedKey);
			}

			// Also add registered properties from this interface
			foreach (var ifacePropHandle in ifaceTypeDef.GetProperties ()) {
				var ifacePropDef = ifaceIndex.Reader.GetPropertyDefinition (ifacePropHandle);
				var propRegister = TryGetPropertyRegisterInfo (ifacePropDef, ifaceIndex);
				if (propRegister is null || propRegister.Signature is null) {
					continue;
				}

				var jniKey = $"{propRegister.JniName}:{propRegister.Signature}";
				if (alreadyRegistered.Contains (jniKey)) {
					continue;
				}

				var accessors = ifacePropDef.GetAccessors ();
				if (!accessors.Getter.IsNil) {
					var getterDef = ifaceIndex.Reader.GetMethodDefinition (accessors.Getter);
					AddMarshalMethod (methods, propRegister, getterDef, ifaceIndex, isInterfaceImplementation: true);
				}

				alreadyRegistered.Add (jniKey);
			}
		}
	}

	/// <summary>
	/// Detects Java constructors by chaining from base registered ctors.
	/// Mirrors the legacy CecilImporter behavior:
	/// 1. Walk the base type hierarchy collecting registered ctors (stopping at DoNotGenerateAcw)
	/// 2. Add all base registered ctors as seed constructors (legacy adds them to the wrapper directly)
	/// 3. For each ctor on this type without [Register], accept it if a base registered ctor
	///    has compatible parameters.
	/// 4. Fallback: if any base registered ctor is parameterless, accept the user ctor and
	///    compute its JNI signature from the managed parameter types.
	/// </summary>
	void CollectBaseConstructorChain (TypeDefinition typeDef, AssemblyIndex index,
		List<MarshalMethodInfo> methods)
	{
		// Collect JNI signatures of ctors already registered via Pass 1 (direct [Register])
		var alreadyRegisteredSignatures = new HashSet<string> (StringComparer.Ordinal);
		foreach (var m in methods) {
			if (m.IsConstructor) {
				alreadyRegisteredSignatures.Add (m.JniSignature);
			}
		}

		// Collect registered ctors from base type hierarchy
		var baseRegisteredCtors = CollectBaseRegisteredCtors (typeDef, index);
		if (baseRegisteredCtors.Count == 0) {
			return;
		}

		// Add all base registered ctors as seed constructors.
		// Legacy CecilImporter processes base types first (ctorTypes is reversed) and adds
		// their registered ctors directly to the wrapper's Constructors list.
		bool hasParameterlessBaseCtor = false;
		foreach (var baseCtor in baseRegisteredCtors) {
			var signature = baseCtor.RegisterInfo.Signature;
			if (signature is null) {
				continue;
			}
			if (!alreadyRegisteredSignatures.Contains (signature)) {
				methods.Add (new MarshalMethodInfo {
					JniName = baseCtor.RegisterInfo.JniName,
					JniSignature = signature,
					Connector = baseCtor.RegisterInfo.Connector,
					ManagedMethodName = ".ctor",
					NativeCallbackName = "n_ctor",
					IsConstructor = true,
				});
				alreadyRegisteredSignatures.Add (signature);
			}
			if (signature == "()V") {
				hasParameterlessBaseCtor = true;
			}
		}

		// Check each ctor on this type for additional constructors not yet covered
		foreach (var methodHandle in typeDef.GetMethods ()) {
			var methodDef = index.Reader.GetMethodDefinition (methodHandle);
			var name = index.Reader.GetString (methodDef.Name);

			if (name != ".ctor") {
				continue;
			}

			// Skip if this ctor already has [Register] or [JniConstructorSignature] (collected in Pass 1)
			if (TryGetMethodRegisterInfo (methodDef, index, out _, out _)) {
				continue;
			}

			// Check if this ctor's params are already covered by a base registered ctor
			bool alreadyCovered = false;
			foreach (var baseCtor in baseRegisteredCtors) {
				if (HaveIdenticalParameterTypes (methodDef, baseCtor.Method, baseCtor.Index, baseCtor.DeclaringType)) {
					alreadyCovered = true;
					break;
				}
			}
			if (alreadyCovered) {
				continue;
			}

			// Fallback: if any base registered ctor is parameterless, accept this ctor
			// and compute its JNI signature from the managed parameter types.
			// The generated Java ctor calls super() (the parameterless base ctor),
			// then delegates to nctor_N(...) which handles the args on the managed side.
			// This matches legacy CecilImporter behavior (CecilImporter.cs:394-397).
			if (hasParameterlessBaseCtor) {
				var sig = methodDef.DecodeSignature (SignatureTypeProvider.Instance, genericContext: default);
				var jniSignature = BuildJniCtorSignature (sig);
				if (jniSignature is not null && !alreadyRegisteredSignatures.Contains (jniSignature)) {
					methods.Add (new MarshalMethodInfo {
						JniName = ".ctor",
						JniSignature = jniSignature,
						Connector = "",
						ManagedMethodName = ".ctor",
						NativeCallbackName = "n_ctor",
						IsConstructor = true,
						SuperArgumentsString = "",
					});
					alreadyRegisteredSignatures.Add (jniSignature);
				}
			}
		}
	}

	string? BuildJniCtorSignature (MethodSignature<string> sig)
	{
		var sb = new System.Text.StringBuilder ();
		sb.Append ('(');
		foreach (var param in sig.ParameterTypes) {
			// Legacy GetJniSignature returns null for non-Java types (System.IntPtr,
			// System.Object, System.Action, etc.). ManagedTypeToJniDescriptor maps
			// these to "Ljava/lang/Object;" by default, but legacy would reject the
			// whole ctor. Use the nullable variant to match legacy behavior.
			var jniType = ManagedTypeToJniDescriptorOrNull (param);
			if (jniType is null) {
				return null;
			}
			sb.Append (jniType);
		}
		sb.Append (")V");
		return sb.ToString ();
	}

	/// <summary>
	/// Maps a managed type name to its JNI descriptor for constructor signature
	/// computation. Returns null for types that can't be mapped to JNI
	/// (matching legacy GetJniSignature behavior). For Java peer object types
	/// (types with [Register]), resolves to "L&lt;jniName&gt;;" via assembly cache.
	/// </summary>
	string? ManagedTypeToJniDescriptorOrNull (string managedType)
	{
		var primitive = TryGetPrimitiveJniDescriptor (managedType);
		if (primitive is not null) {
			return primitive;
		}

		if (managedType.EndsWith ("[]")) {
			var elementType = ManagedTypeToJniDescriptorOrNull (managedType.Substring (0, managedType.Length - 2));
			return elementType is not null ? $"[{elementType}" : null;
		}

		// Try to resolve as a Java peer type with [Register]
		return TryResolveJniObjectDescriptor (managedType);
	}

	/// <summary>
	/// Looks up a managed type name across loaded assemblies. If the type has
	/// [Register], returns "L&lt;jniName&gt;;". Otherwise returns null.
	/// </summary>
	string? TryResolveJniObjectDescriptor (string managedType)
	{
		foreach (var index in assemblyCache.Values) {
			if (index.TypesByFullName.TryGetValue (managedType, out var handle)) {
				if (index.RegisterInfoByType.TryGetValue (handle, out var registerInfo)) {
					return $"L{registerInfo.JniName};";
				}

				// User peer types (extend a Java peer but lack [Register])
				// get a CRC64-based JNI name in ScanAssembly. Mirror that here
				// so [Export]/[ExportField] signatures referring to such types
				// emit the correct peer descriptor instead of falling back to
				// java/lang/Object.
				var typeDef = index.Reader.GetTypeDefinition (handle);
				if (ExtendsJavaPeer (typeDef, index)) {
					var (jniName, _) = ComputeAutoJniNames (typeDef, index);
					return $"L{jniName};";
				}
			}
		}
		return null;
	}

	/// <summary>
	/// Resolves a `typeof(X)` argument captured as an assembly-qualified name
	/// (e.g. <c>"Java.IO.IOException, Mono.Android, ..."</c>) to its JNI internal
	/// name (<c>java/io/IOException</c>). Returns null when the type cannot be
	/// found among the loaded assemblies or has no [Register] attribute.
	/// </summary>
	string? ResolveTypeOfArgumentToJniName (string assemblyQualifiedName)
	{
		var commaIdx = assemblyQualifiedName.IndexOf (',');
		var typeName = (commaIdx >= 0 ? assemblyQualifiedName.Substring (0, commaIdx) : assemblyQualifiedName).Trim ();
		var descriptor = TryResolveJniObjectDescriptor (typeName);
		if (descriptor is null || descriptor.Length < 3) {
			return null;
		}
		// Strip leading 'L' and trailing ';' to get "java/io/IOException".
		return descriptor.Substring (1, descriptor.Length - 2);
	}

	/// <summary>
	/// If <paramref name="managedType"/> resolves to an enum type, returns the
	/// JNI descriptor of its underlying primitive ("I", "B", "S", "J"). Otherwise
	/// returns null. Mirrors legacy CallbackCode behavior, where enum parameters
	/// are passed via their underlying integer JNI ABI rather than as objects.
	/// </summary>
	string? TryResolveEnumUnderlyingDescriptor (string managedType, string? assemblyName = null)
	{
		var typeDef = TryFindEnumTypeDefinition (managedType, assemblyName);
		if (typeDef is null) {
			return null;
		}

		return GetEnumUnderlyingPrimitiveDescriptor (typeDef.Value.typeDef, typeDef.Value.index);
	}

	/// <summary>
	/// Returns true if <paramref name="managedType"/>, or — for array types —
	/// its element type, resolves to an enum. The IL emitter uses this to encode
	/// the type as a valuetype rather than a class in signatures and member refs.
	/// </summary>
	bool IsEnumOrEnumArray (string managedType, string? assemblyName = null)
	{
		while (managedType.EndsWith ("[]", StringComparison.Ordinal)) {
			managedType = managedType.Substring (0, managedType.Length - 2);
		}

		return TryFindEnumTypeDefinition (managedType, assemblyName) is not null;
	}

	(TypeDefinition typeDef, AssemblyIndex index)? TryFindEnumTypeDefinition (string managedType, string? assemblyName = null)
	{
		// Prefer the typed assembly hint so two assemblies with same-named types
		// (one enum, one not) resolve deterministically — assemblyCache
		// enumeration order is non-deterministic.
		if (assemblyName is { Length: > 0 } &&
		    assemblyCache.TryGetValue (assemblyName, out var hintedIndex) &&
		    hintedIndex.TypesByFullName.TryGetValue (managedType, out var hintedHandle)) {
			var hintedDef = hintedIndex.Reader.GetTypeDefinition (hintedHandle);
			if (IsEnumType (hintedDef, hintedIndex)) {
				return (hintedDef, hintedIndex);
			}
			// Hinted assembly had a same-named non-enum; keep scanning.
		}

		foreach (var index in assemblyCache.Values) {
			if (!index.TypesByFullName.TryGetValue (managedType, out var handle)) {
				continue;
			}

			var typeDef = index.Reader.GetTypeDefinition (handle);
			if (IsEnumType (typeDef, index)) {
				return (typeDef, index);
			}
		}

		return null;
	}

	/// <summary>
	/// Returns <paramref name="type"/> with <see cref="TypeRefData.IsEnum"/> set
	/// when the managed type — or, for arrays, the element type — resolves to an
	/// enum. Used to thread enum-ness from the scanner to the emitter so that
	/// signatures and member refs encode the type as a valuetype.
	/// </summary>
	TypeRefData EnrichTypeRefWithEnumInfo (TypeRefData type)
	{
		if (type.IsEnum || string.IsNullOrEmpty (type.ManagedTypeName)) {
			return type;
		}

		return IsEnumOrEnumArray (type.ManagedTypeName, type.AssemblyName) ? type with { IsEnum = true, IsValueType = true } : type;
	}

	static bool IsEnumType (TypeDefinition typeDef, AssemblyIndex index)
	{
		var baseType = typeDef.BaseType;
		if (baseType.IsNil) {
			return false;
		}

		var baseFullName = baseType.Kind switch {
			HandleKind.TypeReference => MetadataTypeNameResolver.GetTypeFromReference (index.Reader, (TypeReferenceHandle) baseType, rawTypeKind: 0),
			HandleKind.TypeDefinition => MetadataTypeNameResolver.GetTypeFromDefinition (index.Reader, (TypeDefinitionHandle) baseType, rawTypeKind: 0),
			_ => null,
		};

		return baseFullName == "System.Enum";
	}

	static string GetEnumUnderlyingPrimitiveDescriptor (TypeDefinition typeDef, AssemblyIndex index)
	{
		foreach (var fieldHandle in typeDef.GetFields ()) {
			var field = index.Reader.GetFieldDefinition (fieldHandle);
			if ((field.Attributes & System.Reflection.FieldAttributes.Static) != 0) {
				continue;
			}

			var sig = field.DecodeSignature (SignatureTypeProvider.Instance, genericContext: null);
			return TryGetPrimitiveJniDescriptor (sig) ?? "I";
		}

		return "I";
	}

	/// <summary>
	/// Walks the base type hierarchy collecting constructors that have [Register] attributes.
	/// Stops after the first base type with DoNotGenerateAcw=true (matching legacy CecilImporter).
	/// Returns them ordered from nearest base to furthest ancestor.
	/// </summary>
	List<BaseCtorInfo> CollectBaseRegisteredCtors (TypeDefinition typeDef, AssemblyIndex index)
	{
		var result = new List<BaseCtorInfo> ();
		var currentTypeDef = typeDef;
		var currentIndex = index;

		TypeRefData? currentTypeRef = null;
		while (TryResolveBaseType (currentTypeDef, currentIndex, currentTypeRef, out var baseTypeDef, out var baseHandle, out var baseIndex, out _, out _, out var baseTypeRef)) {
			foreach (var methodHandle in baseTypeDef.GetMethods ()) {
				var methodDef = baseIndex.Reader.GetMethodDefinition (methodHandle);
				var name = baseIndex.Reader.GetString (methodDef.Name);
				if (name != ".ctor") {
					continue;
				}

				if (TryGetMethodRegisterInfo (methodDef, baseIndex, out var registerInfo, out _) &&
				    registerInfo is not null && registerInfo.Signature is not null) {
					result.Add (new BaseCtorInfo (methodDef, baseIndex, registerInfo, baseTypeRef));
				}
			}

			// Stop after the first MCW base type — its registered ctors are collected above,
			// but we don't need to walk further up (matching legacy CecilImporter behavior).
			if (baseIndex.RegisterInfoByType.TryGetValue (baseHandle, out var baseRegInfo) && baseRegInfo.DoNotGenerateAcw) {
				break;
			}

			currentTypeDef = baseTypeDef;
			currentIndex = baseIndex;
			currentTypeRef = baseTypeRef;
		}

		return result;
	}

	/// <summary>
	/// Resolves the base type of the given type definition, returning its TypeDefinition,
	/// TypeDefinitionHandle, and AssemblyIndex for further inspection.
	/// </summary>
	bool TryResolveBaseType (TypeDefinition typeDef, AssemblyIndex index,
		out TypeDefinition baseTypeDef, out TypeDefinitionHandle baseHandle, [NotNullWhen (true)] out AssemblyIndex? baseIndex,
		out string baseTypeName, out string baseAssemblyName, out TypeRefData baseTypeRef)
		=> TryResolveBaseType (typeDef, index, currentTypeRef: null, out baseTypeDef, out baseHandle, out baseIndex, out baseTypeName, out baseAssemblyName, out baseTypeRef);

	bool TryResolveBaseType (TypeDefinition typeDef, AssemblyIndex index, TypeRefData? currentTypeRef,
		out TypeDefinition baseTypeDef, out TypeDefinitionHandle baseHandle, [NotNullWhen (true)] out AssemblyIndex? baseIndex,
		out string baseTypeName, out string baseAssemblyName, out TypeRefData baseTypeRef)
	{
		baseTypeDef = default;
		baseHandle = default;
		baseIndex = null;
		baseTypeName = "";
		baseAssemblyName = "";
		baseTypeRef = new TypeRefData {
			ManagedTypeName = "",
			AssemblyName = "",
		};

		var baseInfo = GetBaseTypeInfo (typeDef, index);
		if (baseInfo is null) {
			return false;
		}

		baseTypeRef = currentTypeRef is null ? baseInfo : SubstituteGenericArguments (baseInfo, currentTypeRef);
		baseTypeName = baseTypeRef.ManagedTypeName;
		baseAssemblyName = baseTypeRef.AssemblyName;

		if (!TryResolveType (baseTypeName, baseAssemblyName, out baseHandle, out baseIndex)) {
			return false;
		}

		baseTypeDef = baseIndex.Reader.GetTypeDefinition (baseHandle);
		return true;
	}

	static TypeRefData SubstituteGenericArguments (TypeRefData type, TypeRefData context)
	{
		if (type.ManagedTypeName.EndsWith ("[]", StringComparison.Ordinal)) {
			var elementType = SubstituteGenericArguments (type with {
				ManagedTypeName = type.ManagedTypeName.Substring (0, type.ManagedTypeName.Length - 2),
			}, context);
			return elementType with {
				ManagedTypeName = $"{elementType.ManagedTypeName}[]",
			};
		}

		if (type.ManagedTypeName.StartsWith ("!", StringComparison.Ordinal) &&
		    !type.ManagedTypeName.StartsWith ("!!", StringComparison.Ordinal) &&
		    int.TryParse (type.ManagedTypeName.Substring (1), out int parameterIndex) &&
		    (uint) parameterIndex < (uint) context.GenericArguments.Count) {
			return context.GenericArguments [parameterIndex];
		}

		if (type.GenericArguments.Count == 0) {
			return type;
		}

		var arguments = new TypeRefData [type.GenericArguments.Count];
		for (int i = 0; i < arguments.Length; i++) {
			arguments [i] = SubstituteGenericArguments (type.GenericArguments [i], context);
		}
		return type with {
			GenericArguments = arguments,
		};
	}

	readonly record struct BaseCtorInfo (MethodDefinition Method, AssemblyIndex Index, RegisterInfo RegisterInfo, TypeRefData DeclaringType);

	/// <summary>
	/// Walks the base type hierarchy looking for a method with [Register] that matches
	/// the given method name and has a compatible signature. Returns the registration
	/// info along with the declaring type's full name and assembly name (needed so
	/// UCO wrappers call n_* on the correct base type).
	/// </summary>
	(RegisterInfo Info, TypeRefData DeclaringType)? FindBaseRegisteredMethodInfo (
		TypeDefinition typeDef, AssemblyIndex index, string methodName, MethodDefinition derivedMethod, TypeRefData? currentTypeRef = null)
	{
		if (!TryResolveBaseType (typeDef, index, currentTypeRef, out var baseTypeDef, out _, out var baseIndex, out _, out _, out var baseTypeRef)) {
			return null;
		}

		// Check methods on this base type
		foreach (var baseMethodHandle in baseTypeDef.GetMethods ()) {
			var baseMethodDef = baseIndex.Reader.GetMethodDefinition (baseMethodHandle);
			var baseName = baseIndex.Reader.GetString (baseMethodDef.Name);

			if (baseName != methodName) {
				continue;
			}

			if ((baseMethodDef.Attributes & MethodAttributes.Virtual) == 0 &&
			    (baseMethodDef.Attributes & MethodAttributes.Abstract) == 0) {
				continue;
			}

			if (!HaveIdenticalParameterTypes (derivedMethod, baseMethodDef, baseIndex, baseTypeRef)) {
				continue;
			}

			// Found a matching base method — check if it has [Register].
			// [Export] / [ExportField] are AttributeUsage(Inherited=false), so a
			// derived override must NOT inherit a base [Export] registration —
			// only [Register]-driven entries propagate through inheritance.
			if (TryGetMethodRegisterInfo (baseMethodDef, baseIndex, out var registerInfo, out var exportInfo) && registerInfo is not null && exportInfo is null) {
				return (registerInfo, baseTypeRef);
			}
		}

		// Keep walking the full base hierarchy so overrides can inherit [Register]
		// metadata declared above an intermediate MCW base type.
		return FindBaseRegisteredMethodInfo (baseTypeDef, baseIndex, methodName, derivedMethod, baseTypeRef);
	}

	MarshalMethodInfo? FindBaseRegisteredMethod (TypeDefinition typeDef, AssemblyIndex index,
		string methodName, MethodDefinition derivedMethod)
	{
		var result = FindBaseRegisteredMethodInfo (typeDef, index, methodName, derivedMethod);
		if (result is null || result.Value.Info.Signature is null) {
			return null;
		}

		var registerInfo = result.Value.Info;
		bool isConstructor = registerInfo.JniName == "<init>" || registerInfo.JniName == ".ctor";
		return new MarshalMethodInfo {
			JniName = registerInfo.JniName,
			JniSignature = registerInfo.Signature,
			Connector = registerInfo.Connector,
			ManagedMethodName = methodName,
			NativeCallbackName = GetNativeCallbackName (registerInfo.Connector, methodName, isConstructor),
			IsConstructor = isConstructor,
			DeclaringTypeName = result.Value.DeclaringType.ManagedTypeName,
			DeclaringAssemblyName = result.Value.DeclaringType.AssemblyName,
			DeclaringType = result.Value.DeclaringType,
		};
	}

	/// <summary>
	/// Walks the base type hierarchy looking for a property with [Register] whose getter
	/// matches the given getter name and has a compatible signature.
	/// </summary>
	MarshalMethodInfo? FindBaseRegisteredProperty (TypeDefinition typeDef, AssemblyIndex index,
		string getterName, MethodDefinition derivedGetter, TypeRefData? currentTypeRef = null)
	{
		if (!TryResolveBaseType (typeDef, index, currentTypeRef, out var baseTypeDef, out _, out var baseIndex, out _, out _, out var baseTypeRef)) {
			return null;
		}

		// Check properties on this base type
		foreach (var basePropHandle in baseTypeDef.GetProperties ()) {
			var basePropDef = baseIndex.Reader.GetPropertyDefinition (basePropHandle);
			var baseAccessors = basePropDef.GetAccessors ();
			if (baseAccessors.Getter.IsNil) {
				continue;
			}

			var baseGetterDef = baseIndex.Reader.GetMethodDefinition (baseAccessors.Getter);
			var baseGetterName = baseIndex.Reader.GetString (baseGetterDef.Name);
			if (baseGetterName != getterName) {
				continue;
			}

			if ((baseGetterDef.Attributes & MethodAttributes.Virtual) == 0 &&
			    (baseGetterDef.Attributes & MethodAttributes.Abstract) == 0) {
				continue;
			}

			// Check if the base property has [Register]
			var propRegister = TryGetPropertyRegisterInfo (basePropDef, baseIndex);
			if (propRegister is not null && propRegister.Signature is not null) {
				return new MarshalMethodInfo {
					JniName = propRegister.JniName,
					JniSignature = propRegister.Signature,
					Connector = propRegister.Connector,
					ManagedMethodName = getterName,
					NativeCallbackName = GetNativeCallbackName (propRegister.Connector, getterName, false),
					IsConstructor = false,
					DeclaringTypeName = baseTypeRef.ManagedTypeName,
					DeclaringAssemblyName = baseTypeRef.AssemblyName,
					DeclaringType = baseTypeRef,
				};
			}
		}

		// Keep walking the full base hierarchy so property overrides can inherit
		// [Register] metadata declared above an intermediate MCW base type.
		return FindBaseRegisteredProperty (baseTypeDef, baseIndex, getterName, derivedGetter, baseTypeRef);
	}

	/// <summary>
	/// Checks if two methods have identical parameter types by comparing their decoded signatures.
	/// </summary>
	static bool HaveIdenticalParameterTypes (MethodDefinition derivedMethod, MethodDefinition baseMethod, AssemblyIndex baseIndex, TypeRefData baseTypeRef)
	{
		var derivedSig = derivedMethod.DecodeSignature (SignatureTypeProvider.Instance, genericContext: default);
		var baseSig = baseMethod.DecodeSignature (TypeRefSignatureTypeProvider.Instance, genericContext: baseIndex);

		if (derivedSig.ParameterTypes.Length != baseSig.ParameterTypes.Length) {
			return false;
		}

		for (int i = 0; i < derivedSig.ParameterTypes.Length; i++) {
			var baseParameterType = SubstituteGenericArguments (baseSig.ParameterTypes [i], baseTypeRef);
			if (!string.Equals (derivedSig.ParameterTypes [i], baseParameterType.DisplayName, StringComparison.Ordinal)) {
				return false;
			}
		}

		return true;
	}

	void AddMarshalMethod (List<MarshalMethodInfo> methods, RegisterInfo registerInfo, MethodDefinition methodDef, AssemblyIndex index, ExportInfo? exportInfo = null, bool isInterfaceImplementation = false)
	{
		// Skip methods that are just the JNI name (type-level [Register])
		if (registerInfo.Signature is null && registerInfo.Connector is null) {
			return;
		}

		bool isConstructor = registerInfo.JniName == "<init>" || registerInfo.JniName == ".ctor";
		bool isExport = exportInfo is not null;
		string managedName = index.Reader.GetString (methodDef.Name);
		var managedSig = methodDef.DecodeSignature (SignatureTypeProvider.Instance, genericContext: default);
		string jniSignature = registerInfo.Signature ?? "()V";

		string declaringTypeName = "";
		string declaringAssemblyName = "";
		ParseConnectorDeclaringType (registerInfo.Connector, out declaringTypeName, out declaringAssemblyName);

		bool mayCallManagedMethodDirectly = ShouldCallManagedMethodDirectly (isConstructor, isExport, declaringTypeName);

		// Only decode TypeRefData signatures for methods that need direct dispatch IL
		// generation; static n_* callback forwarders already encode from the JNI signature.
		var managedTypeSig = mayCallManagedMethodDirectly
			? methodDef.DecodeSignature (TypeRefSignatureTypeProvider.Instance, index)
			: default;
		bool callManagedMethodDirectly = isExport || (mayCallManagedMethodDirectly && SupportsDirectManagedMethodCall (managedTypeSig));
		var parameterKinds = exportInfo?.ParameterKinds ?? CreateDefaultExportKinds (managedSig.ParameterTypes.Length);

		var managedParameterTypes = new List<TypeRefData> ();
		if (callManagedMethodDirectly) {
			foreach (var parameterType in managedTypeSig.ParameterTypes) {
				managedParameterTypes.Add (EnrichTypeRefWithEnumInfo (parameterType));
			}
		}

		methods.Add (new MarshalMethodInfo {
			JniName = registerInfo.JniName,
			JniSignature = jniSignature,
			Connector = registerInfo.Connector,
			ManagedMethodName = managedName,
			DeclaringTypeName = declaringTypeName,
			DeclaringAssemblyName = declaringAssemblyName,
			NativeCallbackName = GetNativeCallbackName (registerInfo.Connector, managedName, isConstructor),
			ManagedParameterTypes = managedParameterTypes,
			ManagedParameterExportKinds = parameterKinds,
			ManagedReturnType = callManagedMethodDirectly ? EnrichTypeRefWithEnumInfo (managedTypeSig.ReturnType) : new TypeRefData {
				ManagedTypeName = managedSig.ReturnType,
				AssemblyName = "System.Runtime",
			},
			ManagedReturnExportKind = exportInfo?.ReturnKind ?? ExportParameterKindInfo.Unspecified,
			IsStatic = (methodDef.Attributes & MethodAttributes.Static) == MethodAttributes.Static,
			IsConstructor = isConstructor,
			IsExport = isExport,
			IsInterfaceImplementation = isInterfaceImplementation,
			JavaAccess = isExport ? GetJavaAccess (methodDef.Attributes & MethodAttributes.MemberAccessMask) : null,
			ThrownNames = exportInfo?.ThrownNames,
			SuperArgumentsString = exportInfo?.SuperArgumentsString,
			CallManagedMethodDirectly = callManagedMethodDirectly,
		});
	}

	static bool ShouldCallManagedMethodDirectly (bool isConstructor, bool isExport, string declaringTypeName)
	{
		if (isExport) {
			return true;
		}

		if (isConstructor) {
			return false;
		}

		// Direct [Register] methods have no connector-declared callback owner, so forwarding
		// through n_* may bind to an inherited callback. If the type hides a base virtual
		// member with "new virtual" but keeps the same JNI method, that inherited callback
		// dispatches through the base slot instead of the scanned method. Calling the managed
		// method body directly keeps the wrapper tied to this managed method.
		return declaringTypeName.IsNullOrEmpty ();
	}

	static bool SupportsDirectManagedMethodCall (MethodSignature<TypeRefData> managedTypeSig)
	{
		if (!SupportsDirectManagedMethodCall (managedTypeSig.ReturnType)) {
			return false;
		}

		foreach (var parameterType in managedTypeSig.ParameterTypes) {
			if (!SupportsDirectManagedMethodCall (parameterType)) {
				return false;
			}
		}

		return true;
	}

	static bool SupportsDirectManagedMethodCall (TypeRefData type)
	{
		if (type.GenericArguments.Count > 0) {
			return false;
		}

		var typeName = type.ManagedTypeName;
		if (typeName.EndsWith ("&", StringComparison.Ordinal) || typeName.EndsWith ("*", StringComparison.Ordinal)) {
			return false;
		}

		while (typeName.EndsWith ("[]", StringComparison.Ordinal)) {
			typeName = typeName.Substring (0, typeName.Length - 2);
		}

		return !typeName.StartsWith ("!", StringComparison.Ordinal);
	}

	static string GetJavaAccess (MethodAttributes access)
	{
		return access switch {
			MethodAttributes.Public => "public",
			MethodAttributes.FamORAssem => "protected",
			MethodAttributes.Family => "protected",
			_ => "private",
		};
	}

	string? ResolveBaseJavaName (TypeDefinition typeDef, AssemblyIndex index, Dictionary<(string ManagedName, string AssemblyName), JavaPeerInfo> results)
	{
		if (!TryResolveBaseType (typeDef, index, out var baseTypeDef, out _, out var baseIndex, out var baseTypeName, out _, out _)) {
			return null;
		}

		// First try [Register] attribute
		var registerJniName = ResolveRegisterJniName (baseTypeName, baseIndex.AssemblyName);
		if (registerJniName is not null) {
			return registerJniName;
		}

		// Fall back to already-scanned results (component-attributed or hashed-package peers)
		if (results.TryGetValue ((baseTypeName, baseIndex.AssemblyName), out var basePeer)) {
			return basePeer.JavaName;
		}

		// Base type may be a Java peer without [Register] that hasn't been scanned yet
		// (scan order within an assembly is not guaranteed). Resolve it the same way
		// ScanAssembly does: check ExtendsJavaPeer and compute the auto JNI name.
		if (ExtendsJavaPeer (baseTypeDef, baseIndex)) {
			var (jniName, _) = ComputeAutoJniNames (baseTypeDef, baseIndex);
			return jniName;
		}

		return null;
	}

	List<string> ResolveImplementedInterfaceJavaNames (TypeDefinition typeDef, AssemblyIndex index)
	{
		var result = new List<string> ();
		var interfaceImpls = typeDef.GetInterfaceImplementations ();

		foreach (var implHandle in interfaceImpls) {
			var impl = index.Reader.GetInterfaceImplementation (implHandle);
			var ifaceJniName = ResolveInterfaceJniName (impl.Interface, index);
			if (ifaceJniName is not null) {
				result.Add (ifaceJniName);
			}
		}

		return result;
	}

	string? ResolveInterfaceJniName (EntityHandle interfaceHandle, AssemblyIndex index)
	{
		var resolved = ResolveEntityHandle (interfaceHandle, index);
		return resolved is not null ? ResolveRegisterJniName (resolved.ManagedTypeName, resolved.AssemblyName) : null;
	}

	bool TryGetMethodRegisterInfo (MethodDefinition methodDef, AssemblyIndex index, out RegisterInfo? registerInfo, out ExportInfo? exportInfo)
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

			if (attrName == "ExportFieldAttribute") {
				(registerInfo, exportInfo) = ParseExportFieldAsMethod (ca, methodDef, index);
				return true;
			}

			// JI-style constructor registration: [JniConstructorSignature("()V")]
			// Single arg = JNI signature; name is always ".ctor", connector is empty.
			if (attrName == "JniConstructorSignatureAttribute") {
				var value = index.DecodeAttribute (ca);
				var jniSignature = value.FixedArguments.Length > 0 ? (string?)value.FixedArguments [0].Value : null;
				if (jniSignature is not null) {
					registerInfo = new RegisterInfo { JniName = ".ctor", Signature = jniSignature, Connector = "", DoNotGenerateAcw = false };
					return true;
				}
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

	(RegisterInfo registerInfo, ExportInfo exportInfo) ParseExportAttribute (CustomAttribute ca, MethodDefinition methodDef, AssemblyIndex index)
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
			} else if (named.Name == "Throws" && named.Value is ImmutableArray<CustomAttributeTypedArgument<string>> throwsTypes) {
				// Throws is `Type[]` in source, but the metadata blob serializes each
				// `typeof(X)` as a string (assembly-qualified type name) routed through
				// our CustomAttributeTypeProvider's GetTypeFromSerializedName. Resolve
				// each to its [Register]-driven JNI internal name so the runtime can
				// emit `throws` clauses on the generated Java method.
				thrownNames ??= new List<string> (throwsTypes.Length);
				foreach (var item in throwsTypes) {
					if (item.Value is string aqn) {
						var jni = ResolveTypeOfArgumentToJniName (aqn);
						if (jni is not null) {
							thrownNames.Add (jni);
						}
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
		var sig = methodDef.DecodeSignature (TypeRefSignatureTypeProvider.Instance, index);
		var (parameterKinds, returnKind) = GetExportParameterKinds (methodDef, index, sig.ParameterTypes.Length);
		var jniSig = BuildJniSignatureFromManaged (sig, parameterKinds, returnKind);

		return (
			new RegisterInfo { JniName = resolvedExportName, Signature = jniSig, Connector = null, DoNotGenerateAcw = false },
			new ExportInfo {
				ThrownNames = thrownNames,
				SuperArgumentsString = superArguments,
				ParameterKinds = parameterKinds,
				ReturnKind = returnKind,
			}
		);
	}

	static List<ExportParameterKindInfo> CreateDefaultExportKinds (int parameterCount)
	{
		var kinds = new List<ExportParameterKindInfo> (parameterCount);
		for (int i = 0; i < parameterCount; i++) {
			kinds.Add (ExportParameterKindInfo.Unspecified);
		}
		return kinds;
	}

	static (List<ExportParameterKindInfo> parameterKinds, ExportParameterKindInfo returnKind) GetExportParameterKinds (MethodDefinition methodDef, AssemblyIndex index, int parameterCount)
	{
		var parameterKinds = CreateDefaultExportKinds (parameterCount);
		var returnKind = ExportParameterKindInfo.Unspecified;

		foreach (var parameterHandle in methodDef.GetParameters ()) {
			var parameter = index.Reader.GetParameter (parameterHandle);
			var kind = GetExportParameterKind (parameter, index);
			if (kind == ExportParameterKindInfo.Unspecified) {
				continue;
			}

			if (parameter.SequenceNumber == 0) {
				returnKind = kind;
			} else {
				int parameterIndex = parameter.SequenceNumber - 1;
				if (parameterIndex >= 0 && parameterIndex < parameterKinds.Count) {
					parameterKinds [parameterIndex] = kind;
				}
			}
		}

		return (parameterKinds, returnKind);
	}

	static ExportParameterKindInfo GetExportParameterKind (Parameter parameter, AssemblyIndex index)
	{
		foreach (var caHandle in parameter.GetCustomAttributes ()) {
			var ca = index.Reader.GetCustomAttribute (caHandle);
			var attrName = AssemblyIndex.GetCustomAttributeName (ca, index.Reader);
			if (attrName != "ExportParameterAttribute") {
				continue;
			}

			var value = index.DecodeAttribute (ca);
			if (value.FixedArguments.Length > 0 && TryConvertExportParameterKind (value.FixedArguments [0].Value, out var ctorKind)) {
				return ctorKind;
			}

			foreach (var named in value.NamedArguments) {
				if (named.Name == "Kind" && TryConvertExportParameterKind (named.Value, out var namedKind)) {
					return namedKind;
				}
			}
		}

		return ExportParameterKindInfo.Unspecified;
	}

	static bool TryConvertExportParameterKind (object? value, out ExportParameterKindInfo kind)
	{
		if (value is int i && Enum.IsDefined (typeof (ExportParameterKindInfo), i)) {
			kind = (ExportParameterKindInfo) i;
			return true;
		}

		kind = ExportParameterKindInfo.Unspecified;
		return false;
	}

	string BuildJniSignatureFromManaged (MethodSignature<TypeRefData> sig, IReadOnlyList<ExportParameterKindInfo> parameterKinds, ExportParameterKindInfo returnKind)
	{
		var sb = new System.Text.StringBuilder ();
		sb.Append ('(');
		for (int i = 0; i < sig.ParameterTypes.Length; i++) {
			var exportKind = i < parameterKinds.Count ? parameterKinds [i] : ExportParameterKindInfo.Unspecified;
			sb.Append (ManagedTypeToJniDescriptor (sig.ParameterTypes [i], exportKind));
		}
		sb.Append (')');
		sb.Append (ManagedTypeToJniDescriptor (sig.ReturnType, returnKind));
		return sb.ToString ();
	}

	/// <summary>
	/// Parses an [ExportField] attribute as a marshal method registration.
	/// [ExportField] methods use the managed method name as the JNI name and have
	/// a connector of "__export__" (matching legacy CecilImporter behavior).
	/// </summary>
	(RegisterInfo registerInfo, ExportInfo exportInfo) ParseExportFieldAsMethod (CustomAttribute ca, MethodDefinition methodDef, AssemblyIndex index)
	{
		var managedName = index.Reader.GetString (methodDef.Name);
		var sig = methodDef.DecodeSignature (TypeRefSignatureTypeProvider.Instance, index);
		var jniSig = BuildJniSignatureFromManaged (sig, CreateDefaultExportKinds (sig.ParameterTypes.Length), ExportParameterKindInfo.Unspecified);

		return (
			new RegisterInfo { JniName = managedName, Signature = jniSig, Connector = "__export__", DoNotGenerateAcw = false },
			new ExportInfo { ThrownNames = null, SuperArgumentsString = null }
		);
	}

	/// <summary>
	/// Maps a managed type name to its JNI descriptor. Resolves Java-bound types
	/// via their [Register] attribute, falling back to "Ljava/lang/Object;" only
	/// for types that cannot be resolved (used by [Export] signature computation).
	/// </summary>
	string ManagedTypeToJniDescriptor (TypeRefData managedType, ExportParameterKindInfo exportKind = ExportParameterKindInfo.Unspecified)
	{
		if (exportKind != ExportParameterKindInfo.Unspecified) {
			return exportKind switch {
				ExportParameterKindInfo.InputStream => "Ljava/io/InputStream;",
				ExportParameterKindInfo.OutputStream => "Ljava/io/OutputStream;",
				ExportParameterKindInfo.XmlPullParser => "Lorg/xmlpull/v1/XmlPullParser;",
				ExportParameterKindInfo.XmlResourceParser => "Landroid/content/res/XmlResourceParser;",
				_ => "Ljava/lang/Object;",
			};
		}

		var primitive = TryGetPrimitiveJniDescriptor (managedType.ManagedTypeName);
		if (primitive is not null) {
			return primitive;
		}

		if (managedType.ManagedTypeName.EndsWith ("[]", StringComparison.Ordinal)) {
			return $"[{ManagedTypeToJniDescriptor (managedType with { ManagedTypeName = managedType.ManagedTypeName.Substring (0, managedType.ManagedTypeName.Length - 2) })}";
		}

		// Try to resolve as a Java peer type with [Register]
		var resolved = TryResolveJniObjectDescriptor (managedType.ManagedTypeName);
		if (resolved is not null) {
			return resolved;
		}

		// Well-known interface types that legacy CallbackCode mapped explicitly
		// to their canonical Java type. ICharSequence is in Mono.Android but is
		// not annotated with [Register]; the non-generic collection interfaces
		// live in System.Collections (no Java peer at all) and are wrapped at
		// runtime by JavaList/JavaDictionary/JavaCollection.
		var wellKnown = managedType.ManagedTypeName switch {
			"Java.Lang.ICharSequence"          => "Ljava/lang/CharSequence;",
			"System.Collections.IList"         => "Ljava/util/List;",
			"System.Collections.IDictionary"   => "Ljava/util/Map;",
			"System.Collections.ICollection"   => "Ljava/util/Collection;",
			_ => null,
		};
		if (wellKnown is not null) {
			return wellKnown;
		}

		// Enum parameters use their underlying primitive JNI ABI (matches legacy
		// CallbackCode behavior).
		var enumDescriptor = TryResolveEnumUnderlyingDescriptor (managedType.ManagedTypeName, managedType.AssemblyName);
		if (enumDescriptor is not null) {
			return enumDescriptor;
		}

		return "Ljava/lang/Object;";
	}

	/// <summary>
	/// Returns the JNI descriptor for primitive types and System.String.
	/// Returns null for all other types.
	/// </summary>
	static string? TryGetPrimitiveJniDescriptor (string managedType)
	{
		return managedType switch {
			"System.Void" => "V",
			"System.Boolean" => "Z",
			"System.Byte" => "B",
			"System.SByte" => "B",
			"System.Char" => "C",
			"System.Int16" => "S",
			"System.UInt16" => "S",
			"System.Int32" => "I",
			"System.UInt32" => "I",
			"System.Int64" => "J",
			"System.UInt64" => "J",
			"System.Single" => "F",
			"System.Double" => "D",
			"System.String" => "Ljava/lang/String;",
			_ => null,
		};
	}

	ActivationCtorInfo? ResolveActivationCtor (string typeName, TypeDefinition typeDef, AssemblyIndex index, TypeRefData? currentTypeRef = null)
	{
		var cacheKey = (currentTypeRef?.DisplayName ?? typeName, index.AssemblyName);
		if (activationCtorCache.TryGetValue (cacheKey, out var cached)) {
			return cached;
		}

		// Check this type's constructors
		var ownCtor = FindActivationCtorOnType (typeDef, index);
		if (ownCtor is not null) {
			var info = new ActivationCtorInfo {
				DeclaringTypeName = typeName,
				DeclaringAssemblyName = index.AssemblyName,
				DeclaringType = new TypeRefData {
					ManagedTypeName = typeName,
					AssemblyName = index.AssemblyName,
				},
				Style = ownCtor.Value,
			};
			if (currentTypeRef is not null) {
				info = info with {
					DeclaringType = currentTypeRef,
				};
			}
			activationCtorCache [cacheKey] = info;
			return info;
		}

		// Walk base type hierarchy
		var baseInfo = GetBaseTypeInfo (typeDef, index);
		if (baseInfo is not null) {
			baseInfo = currentTypeRef is null ? baseInfo : SubstituteGenericArguments (baseInfo, currentTypeRef);
			var baseTypeName = baseInfo.ManagedTypeName;
			var baseAssemblyName = baseInfo.AssemblyName;
			if (TryResolveType (baseTypeName, baseAssemblyName, out var baseHandle, out var baseIndex)) {
				var baseTypeDef = baseIndex.Reader.GetTypeDefinition (baseHandle);
				var result = ResolveActivationCtor (baseTypeName, baseTypeDef, baseIndex, baseInfo);
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
	TypeRefData? ResolveTypeSpecification (TypeSpecificationHandle specHandle, AssemblyIndex index)
	{
		var typeSpec = index.Reader.GetTypeSpecification (specHandle);
		return typeSpec.DecodeSignature (TypeRefSignatureTypeProvider.Instance, index);
	}

	/// <summary>
	/// Resolves an EntityHandle (TypeDef, TypeRef, or TypeSpec) to (typeName, assemblyName).
	/// Shared by base type resolution, interface resolution, and any handle-to-name lookup.
	/// </summary>
	TypeRefData? ResolveEntityHandle (EntityHandle handle, AssemblyIndex index)
	{
		switch (handle.Kind) {
		case HandleKind.TypeDefinition: {
			var td = index.Reader.GetTypeDefinition ((TypeDefinitionHandle)handle);
			return new TypeRefData {
				ManagedTypeName = MetadataTypeNameResolver.GetFullName (td, index.Reader),
				AssemblyName = index.AssemblyName,
			};
		}
		case HandleKind.TypeReference:
			return MetadataTypeNameResolver.GetTypeRefFromReference (index.Reader, (TypeReferenceHandle)handle, index.AssemblyName, rawTypeKind: 0);
		case HandleKind.TypeSpecification:
			return ResolveTypeSpecification ((TypeSpecificationHandle)handle, index);
		default:
			return null;
		}
	}

	TypeRefData? GetBaseTypeInfo (TypeDefinition typeDef, AssemblyIndex index)
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
				return NormalizeConnectorManagedTypeName (connector.Substring (0, commaIndex));
			}
			if (connector.Length > 0) {
				return NormalizeConnectorManagedTypeName (connector);
			}
		}

		// Fallback: convention-based lookup — invoker type is TypeName + "Invoker"
		var invokerName = $"{typeName}Invoker";
		if (index.TypesByFullName.ContainsKey (invokerName)) {
			return invokerName;
		}
		return null;
	}

	static string NormalizeConnectorManagedTypeName (string managedTypeName)
	{
		return managedTypeName.Trim ().Replace ('/', '+');
	}

	/// <summary>
	/// Resolve the activation ctor on a known invoker type (search all loaded assemblies).
	/// Used for interface peers, whose own type definition has no constructors.
	/// The assemblyCache typically contains 10–30 entries (app + framework assemblies),
	/// and each lookup is an O(1) dictionary probe, so the linear scan is cheap.
	/// </summary>
	ActivationCtorInfo? TryResolveActivationCtorOnInvoker (string invokerTypeName)
	{
		foreach (var assembly in assemblyCache.Values) {
			if (!assembly.TypesByFullName.TryGetValue (invokerTypeName, out var invokerHandle)) {
				continue;
			}
			var invokerDef = assembly.Reader.GetTypeDefinition (invokerHandle);
			return ResolveActivationCtor (invokerTypeName, invokerDef, assembly);
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

		var baseTypeName = baseInfo.ManagedTypeName;
		var baseAssemblyName = baseInfo.AssemblyName;

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
	/// JNI name uses the selected package naming policy hash for "namespace:assemblyName".
	/// Compat JNI name uses the raw managed namespace (lowercased).
	/// If a declaring type has [Register], its JNI name is used as prefix for both.
	/// Generic backticks are replaced with _.
	/// </summary>
	(string jniName, string compatJniName) ComputeAutoJniNames (TypeDefinition typeDef, AssemblyIndex index)
	{
		var (typeName, parentJniName, ns) = ComputeTypeNameParts (typeDef, index);

		if (parentJniName is not null) {
			var name = $"{parentJniName}_{typeName}";
			return (name, name);
		}

		var packageName = GetHashedPackageName (ns, index.AssemblyName);
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
	/// as prefix instead of computing hashed package names from the namespace.
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

	/// <summary>
	/// Derives the native callback method name from a <c>[Register]</c> attribute's Connector field.
	/// The Connector may be a simple name like <c>"GetOnCreate_Landroid_os_Bundle_Handler"</c>
	/// or a qualified name like <c>"GetOnClick_Landroid_view_View_Handler:Android.Views.View/IOnClickListenerInvoker, Mono.Android, …"</c>.
	/// In both cases the result is e.g. <c>"n_OnCreate_Landroid_os_Bundle_"</c>.
	/// Falls back to <c>"n_{managedName}"</c> when the Connector doesn't follow the expected pattern.
	/// </summary>
	static string GetNativeCallbackName (string? connector, string managedName, bool isConstructor)
	{
		if (isConstructor) {
			return "n_ctor";
		}

		if (connector is not null) {
			// Strip the optional type qualifier after ':'
			int colonIndex = connector.IndexOf (':');
			string handlerName = colonIndex >= 0 ? connector.Substring (0, colonIndex) : connector;

			if (handlerName.StartsWith ("Get", StringComparison.Ordinal)
				&& handlerName.EndsWith ("Handler", StringComparison.Ordinal)) {
				return "n_" + handlerName.Substring (3, handlerName.Length - 3 - "Handler".Length);
			}
		}

		return $"n_{managedName}";
	}

	/// <summary>
	/// Parses the type qualifier from a Connector string.
	/// Connector format is either assembly-qualified:
	/// <c>"GetOnClickHandler:Android.Views.View/IOnClickListenerInvoker, Mono.Android, Version=…"</c>
	/// or type-only: <c>"GetOnClickHandler:Android.Views.IOnClickListenerInvoker"</c>.
	/// Extracts the managed type name (converting <c>/</c> → <c>+</c> for nested types) and assembly name (if present).
	/// </summary>
	static void ParseConnectorDeclaringType (string? connector, out string declaringTypeName, out string declaringAssemblyName)
	{
		declaringTypeName = "";
		declaringAssemblyName = "";

		if (connector is null) {
			return;
		}

		int colonIndex = connector.IndexOf (':');
		if (colonIndex < 0) {
			return;
		}

		// After ':' is typically "TypeName, AssemblyName, Version=…" (assembly-qualified name),
		// but some connectors only provide "TypeName" without an assembly.
		string typeQualified = connector.Substring (colonIndex + 1);
		int commaIndex = typeQualified.IndexOf (',');

		if (commaIndex < 0) {
			// No assembly information; treat the whole segment as the type name
			declaringTypeName = NormalizeConnectorManagedTypeName (typeQualified);
			return;
		}

		declaringTypeName = NormalizeConnectorManagedTypeName (typeQualified.Substring (0, commaIndex));
		string rest = typeQualified.Substring (commaIndex + 1).Trim ();
		int nextComma = rest.IndexOf (',');
		declaringAssemblyName = nextComma >= 0 ? rest.Substring (0, nextComma).Trim () : rest.Trim ();
	}

	string GetHashedPackageName (string ns, string assemblyName)
	{
		// Only Mono.Android preserves the namespace directly
		if (assemblyName == "Mono.Android") {
			return ns.ToLowerInvariant ().Replace ('.', '/');
		}

		return packageNamingPolicy switch {
			HashedPackageNamingPolicy.LowercaseCrc64 => "crc64" + ScannerHashingHelper.ToLegacyCrc64 (ns, assemblyName),
			HashedPackageNamingPolicy.Crc64 => "scrc64" + ScannerHashingHelper.ToCrc64 (ns, assemblyName),
			_ => throw new InvalidOperationException ($"Unsupported package naming policy: {packageNamingPolicy}"),
		};
	}

	static HashedPackageNamingPolicy ParsePackageNamingPolicy (string? packageNamingPolicy)
	{
		if (packageNamingPolicy.IsNullOrEmpty ()) {
			return HashedPackageNamingPolicy.Crc64;
		}
		if (string.Equals (packageNamingPolicy, "Crc64", StringComparison.OrdinalIgnoreCase)) {
			return HashedPackageNamingPolicy.Crc64;
		}
		if (string.Equals (packageNamingPolicy, "LowercaseCrc64", StringComparison.OrdinalIgnoreCase)) {
			return HashedPackageNamingPolicy.LowercaseCrc64;
		}
		throw new ArgumentException ($"Unsupported AndroidPackageNamingPolicy value '{packageNamingPolicy}' for trimmable typemap. Supported values are 'Crc64' and 'LowercaseCrc64'.", nameof (packageNamingPolicy));
	}

	static string ExtractNamespace (string fullName)
	{
		// Strip nested type suffix (e.g., "My.Namespace.Outer+Inner" → "My.Namespace.Outer")
		int plusIndex = fullName.IndexOf ('+');
		var nameForNamespace = plusIndex >= 0 ? fullName.Substring (0, plusIndex) : fullName;
		int lastDot = nameForNamespace.LastIndexOf ('.');
		return lastDot >= 0 ? nameForNamespace.Substring (0, lastDot) : "";
	}

	static string ExtractShortName (string fullName)
	{
		var span = fullName.AsSpan ();
		int lastDot = span.LastIndexOf ('.');
		var typePart = lastDot >= 0 ? span.Slice (lastDot + 1) : span;
		int lastPlus = typePart.LastIndexOf ('+');
		return (lastPlus >= 0 ? typePart.Slice (lastPlus + 1) : typePart).ToString ();
	}

	List<JavaConstructorInfo> BuildJavaConstructors (List<MarshalMethodInfo> marshalMethods, TypeDefinition typeDef, AssemblyIndex index)
	{
		var ctors = new List<JavaConstructorInfo> ();
		int ctorIndex = 0;
		foreach (var mm in marshalMethods) {
			if (!mm.IsConstructor) {
				continue;
			}
			// Try to find a managed ctor whose signature matches the JNI ctor.
			// Unsupported managed parameter shapes fail in model building for [Export]
			// constructors; non-[Export] registrations keep the legacy activation fallback.
			var managedParams = TryGetMatchingPublicConstructorParameterTypes (typeDef, mm.JniSignature, index);
			ctors.Add (new JavaConstructorInfo {
				JniSignature = mm.JniSignature,
				ConstructorIndex = ctorIndex,
				SuperArgumentsString = mm.SuperArgumentsString,
				HasMatchingManagedCtor = managedParams != null,
				ManagedParameterTypes = managedParams ?? [],
			});
			ctorIndex++;
		}
		return ctors;
	}

	/// <summary>
	/// Attempts to find a managed instance constructor on <paramref name="typeDef"/>
	/// whose parameters match the supplied JNI signature, and returns its managed
	/// parameter types. Returns <see langword="null"/> when no compatible
	/// constructor exists.
	/// </summary>
	IReadOnlyList<TypeRefData>? TryGetMatchingPublicConstructorParameterTypes (TypeDefinition typeDef, string jniSignature, AssemblyIndex index)
	{
		var jniParams = JniSignatureHelper.ParseParameters (jniSignature);
		foreach (var methodHandle in typeDef.GetMethods ()) {
			var methodDef = index.Reader.GetMethodDefinition (methodHandle);
			if ((methodDef.Attributes & MethodAttributes.Static) != 0) {
				continue;
			}
			var name = index.Reader.GetString (methodDef.Name);
			if (name != ".ctor") {
				continue;
			}
			if ((methodDef.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public) {
				continue;
			}
			var sig = methodDef.DecodeSignature (TypeRefSignatureTypeProvider.Instance, genericContext: index);
			if (sig.ParameterTypes.Length != jniParams.Count) {
				continue;
			}
			// Skip ctors whose managed parameter signatures are not supported by the
			// trimmable [Export]-style argument marshaller (generic instantiations,
			// by-ref, pointers). Returning null here makes EmitUcoConstructor fall
			// back to the legacy `(IntPtr, JniHandleOwnership)` activation ctor,
			// which matches the legacy LLVM-IR behaviour for these shapes.
			bool unsupportedParam = false;
			foreach (var p in sig.ParameterTypes) {
				var paramTypeName = p.ManagedTypeName;
				if (p.GenericArguments.Count > 0 || paramTypeName.EndsWith ("&", StringComparison.Ordinal) || paramTypeName.EndsWith ("*", StringComparison.Ordinal)) {
					unsupportedParam = true;
					break;
				}
			}
			if (unsupportedParam) {
				continue;
			}
			if (!ManagedConstructorParametersMatchJniSignature (sig.ParameterTypes, jniParams)) {
				continue;
			}
			// If multiple overloads with the same JNI-compatible signature exist, match
			// the first public constructor in metadata order, like TypeManager.Activate.
			return [.. sig.ParameterTypes];
		}
		return null;
	}

	bool ManagedConstructorParametersMatchJniSignature (IReadOnlyList<TypeRefData> managedParams, IReadOnlyList<JniParameterInfo> jniParams)
	{
		if (managedParams.Count != jniParams.Count) {
			return false;
		}

		for (int i = 0; i < managedParams.Count; i++) {
			var managedDescriptor = ManagedTypeToJniDescriptor (managedParams [i]);
			if (!string.Equals (managedDescriptor, jniParams [i].JniType, StringComparison.Ordinal)) {
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Checks a single method for [ExportField] and adds a JavaFieldInfo if found.
	/// Called inline during Pass 1 to avoid a separate iteration.
	/// </summary>
	void CollectExportField (MethodDefinition methodDef, AssemblyIndex index, List<JavaFieldInfo> fields)
	{
		foreach (var caHandle in methodDef.GetCustomAttributes ()) {
			var ca = index.Reader.GetCustomAttribute (caHandle);
			var attrName = AssemblyIndex.GetCustomAttributeName (ca, index.Reader);

			if (attrName != "ExportFieldAttribute") {
				continue;
			}

			var value = index.DecodeAttribute (ca);
			if (value.FixedArguments.Length == 0) {
				continue;
			}

			var fieldName = (string?)value.FixedArguments [0].Value;
			if (fieldName is null) {
				continue;
			}

			var managedName = index.Reader.GetString (methodDef.Name);
			var sig = methodDef.DecodeSignature (TypeRefSignatureTypeProvider.Instance, index);
			var jniSig = BuildJniSignatureFromManaged (sig, CreateDefaultExportKinds (sig.ParameterTypes.Length), ExportParameterKindInfo.Unspecified);
			var jniReturnType = JniSignatureHelper.ParseReturnTypeString (jniSig);
			var javaReturnType = JniSignatureHelper.JniTypeToJava (jniReturnType);
			var access = GetJavaAccess (methodDef.Attributes & MethodAttributes.MemberAccessMask);
			var isStatic = (methodDef.Attributes & MethodAttributes.Static) != 0;

			fields.Add (new JavaFieldInfo {
				FieldName = fieldName,
				JavaTypeName = javaReturnType,
				InitializerMethodName = managedName,
				Visibility = access,
				IsStatic = isStatic,
			});
		}
	}

	static ComponentInfo? ToComponentInfo (TypeAttributeInfo? attrInfo)
	{
		if (attrInfo is null) {
			return null;
		}

		var kind = attrInfo.AttributeName switch {
			"ActivityAttribute" => ComponentKind.Activity,
			"ServiceAttribute" => ComponentKind.Service,
			"BroadcastReceiverAttribute" => ComponentKind.BroadcastReceiver,
			"ContentProviderAttribute" => ComponentKind.ContentProvider,
			"ApplicationAttribute" => ComponentKind.Application,
			"InstrumentationAttribute" => ComponentKind.Instrumentation,
			_ => (ComponentKind?)null,
		};

		if (kind is null) {
			return null;
		}

		return new ComponentInfo {
			Kind = kind.Value,
			Properties = attrInfo.Properties,
			IntentFilters = attrInfo.IntentFilters,
			MetaData = attrInfo.MetaData,
		};
	}
}
