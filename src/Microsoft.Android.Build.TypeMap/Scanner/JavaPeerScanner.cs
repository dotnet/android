using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;

namespace Microsoft.Android.Build.TypeMap;

/// <summary>
/// Scans assemblies for Java peer types using System.Reflection.Metadata.
/// Two-phase architecture:
///   Phase 1: Build per-assembly indices (fast, O(1) lookups)
///   Phase 2: Analyze types using cached indices
/// </summary>
sealed class JavaPeerScanner : IDisposable
{
	readonly Dictionary<string, AssemblyIndex> assemblyCache = new (StringComparer.Ordinal);
	readonly Dictionary<string, ActivationCtorInfo> activationCtorCache = new (StringComparer.Ordinal);
	readonly Dictionary<string, List<TypeDefinitionHandle>> baseTypeChainCache = new ();

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
		var results = new List<JavaPeerInfo> ();

		foreach (var kvp in assemblyCache) {
			var index = kvp.Value;
			ScanAssembly (index, results);
		}

		return results;
	}

	void ScanAssembly (AssemblyIndex index, List<JavaPeerInfo> results)
	{
		foreach (var typeHandle in index.Reader.TypeDefinitions) {
			var typeDef = index.Reader.GetTypeDefinition (typeHandle);

			// Skip module-level types
			if (IsModuleType (typeDef, index.Reader)) {
				continue;
			}

			// Determine the JNI name and whether this is a known Java peer.
			// Priority:
			//   1. [Register] attribute → use JNI name from attribute
			//   2. Component attribute Name property → convert dots to slashes
			//   3. Extends a known Java peer → auto-compute JNI name via CRC64
			//   4. None of the above → not a Java peer, skip
			string? jniName = null;
			bool doNotGenerateAcw = false;

			index.RegisterInfoByType.TryGetValue (typeHandle, out var registerInfo);
			index.AttributesByType.TryGetValue (typeHandle, out var attrInfo);

			if (registerInfo != null && !string.IsNullOrEmpty (registerInfo.JniName)) {
				jniName = registerInfo.JniName;
				doNotGenerateAcw = registerInfo.DoNotGenerateAcw;
			} else if (attrInfo?.ComponentAttributeJniName != null) {
				// User type with [Activity(Name = "...")] but no [Register]
				jniName = attrInfo.ComponentAttributeJniName;
			} else {
				// No explicit JNI name — check if this type extends a known Java peer.
				// If so, auto-compute JNI name from the managed type name via CRC64.
				if (ExtendsJavaPeer (typeDef, index)) {
					jniName = ComputeJniName (typeDef, GetFullName (typeDef, index.Reader), index);
				} else {
					continue;
				}
			}

			// Skip invoker types (they share JNI names with their interface)
			if (IsInvokerType (typeDef, doNotGenerateAcw, index)) {
				continue;
			}

			var fullName = GetFullName (typeDef, index.Reader);
			var ns = index.Reader.GetString (typeDef.Namespace);
			var shortName = index.Reader.GetString (typeDef.Name);
			if (typeDef.IsNested) {
				var parentName = GetFullName (index.Reader.GetTypeDefinition (typeDef.GetDeclaringType ()), index.Reader);
				ns = parentName;
			}

			var isInterface = (typeDef.Attributes & TypeAttributes.Interface) != 0;
			var isAbstract = (typeDef.Attributes & TypeAttributes.Abstract) != 0;
			var isGenericDefinition = typeDef.GetGenericParameters ().Count > 0;

			var isUnconditional = attrInfo?.HasComponentAttribute ?? false;
			string? invokerTypeName = null;

			// Resolve base Java type name
			var baseJavaName = ResolveBaseJavaName (typeDef, index);

			// Resolve implemented Java interface names
			var implementedInterfaces = ResolveImplementedInterfaceJavaNames (typeDef, index);

			// Collect marshal methods
			var marshalMethods = CollectMarshalMethods (typeDef, index);

			// Collect Java constructors (from [Register] on .ctor methods)
			var javaConstructors = CollectJavaConstructors (typeDef, index);

			// Resolve activation constructor
			var activationCtor = ResolveActivationCtor (fullName, typeDef, index);

			// For interfaces/abstract types, try to find invoker type name
			if (isInterface || isAbstract) {
				invokerTypeName = TryFindInvokerTypeName (fullName, typeHandle, index);
			}

			var peer = new JavaPeerInfo (
				javaName: jniName,
				managedTypeName: fullName,
				managedTypeNamespace: ns,
				managedTypeShortName: shortName,
				assemblyName: index.AssemblyName,
				baseJavaName: baseJavaName,
				implementedInterfaceJavaNames: implementedInterfaces,
				isInterface: isInterface,
				isAbstract: isAbstract,
				doNotGenerateAcw: doNotGenerateAcw,
				isUnconditional: isUnconditional,
				marshalMethods: marshalMethods,
				javaConstructors: javaConstructors,
				activationCtor: activationCtor,
				invokerTypeName: invokerTypeName,
				isGenericDefinition: isGenericDefinition
			);

			results.Add (peer);
		}
	}

	static bool IsModuleType (TypeDefinition typeDef, MetadataReader reader)
	{
		return reader.GetString (typeDef.Name) == "<Module>";
	}

	/// <summary>
	/// Invoker types are implementation details — they implement interfaces
	/// for Java-to-.NET calls. They have DoNotGenerateAcw=true and their name
	/// typically ends with "Invoker".
	/// They are excluded from the TypeMap entirely.
	/// </summary>
	static bool IsInvokerType (TypeDefinition typeDef, bool doNotGenerateAcw, AssemblyIndex index)
	{
		if (!doNotGenerateAcw) {
			return false;
		}

		var name = index.Reader.GetString (typeDef.Name);
		return name.EndsWith ("Invoker", StringComparison.Ordinal);
	}

	List<MarshalMethodInfo> CollectMarshalMethods (TypeDefinition typeDef, AssemblyIndex index)
	{
		var methods = new List<MarshalMethodInfo> ();
		var declaringTypeName = GetFullName (typeDef, index.Reader);

		// Collect [Register] from methods directly
		foreach (var methodHandle in typeDef.GetMethods ()) {
			var methodDef = index.Reader.GetMethodDefinition (methodHandle);
			var methodRegister = TryGetMethodRegisterInfo (methodDef, index);
			if (methodRegister != null) {
				AddMarshalMethod (methods, methodRegister, methodDef, declaringTypeName, index);
			}
		}

		// Collect [Register] from properties (attribute is on the property, not the getter)
		foreach (var propHandle in typeDef.GetProperties ()) {
			var propDef = index.Reader.GetPropertyDefinition (propHandle);
			var propRegister = TryGetPropertyRegisterInfo (propDef, index);
			if (propRegister == null) {
				continue;
			}

			// Resolve the getter method
			var accessors = propDef.GetAccessors ();
			if (!accessors.Getter.IsNil) {
				var getterDef = index.Reader.GetMethodDefinition (accessors.Getter);
				AddMarshalMethod (methods, propRegister, getterDef, declaringTypeName, index);
			}
		}

		return methods;
	}

	void AddMarshalMethod (List<MarshalMethodInfo> methods, RegisterInfo registerInfo, MethodDefinition methodDef, string declaringTypeName, AssemblyIndex index)
	{
		// Skip methods that are just the JNI name (type-level [Register])
		if (registerInfo.Signature == null && registerInfo.Connector == null) {
			return;
		}

		var methodName = index.Reader.GetString (methodDef.Name);

		// Determine if this is a constructor registration
		bool isConstructor = registerInfo.JniName == "<init>" || registerInfo.JniName == ".ctor";

		// Parse JNI signature to get parameter info and return type
		var (parameters, returnType) = ParseJniSignature (registerInfo.Signature ?? "()V");

		// Compute native callback name
		string nativeCallbackName;
		if (isConstructor) {
			nativeCallbackName = "nctor_" + methods.FindAll (m => m.IsConstructor).Count;
		} else {
			nativeCallbackName = "n_" + methodName;
		}

		// The JNI name is the Java method name (without the n_ prefix)
		string jniName = registerInfo.JniName;

		methods.Add (new MarshalMethodInfo (
			jniName: jniName,
			jniSignature: registerInfo.Signature ?? "()V",
			connector: registerInfo.Connector,
			managedMethodName: methodName,
			declaringTypeName: declaringTypeName,
			declaringAssemblyName: index.AssemblyName,
			nativeCallbackName: nativeCallbackName,
			parameters: parameters,
			jniReturnType: returnType,
			isConstructor: isConstructor,
			thrownNames: registerInfo.ThrownNames,
			superArgumentsString: registerInfo.SuperArgumentsString
		));
	}

	List<JavaConstructorInfo> CollectJavaConstructors (TypeDefinition typeDef, AssemblyIndex index)
	{
		var constructors = new List<JavaConstructorInfo> ();

		foreach (var methodHandle in typeDef.GetMethods ()) {
			var methodDef = index.Reader.GetMethodDefinition (methodHandle);
			var methodRegister = TryGetMethodRegisterInfo (methodDef, index);

			if (methodRegister == null) {
				continue;
			}

			if (methodRegister.JniName != "<init>" && methodRegister.JniName != ".ctor") {
				continue;
			}

			var sig = methodRegister.Signature ?? "()V";
			var (parameters, _) = ParseJniSignature (sig);

			constructors.Add (new JavaConstructorInfo (
				jniSignature: sig,
				constructorIndex: constructors.Count,
				parameters: parameters
			));
		}

		return constructors;
	}

	string? ResolveBaseJavaName (TypeDefinition typeDef, AssemblyIndex index)
	{
		var baseInfo = GetBaseTypeInfo (typeDef, index);
		if (baseInfo == null) {
			return null;
		}

		var (baseTypeName, baseAssemblyName) = baseInfo.Value;

		// Check current assembly first
		if (baseAssemblyName == index.AssemblyName) {
			if (index.TypesByFullName.TryGetValue (baseTypeName, out var baseHandle)) {
				if (index.RegisterInfoByType.TryGetValue (baseHandle, out var baseRegister)) {
					return baseRegister.JniName;
				}
			}
		}

		// Check other cached assemblies
		if (assemblyCache.TryGetValue (baseAssemblyName, out var baseIndex)) {
			if (baseIndex.TypesByFullName.TryGetValue (baseTypeName, out var baseHandle)) {
				if (baseIndex.RegisterInfoByType.TryGetValue (baseHandle, out var baseRegister)) {
					return baseRegister.JniName;
				}
			}
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
			if (ifaceJniName != null) {
				result.Add (ifaceJniName);
			}
		}

		return result;
	}

	string? ResolveInterfaceJniName (EntityHandle interfaceHandle, AssemblyIndex index)
	{
		string? typeName = null;
		string? assemblyName = null;

		switch (interfaceHandle.Kind) {
		case HandleKind.TypeDefinition: {
			var td = index.Reader.GetTypeDefinition ((TypeDefinitionHandle)interfaceHandle);
			typeName = GetFullName (td, index.Reader);
			assemblyName = index.AssemblyName;
			break;
		}
		case HandleKind.TypeReference: {
			var tr = index.Reader.GetTypeReference ((TypeReferenceHandle)interfaceHandle);
			var name = index.Reader.GetString (tr.Name);
			var ns = index.Reader.GetString (tr.Namespace);
			typeName = ns.Length > 0 ? ns + "." + name : name;
			if (tr.ResolutionScope.Kind == HandleKind.AssemblyReference) {
				var asmRef = index.Reader.GetAssemblyReference ((AssemblyReferenceHandle)tr.ResolutionScope);
				assemblyName = index.Reader.GetString (asmRef.Name);
			} else {
				assemblyName = index.AssemblyName;
			}
			break;
		}
		default:
			return null;
		}

		if (typeName == null || assemblyName == null) {
			return null;
		}

		// Look up JNI name from the [Register] attribute
		if (assemblyName == index.AssemblyName) {
			if (index.TypesByFullName.TryGetValue (typeName, out var handle) &&
			    index.RegisterInfoByType.TryGetValue (handle, out var regInfo)) {
				return regInfo.JniName;
			}
		}

		if (assemblyCache.TryGetValue (assemblyName, out var otherIndex)) {
			if (otherIndex.TypesByFullName.TryGetValue (typeName, out var handle) &&
			    otherIndex.RegisterInfoByType.TryGetValue (handle, out var regInfo)) {
				return regInfo.JniName;
			}
		}

		return null;
	}

	static RegisterInfo? TryGetMethodRegisterInfo (MethodDefinition methodDef, AssemblyIndex index)
	{
		foreach (var caHandle in methodDef.GetCustomAttributes ()) {
			var ca = index.Reader.GetCustomAttribute (caHandle);
			var attrName = GetCustomAttributeName (ca, index.Reader);

			if (attrName == "RegisterAttribute") {
				return ParseRegisterAttribute (ca, index.Reader);
			}

			if (attrName == "ExportAttribute") {
				return ParseExportAttribute (ca, index.Reader, methodDef, index);
			}
		}
		return null;
	}

	static RegisterInfo? TryGetPropertyRegisterInfo (PropertyDefinition propDef, AssemblyIndex index)
	{
		foreach (var caHandle in propDef.GetCustomAttributes ()) {
			var ca = index.Reader.GetCustomAttribute (caHandle);
			var attrName = GetCustomAttributeName (ca, index.Reader);

			if (attrName == "RegisterAttribute") {
				return ParseRegisterAttribute (ca, index.Reader);
			}
		}
		return null;
	}

	static RegisterInfo ParseExportAttribute (CustomAttribute ca, MetadataReader reader, MethodDefinition methodDef, AssemblyIndex index)
	{
		var value = ca.DecodeValue (new CustomAttributeTypeProvider (reader));

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
			} else if (named.Name == "ThrownNames" && named.Value is string[] names) {
				thrownNames = new List<string> (names);
			} else if (named.Name == "SuperArgumentsString" && named.Value is string superArgs) {
				superArguments = superArgs;
			}
		}

		if (exportName == null || exportName.Length == 0) {
			exportName = index.Reader.GetString (methodDef.Name);
		}

		// Build JNI signature from method signature
		var sig = methodDef.DecodeSignature (new SignatureTypeProvider (), genericContext: default);
		var jniSig = BuildJniSignatureFromManaged (sig);

		return new RegisterInfo (exportName, jniSig, null, false,
			thrownNames: thrownNames, superArgumentsString: superArguments);
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
		case "System.SByte": return "B";
		case "System.Char": return "C";
		case "System.Int16": return "S";
		case "System.Int32": return "I";
		case "System.Int64": return "J";
		case "System.Single": return "F";
		case "System.Double": return "D";
		case "System.String": return "Ljava/lang/String;";
		default:
			if (managedType.EndsWith ("[]")) {
				return "[" + ManagedTypeToJniDescriptor (managedType.Substring (0, managedType.Length - 2));
			}
			return "Ljava/lang/Object;";
		}
	}

	static string? GetCustomAttributeName (CustomAttribute ca, MetadataReader reader)
	{
		if (ca.Constructor.Kind == HandleKind.MemberReference) {
			var memberRef = reader.GetMemberReference ((MemberReferenceHandle)ca.Constructor);
			if (memberRef.Parent.Kind == HandleKind.TypeReference) {
				var typeRef = reader.GetTypeReference ((TypeReferenceHandle)memberRef.Parent);
				return reader.GetString (typeRef.Name);
			}
		} else if (ca.Constructor.Kind == HandleKind.MethodDefinition) {
			var methodDef2 = reader.GetMethodDefinition ((MethodDefinitionHandle)ca.Constructor);
			var declaringType = reader.GetTypeDefinition (methodDef2.GetDeclaringType ());
			return reader.GetString (declaringType.Name);
		}
		return null;
	}

	static RegisterInfo ParseRegisterAttribute (CustomAttribute ca, MetadataReader reader)
	{
		var value = ca.DecodeValue (new CustomAttributeTypeProvider (reader));

		string jniName = "";
		string? signature = null;
		string? connector = null;
		bool doNotGenerateAcw = false;

		if (value.FixedArguments.Length > 0) {
			jniName = (string?)value.FixedArguments [0].Value ?? "";
		}
		if (value.FixedArguments.Length > 1) {
			signature = (string?)value.FixedArguments [1].Value;
		}
		if (value.FixedArguments.Length > 2) {
			connector = (string?)value.FixedArguments [2].Value;
		}

		foreach (var named in value.NamedArguments) {
			if (named.Name == "DoNotGenerateAcw" && named.Value is bool val) {
				doNotGenerateAcw = val;
			}
		}

		return new RegisterInfo (jniName, signature, connector, doNotGenerateAcw);
	}

	ActivationCtorInfo? ResolveActivationCtor (string typeName, TypeDefinition typeDef, AssemblyIndex index)
	{
		if (activationCtorCache.TryGetValue (typeName, out var cached)) {
			return cached;
		}

		// Check this type's constructors
		var ownCtor = FindActivationCtorOnType (typeDef, index);
		if (ownCtor != null) {
			var info = new ActivationCtorInfo (typeName, index.AssemblyName, ownCtor.Value);
			activationCtorCache [typeName] = info;
			return info;
		}

		// Walk base type hierarchy
		var baseInfo = GetBaseTypeInfo (typeDef, index);
		if (baseInfo != null) {
			var (baseTypeName, baseAssemblyName) = baseInfo.Value;
			if (assemblyCache.TryGetValue (baseAssemblyName, out var baseIndex)) {
				if (baseIndex.TypesByFullName.TryGetValue (baseTypeName, out var baseHandle)) {
					var baseTypeDef = baseIndex.Reader.GetTypeDefinition (baseHandle);
					var result = ResolveActivationCtor (baseTypeName, baseTypeDef, baseIndex);
					if (result != null) {
						activationCtorCache [typeName] = result;
					}
					return result;
				}
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

			var sig = method.DecodeSignature (new SignatureTypeProvider (), genericContext: default);

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

	(string typeName, string assemblyName)? GetBaseTypeInfo (TypeDefinition typeDef, AssemblyIndex index)
	{
		var baseTypeHandle = typeDef.BaseType;
		if (baseTypeHandle.IsNil) {
			return null;
		}

		switch (baseTypeHandle.Kind) {
		case HandleKind.TypeDefinition: {
			var baseDef = index.Reader.GetTypeDefinition ((TypeDefinitionHandle)baseTypeHandle);
			return (GetFullName (baseDef, index.Reader), index.AssemblyName);
		}
		case HandleKind.TypeReference: {
			var typeRef = index.Reader.GetTypeReference ((TypeReferenceHandle)baseTypeHandle);
			var name = index.Reader.GetString (typeRef.Name);
			var ns = index.Reader.GetString (typeRef.Namespace);
			var fullName = ns.Length > 0 ? ns + "." + name : name;

			// Resolve assembly from scope
			var scope = typeRef.ResolutionScope;
			if (scope.Kind == HandleKind.AssemblyReference) {
				var asmRef = index.Reader.GetAssemblyReference ((AssemblyReferenceHandle)scope);
				return (fullName, index.Reader.GetString (asmRef.Name));
			}

			return (fullName, index.AssemblyName);
		}
		case HandleKind.TypeSpecification: {
			// Generic base type — resolve the generic type definition
			var typeSpec = index.Reader.GetTypeSpecification ((TypeSpecificationHandle)baseTypeHandle);
			var decoded = typeSpec.DecodeSignature (new SignatureTypeProvider (), genericContext: default);

			// Strip generic arguments: "Ns.Type`1<A,B>" → "Ns.Type`1"
			var angleBracket = decoded.IndexOf ('<');
			if (angleBracket > 0) {
				var genericDef = decoded.Substring (0, angleBracket);
				// The genericDef already includes the backtick+arity (e.g. "Ns.Type`1")
				if (index.TypesByFullName.TryGetValue (genericDef, out _)) {
					return (genericDef, index.AssemblyName);
				}
				// Check other assemblies
				foreach (var asmKvp in assemblyCache) {
					if (asmKvp.Value.TypesByFullName.TryGetValue (genericDef, out _)) {
						return (genericDef, asmKvp.Key);
					}
				}
			}

			// Non-generic fallback: check current assembly first
			if (index.TypesByFullName.ContainsKey (decoded)) {
				return (decoded, index.AssemblyName);
			}

			// Search all cached assemblies
			foreach (var kvp in assemblyCache) {
				if (kvp.Value.TypesByFullName.ContainsKey (decoded)) {
					return (decoded, kvp.Key);
				}
			}

			return null;
		}
		default:
			return null;
		}
	}

	string? TryFindInvokerTypeName (string typeName, TypeDefinitionHandle typeHandle, AssemblyIndex index)
	{
		// First, check the [Register] attribute's connector arg (3rd arg).
		// In real Mono.Android, interfaces have [Register("jni/name", "", "InvokerTypeName, Assembly")]
		// where the connector contains the assembly-qualified invoker type name.
		if (index.RegisterInfoByType.TryGetValue (typeHandle, out var registerInfo) && registerInfo.Connector != null) {
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
		var invokerName = typeName + "Invoker";
		if (index.TypesByFullName.ContainsKey (invokerName)) {
			return invokerName;
		}
		return null;
	}

	static string GetFullName (TypeDefinition typeDef, MetadataReader reader)
	{
		var name = reader.GetString (typeDef.Name);
		var ns = reader.GetString (typeDef.Namespace);

		if (typeDef.IsNested) {
			var declaringType = reader.GetTypeDefinition (typeDef.GetDeclaringType ());
			var parentName = GetFullName (declaringType, reader);
			return parentName + "+" + name;
		}

		if (ns.Length == 0) {
			return name;
		}

		return ns + "." + name;
	}

	static (IReadOnlyList<JniParameterInfo> parameters, string returnType) ParseJniSignature (string signature)
	{
		var parameters = new List<JniParameterInfo> ();
		var returnType = "V";

		if (signature.Length == 0 || signature [0] != '(') {
			return (parameters, returnType);
		}

		int i = 1;
		while (i < signature.Length && signature [i] != ')') {
			var (jniType, managedType, newIndex) = ParseJniType (signature, i);
			parameters.Add (new JniParameterInfo (jniType, managedType));
			i = newIndex;
		}

		if (i < signature.Length - 1) {
			returnType = signature.Substring (i + 1);
		}

		return (parameters, returnType);
	}

	static (string jniType, string managedType, int nextIndex) ParseJniType (string signature, int index)
	{
		switch (signature [index]) {
		case 'Z': return ("Z", "System.Boolean", index + 1);
		case 'B': return ("B", "System.SByte", index + 1);
		case 'C': return ("C", "System.Char", index + 1);
		case 'S': return ("S", "System.Int16", index + 1);
		case 'I': return ("I", "System.Int32", index + 1);
		case 'J': return ("J", "System.Int64", index + 1);
		case 'F': return ("F", "System.Single", index + 1);
		case 'D': return ("D", "System.Double", index + 1);
		case 'V': return ("V", "System.Void", index + 1);
		case 'L': {
			int end = signature.IndexOf (';', index);
			if (end < 0) end = signature.Length;
			var jniType = signature.Substring (index, end - index + 1);
			var managedType = jniType.Substring (1, jniType.Length - 2).Replace ('/', '.');
			return (jniType, managedType, end + 1);
		}
		case '[': {
			var (elementJni, elementManaged, nextIndex) = ParseJniType (signature, index + 1);
			return ("[" + elementJni, elementManaged + "[]", nextIndex);
		}
		default:
			return (signature [index].ToString (), "unknown", index + 1);
		}
	}

	public void Dispose ()
	{
		foreach (var index in assemblyCache.Values) {
			index.Dispose ();
		}
		assemblyCache.Clear ();
	}

	// ================================================================
	// Gap #1: Auto-computed JNI names via CRC64
	// ================================================================

	/// <summary>
	/// Check if a type extends a known Java peer (has [Register] or component attribute)
	/// by walking the base type chain.
	/// </summary>
	bool ExtendsJavaPeer (TypeDefinition typeDef, AssemblyIndex index)
	{
		var baseInfo = GetBaseTypeInfo (typeDef, index);
		if (baseInfo == null) {
			return false;
		}

		var (baseTypeName, baseAssemblyName) = baseInfo.Value;

		// Check if the base type is a known Java peer (has [Register])
		if (assemblyCache.TryGetValue (baseAssemblyName, out var baseIndex)) {
			if (baseIndex.TypesByFullName.TryGetValue (baseTypeName, out var baseHandle)) {
				if (baseIndex.RegisterInfoByType.ContainsKey (baseHandle)) {
					return true;
				}
				// Recurse into base type's base
				var baseDef = baseIndex.Reader.GetTypeDefinition (baseHandle);
				return ExtendsJavaPeer (baseDef, baseIndex);
			}
		}

		return false;
	}

	/// <summary>
	/// Compute a JNI name for a type without [Register] or component Name.
	/// Uses CRC64 hash of "namespace:assemblyName" to generate the package name.
	/// Format: crc64{hash}/{TypeName}
	/// Nested types use _ separator (matching JavaNativeTypeManager.ToJniName).
	/// Generic backticks are replaced with _.
	/// </summary>
	static string ComputeJniName (TypeDefinition typeDef, string fullManagedName, AssemblyIndex index)
	{
		// Build the type name part (handles nesting)
		var nameParts = new List<string> ();
		var current = typeDef;

		while (true) {
			var name = index.Reader.GetString (current.Name).Replace ('`', '_');
			nameParts.Add (name);

			if (!current.IsNested) {
				break;
			}
			current = index.Reader.GetTypeDefinition (current.GetDeclaringType ());
		}

		nameParts.Reverse ();
		var typeName = string.Join ("_", nameParts);

		// Get the namespace from the outermost type
		var ns = index.Reader.GetString (current.Namespace);

		// Compute package name via CRC64
		var packageName = GetCrc64PackageName (ns, index.AssemblyName);

		return packageName + "/" + typeName;
	}

	static string GetCrc64PackageName (string ns, string assemblyName)
	{
		// Only Mono.Android preserves the namespace directly
		if (assemblyName == "Mono.Android") {
			return ns.ToLowerInvariant ().Replace ('.', '/');
		}

		var input = ns + ":" + assemblyName;
		var data = System.Text.Encoding.UTF8.GetBytes (input);
		var hash = System.IO.Hashing.Crc64.Hash (data);

		var sb = new System.Text.StringBuilder ("crc64");
		foreach (var b in hash) {
			sb.AppendFormat ("{0:x2}", b);
		}
		return sb.ToString ();
	}

	// ================================================================
}
