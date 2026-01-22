using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Android.Runtime;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers.Utilities;
using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Linker;
using Mono.Linker.Steps;

namespace Microsoft.Android.Sdk.ILLink;

/// <summary>
/// Generates TypeMap attributes using the .NET 10 TypeMapAttribute and TypeMapAssociationAttribute
/// Find the best .NET type that maps to each Java type, and create the following code:
/// <code>
/// [assembly: TypeMapAttribute("java/lang/JavaClas", typeof(Java.Lang.JavaClass), typeof(Java.Lang.JavaClass))]
/// [assembly: TypeMapAssociationAttribute(typeof(Java.Lang.JavaClass), typeof(Java.Lang.JavaClassProxy))]
///
/// [TypeMapProxy("java/lang/JavaClass")]
/// class JavaClassProxy {
/// Target
/// }
/// </code>
/// </summary>
public class GenerateTypeMapAttributesStep : BaseStep
{
	const string TypeMapAttributeTypeName = "System.Runtime.InteropServices.TypeMapAttribute`1";
	MethodReference TypeMapAttributeCtor;

	const string TypeMapAssociationAttributeTypeName = "System.Runtime.InteropServices.TypeMapAssociationAttribute`1";
	MethodReference TypeMapAssociationAttributeCtor;

	const string TypeMapProxyAttributeTypeName = "Java.Interop.TypeMapProxyAttribute";
	MethodReference TypeMapProxyAttributeCtor;

	const string JavaPeerProxyTypeName = "Java.Interop.JavaPeerProxy";
	TypeReference JavaPeerProxyType { get; set; }
	MethodReference JavaPeerProxyDefaultCtor;

	const string JavaInteropAliasesAttributeTypeName = "Java.Interop.JavaInteropAliasesAttribute";
	MethodReference JavaInteropAliasesAttributeCtor;

	const string TypeMapAssemblyTargetAttributeTypeName = "System.Runtime.InteropServices.TypeMapAssemblyTargetAttribute`1";
	MethodReference TypeMapAssemblyTargetAttributeCtor;

	const string JavaTypeMapUniverseTypeName = "Java.Lang.Object";
	TypeReference JavaTypeMapUniverseType { get; set; }

	const string InvokerUniverseTypeName = "Java.Interop.InvokerUniverse";
	TypeReference InvokerUniverseType { get; set; }
	MethodReference InvokerTypeMapAssociationAttributeCtor;

	TypeReference SystemTypeType { get; set; }
	TypeReference SystemStringType { get; set; }
	TypeReference SystemExceptionType { get; set; }
	TypeReference SystemIntPtrType { get; set; }

	// UCO wrapper generation imports
	MethodReference UnmanagedCallersOnlyAttributeCtor { get; set; }
	MethodReference WaitForBridgeProcessingMethod { get; set; }
	MethodReference UnhandledExceptionMethod { get; set; }

	AssemblyDefinition AssemblyToInjectTypeMap { get; set; }
	AssemblyDefinition MonoAndroidAssembly { get; set; }

	/// <summary>
	/// Generates the MethodReference for the given TypeMap attribute constructor,
	/// adding the necessary TypeRefs into the given assembly.
	/// </summary>
	void GetTypeMapAttributeReferences (
		string attributeTypeName,
		Func<MethodDefinition, bool> ctorSelector,
		AssemblyDefinition addReferencesTo,
		TypeReference typeMapUniverse,
		out MethodReference ctor)
	{
		var typeMapAttributeDefinition = Context.GetType (attributeTypeName);
		var attributeType = addReferencesTo.MainModule.ImportReference (typeMapAttributeDefinition.MakeGenericInstanceType (typeMapUniverse));

		var typeMapAttributeCtorDefinition = typeMapAttributeDefinition.Methods
			.FirstOrDefault (ctorSelector) ?? throw new InvalidOperationException ($"Couldn't find {attributeTypeName}..ctor()");
		var typeMapAttributeCtor = new MethodReference (
			typeMapAttributeCtorDefinition.Name,
			typeMapAttributeCtorDefinition.ReturnType,
			attributeType) {
			HasThis = typeMapAttributeCtorDefinition.HasThis,
			ExplicitThis = typeMapAttributeCtorDefinition.ExplicitThis,
			CallingConvention = typeMapAttributeCtorDefinition.CallingConvention,
		};
		foreach (var param in typeMapAttributeCtorDefinition.Parameters) {
			typeMapAttributeCtor.Parameters.Add (new ParameterDefinition (
				param.Name,
				param.Attributes,
				addReferencesTo.MainModule.ImportReference (param.ParameterType)));
		}
		ctor = addReferencesTo.MainModule.ImportReference (typeMapAttributeCtor);
	}

	protected override void Process ()
	{
		File.WriteAllText ("/tmp/linker-process.txt", $"Process started at {DateTime.Now}\n");
		try {
		// Context.LogMessage (MessageContainer.CreateInfoMessage ("GenerateTypeMapAttributesStep running..."));
		var javaTypeMapUniverseTypeDefinition = Context.GetType (JavaTypeMapUniverseTypeName);
		MonoAndroidAssembly = javaTypeMapUniverseTypeDefinition.Module.Assembly;

		// Try to find the entry assembly to inject types into
		// This avoids circular dependencies when proxies need to reference user types
		// 1. Try internal Linker API via reflection
		var getEntryPoint = Context.Annotations.GetType ().GetMethod ("GetAction", new Type[] { typeof (MethodDefinition) }) == null ? 
			Context.Annotations.GetType ().GetMethod ("GetEntryPointAssembly") : null;
		
		if (getEntryPoint != null)
			AssemblyToInjectTypeMap = getEntryPoint.Invoke (Context.Annotations, null) as AssemblyDefinition;

		// 2. Try to find assembly with EntryPoint
		// if (AssemblyToInjectTypeMap == null) {
		// 	foreach (var asm in Context.GetAssemblies ()) {
		// 		if (asm.EntryPoint != null) {
		// 			AssemblyToInjectTypeMap = asm;
		// 			break;
		// 		}
		// 	}
		// }

		// 3. Fallback to Mono.Android (will fail for user types if they need UCO wrappers)
		if (AssemblyToInjectTypeMap == null) {
			// Context.LogMessage (MessageContainer.CreateInfoMessage ("Could not find EntryPoint assembly, falling back to Mono.Android"));
			AssemblyToInjectTypeMap = MonoAndroidAssembly;
		} else {
			// Context.LogMessage (MessageContainer.CreateInfoMessage ($"Injecting TypeMap into entry assembly: {AssemblyToInjectTypeMap.Name}"));
		}

		JavaTypeMapUniverseType = AssemblyToInjectTypeMap.MainModule.ImportReference (javaTypeMapUniverseTypeDefinition);

		var invokerUniverseTypeDefinition = Context.GetType (InvokerUniverseTypeName);
		InvokerUniverseType = AssemblyToInjectTypeMap.MainModule.ImportReference (invokerUniverseTypeDefinition);

		GetTypeMapAttributeReferences (TypeMapAttributeTypeName,
			m => m.IsConstructor
				&& m.Parameters is [
				{ ParameterType.FullName: "System.String" },
				{ ParameterType.FullName: "System.Type" },
				{ ParameterType.FullName: "System.Type" }],
			AssemblyToInjectTypeMap,
			JavaTypeMapUniverseType,
			out TypeMapAttributeCtor);

		GetTypeMapAttributeReferences (TypeMapAssociationAttributeTypeName,
			m => m.IsConstructor
				&& m.Parameters is [
				{ ParameterType.FullName: "System.Type" },
				{ ParameterType.FullName: "System.Type" }],
			AssemblyToInjectTypeMap,
			JavaTypeMapUniverseType,
			out TypeMapAssociationAttributeCtor);

		// TypeMapAssociation<InvokerUniverse> for interface-to-invoker mappings
		GetTypeMapAttributeReferences (TypeMapAssociationAttributeTypeName,
			m => m.IsConstructor
				&& m.Parameters is [
				{ ParameterType.FullName: "System.Type" },
				{ ParameterType.FullName: "System.Type" }],
			AssemblyToInjectTypeMap,
			InvokerUniverseType,
			out InvokerTypeMapAssociationAttributeCtor);

		GetTypeMapAttributeReferences (TypeMapAssemblyTargetAttributeTypeName,
			m => m.IsConstructor
				&& m.Parameters is [{ ParameterType.FullName: "System.String" }],
			MonoAndroidAssembly,
			JavaTypeMapUniverseType,
			out TypeMapAssemblyTargetAttributeCtor);

		var typeMapProxyAttrTypeDef = Context.GetType (TypeMapProxyAttributeTypeName);
		var typeMapProxyAttribute = AssemblyToInjectTypeMap.MainModule.ImportReference (typeMapProxyAttrTypeDef);
		var typeMapProxyAttrCtor = typeMapProxyAttrTypeDef.Methods.Single (m => m.IsConstructor && !m.IsStatic);
		TypeMapProxyAttributeCtor = AssemblyToInjectTypeMap.MainModule.ImportReference (typeMapProxyAttrCtor);

		var javaPeerProxyTypeDef = Context.GetType (JavaPeerProxyTypeName);
		JavaPeerProxyType = AssemblyToInjectTypeMap.MainModule.ImportReference (javaPeerProxyTypeDef);
		var javaPeerProxyDefaultCtorDef = javaPeerProxyTypeDef.Methods.Single (m => m.IsConstructor && !m.IsStatic && !m.HasParameters);
		JavaPeerProxyDefaultCtor = AssemblyToInjectTypeMap.MainModule.ImportReference (javaPeerProxyDefaultCtorDef);

		var javaInteropAliasesAttrTypeDef = Context.GetType (JavaInteropAliasesAttributeTypeName);
		var javaInteropAliasesAttrCtor = javaInteropAliasesAttrTypeDef.Methods.Single (m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 1);
		JavaInteropAliasesAttributeCtor = AssemblyToInjectTypeMap.MainModule.ImportReference (javaInteropAliasesAttrCtor);

		SystemTypeType = AssemblyToInjectTypeMap.MainModule.ImportReference (Context.GetType ("System.Type"));
		SystemStringType = AssemblyToInjectTypeMap.MainModule.ImportReference (Context.GetType ("System.String"));
		SystemExceptionType = AssemblyToInjectTypeMap.MainModule.ImportReference (Context.GetType ("System.Exception"));
		SystemIntPtrType = AssemblyToInjectTypeMap.MainModule.ImportReference (Context.GetType ("System.IntPtr"));

		// Initialize UCO wrapper generation imports
		InitializeUcoImports ();

		base.Process ();
		} catch (Exception ex) {
			throw new InvalidOperationException ($"GenerateTypeMapAttributesStep crashed: {ex}");
		}
	}

	/// <summary>
	/// Initialize imports needed for generating [UnmanagedCallersOnly] wrapper methods.
	/// </summary>
	void InitializeUcoImports ()
	{
		// Find UnmanagedCallersOnlyAttribute constructor
		var ucoAttrTypeDef = Context.GetType ("System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute");
		var ucoAttrCtor = ucoAttrTypeDef.Methods.FirstOrDefault (m => m.IsConstructor && !m.IsStatic && !m.HasParameters)
			?? throw new InvalidOperationException ("Could not find UnmanagedCallersOnlyAttribute constructor");
		UnmanagedCallersOnlyAttributeCtor = AssemblyToInjectTypeMap.MainModule.ImportReference (ucoAttrCtor);

		// Find AndroidRuntimeInternal.WaitForBridgeProcessing
		var runtimeInternalTypeDef = MonoAndroidAssembly.MainModule.Types
			.FirstOrDefault (t => t.FullName == "Android.Runtime.AndroidRuntimeInternal");
		if (runtimeInternalTypeDef != null) {
			var waitMethod = runtimeInternalTypeDef.Methods.FirstOrDefault (m => m.Name == "WaitForBridgeProcessing");
			if (waitMethod != null) {
				WaitForBridgeProcessingMethod = AssemblyToInjectTypeMap.MainModule.ImportReference (waitMethod);
			}
		}

		// Find AndroidEnvironmentInternal.UnhandledException
		var envInternalTypeDef = MonoAndroidAssembly.MainModule.Types
			.FirstOrDefault (t => t.FullName == "Android.Runtime.AndroidEnvironmentInternal");
		if (envInternalTypeDef != null) {
			var unhandledMethod = envInternalTypeDef.Methods.FirstOrDefault (m => m.Name == "UnhandledException");
			if (unhandledMethod != null) {
				UnhandledExceptionMethod = AssemblyToInjectTypeMap.MainModule.ImportReference (unhandledMethod);
			}
		}

		if (WaitForBridgeProcessingMethod == null) {
			// Context.LogMessage (MessageContainer.CreateInfoMessage (
			// 	"Could not find AndroidRuntimeInternal.WaitForBridgeProcessing - UCO wrappers will be simplified"));
		}
		if (UnhandledExceptionMethod == null) {
			// Context.LogMessage (MessageContainer.CreateInfoMessage (
			// 	"Could not find AndroidEnvironmentInternal.UnhandledException - UCO wrappers will be simplified"));
		}
	}


	protected override void ProcessAssembly (AssemblyDefinition assembly)
	{
		foreach (var type in assembly.MainModule.Types) {
			ProcessType (assembly, type);
		}
	}

	// Need to find all possible mappings and pick the best before emitting
	// Maps Java name -> list of .NET types (for aliasing when multiple types share the same Java name)
	Dictionary<string, List<TypeDefinition>> externalMappings = new ();
	// Maps target type -> proxy attribute type (proxy will be applied to target type as custom attribute)
	Dictionary<TypeDefinition, TypeDefinition> proxyMappings = new ();
	// list of proxy types to inject into AssemblyToInjectInto in EndProcess
	List<TypeDefinition> typesToInject = new ();
	// Maps interfaces/abstract types to their Invoker types for TypeMapAssociation<InvokerUniverse>
	Dictionary<TypeDefinition, TypeDefinition> invokerMappings = new ();
	// Cache of all types in each module for quick lookup
	Dictionary<ModuleDefinition, Dictionary<string, TypeDefinition>> moduleTypesCache = new ();
	// Maps target type -> list of marshal method info for GetFunctionPointer generation
	Dictionary<TypeDefinition, List<MarshalMethodInfo>> marshalMethodMappings = new ();

	/// <summary>
	/// Information about a marshal method that can be called from native code via GetFunctionPointer.
	/// </summary>
	class MarshalMethodInfo
	{
		public string JniName { get; }
		public string JniSignature { get; }
		public MethodDefinition NativeCallback { get; }
		public MethodDefinition? RegisteredMethod { get; }
		public MethodDefinition? UcoWrapper { get; set; }

		public MarshalMethodInfo (string jniName, string jniSignature, MethodDefinition nativeCallback, MethodDefinition? registeredMethod)
		{
			JniName = jniName;
			JniSignature = jniSignature;
			NativeCallback = nativeCallback;
			RegisteredMethod = registeredMethod;
		}
	}

	/// <summary>
	/// Collects marshal methods from a type that have [Register] attributes with connector methods.
	/// These are methods that can be converted to [UnmanagedCallersOnly] callbacks.
	/// </summary>
	List<MarshalMethodInfo> CollectMarshalMethods (TypeDefinition type)
	{
		var methods = new List<MarshalMethodInfo> ();
		var seen = new HashSet<string> (); // Track JniName+JniSignature to dedupe

		foreach (var method in type.Methods) {
			if (!CecilExtensions.HasMethodRegistrationAttributes (method)) {
				continue;
			}

			foreach (var attr in CecilExtensions.GetMethodRegistrationAttributes (method)) {
				// Must have JNI name, signature, and connector (3 arguments)
				if (string.IsNullOrEmpty (attr.Name) || string.IsNullOrEmpty (attr.Signature)) {
					continue;
				}

				string key = $"{attr.Name}{attr.Signature}";
				if (seen.Contains (key)) {
					continue; // Skip duplicates
				}

				// Find the native callback method (n_* method) based on connector naming pattern
				string? nativeCallbackName = GetNativeCallbackName (attr.Connector, attr.Name, attr.Signature);
				MethodDefinition? nativeCallback = nativeCallbackName != null
					? type.Methods.FirstOrDefault (m => m.Name == nativeCallbackName && m.IsStatic)
					: null;

				if (nativeCallback == null) {
					// Context.LogMessage (MessageContainer.CreateInfoMessage (
						// $"Could not find native callback '{nativeCallbackName}' for method '{method.FullName}'"));
					continue;
				}

				seen.Add (key);
				methods.Add (new MarshalMethodInfo (attr.Name, attr.Signature, nativeCallback, method));
				// Context.LogMessage (MessageContainer.CreateInfoMessage (
					// $"Found marshal method: {type.FullName}.{nativeCallback.Name} -> {attr.Name}{attr.Signature}"));
			}
		}

		// Also collect exported constructors
		foreach (var ctor in type.Methods.Where (m => m.IsConstructor && !m.IsStatic)) {
			foreach (var attr in CecilExtensions.GetMethodRegistrationAttributes (ctor)) {
				if (string.IsNullOrEmpty (attr.Signature)) {
					continue;
				}

				string key = $"<init>{attr.Signature}";
				if (seen.Contains (key)) {
					continue; // Skip duplicates
				}

				// For constructors, look for n_<init> or activation patterns
				var nativeCallback = type.Methods.FirstOrDefault (m =>
					m.IsStatic && (m.Name == "n_<init>" || m.Name.StartsWith ("n_", StringComparison.Ordinal)));

				if (nativeCallback != null) {
					seen.Add (key);
					methods.Add (new MarshalMethodInfo ("<init>", attr.Signature, nativeCallback, ctor));
					// Context.LogMessage (MessageContainer.CreateInfoMessage (
						// $"Found marshal constructor: {type.FullName}.{nativeCallback.Name}"));
				}
			}
		}

		return methods;
	}

	/// <summary>
	/// Extracts the native callback method name from a connector string.
	/// Connector format is typically "GetMethodName_Handler" and the callback is "n_MethodName".
	/// </summary>
	static string? GetNativeCallbackName (string? connector, string jniName, string jniSignature)
	{
		if (string.IsNullOrEmpty (connector)) {
			return null;
		}

		// Standard pattern: connector is "GetOnCreate_Landroid_os_Bundle_Handler" -> callback is "n_onCreate"
		// Try to extract from connector pattern
		if (connector!.StartsWith ("Get", StringComparison.Ordinal) && connector.EndsWith ("Handler", StringComparison.Ordinal)) {
			// Extract method name part: "GetOnCreate_Landroid_os_Bundle_Handler" -> find the method name
			// The callback is typically "n_" + jniName
			return $"n_{jniName}";
		}

		// Fallback: just prepend n_
		return $"n_{jniName}";
	}

	/// <summary>
	/// Iterates through all types to find types that map to/from java types, and stores
	/// them for modifying the assemblies during EndProcess
	/// </summary>
	private void ProcessType (AssemblyDefinition assembly, TypeDefinition type)
	{
		if (type.HasJavaPeer (Context)) {
			string javaName = JavaNativeTypeManager.ToJniName (type, Context);
			if (!externalMappings.TryGetValue (javaName, out var typeList)) {
				typeList = new List<TypeDefinition> ();
				externalMappings.Add (javaName, typeList);
			}
			typeList.Add (type);

			// Collect marshal methods for this type
			var marshalMethods = CollectMarshalMethods (type);
			if (marshalMethods.Count > 0) {
				marshalMethodMappings [type] = marshalMethods;
			}

			Context.LogMessage (MessageContainer.CreateInfoMessage ($"DEBUG: Type '{type.FullName}' has peer '{javaName}', {marshalMethods.Count} marshal methods"));
			var proxyType = GenerateTypeMapProxyType (javaName, type, marshalMethods);
			typesToInject.Add (proxyType);
			proxyMappings.Add (type, proxyType);

			// For interfaces and abstract types, find their Invoker type
			if (type.IsInterface || type.IsAbstract) {
				var invokerType = GetInvokerType (type);
				if (invokerType != null) {
					invokerMappings [type] = invokerType;
					// Context.LogMessage (MessageContainer.CreateInfoMessage ($"Found invoker '{invokerType.FullName}' for type '{type.FullName}'"));
				}
			}
		} else {
			// Context.LogMessage (MessageContainer.CreateInfoMessage ($"Type '{type.FullName}' has no peer"));
		}

		if (!type.HasNestedTypes)
			return;

		foreach (TypeDefinition nested in type.NestedTypes)
			ProcessType (assembly, nested);
	}

	/// <summary>
	/// Finds the Invoker type for an interface or abstract type.
	/// Follows the naming convention: IMyInterface -> IMyInterfaceInvoker, MyAbstractClass -> MyAbstractClassInvoker
	/// </summary>
	TypeDefinition? GetInvokerType (TypeDefinition type)
	{
		const string suffix = "Invoker";
		string fullname = type.FullName;

		if (type.HasGenericParameters) {
			var pos = fullname.IndexOf ('`');
			if (pos == -1)
				return null;

			fullname = fullname.Substring (0, pos) + suffix + fullname.Substring (pos);
		} else {
			fullname = fullname + suffix;
		}

		return FindTypeInModule (type.Module, fullname);
	}

	/// <summary>
	/// Finds a type by its full name in the given module, using a cached lookup.
	/// </summary>
	TypeDefinition? FindTypeInModule (ModuleDefinition module, string fullname)
	{
		if (!moduleTypesCache.TryGetValue (module, out var types)) {
			types = GetAllTypesInModule (module);
			moduleTypesCache [module] = types;
		}

		types.TryGetValue (fullname, out var result);
		return result;
	}

	/// <summary>
	/// Gets all types in a module, including nested types, for quick lookup.
	/// </summary>
	static Dictionary<string, TypeDefinition> GetAllTypesInModule (ModuleDefinition module)
	{
		var types = module.Types.ToDictionary (p => p.FullName);

		foreach (var t in module.Types)
			AddNestedTypes (types, t);

		return types;
	}

	static void AddNestedTypes (Dictionary<string, TypeDefinition> types, TypeDefinition type)
	{
		if (!type.HasNestedTypes)
			return;

		foreach (var t in type.NestedTypes) {
			types [t.FullName] = t;
			AddNestedTypes (types, t);
		}
	}

	protected override void EndProcess ()
	{
		File.WriteAllText ("/tmp/linker-endprocess.txt", $"EndProcess started at {DateTime.Now}\n");
		try {
		// NOTE: We override the entry_assembly so that the TypeMapHandler in illink can have a starting point for TypeMapTargetAssemblies.
		// This is critical because Mono.Android should be the entrypoint assembly so that we can call Assembly.SetEntryAssembly()
		// during application initialization. Without this override, the TypeMapHandler would not be able to correctly identify which
		// assemblies need TypeMap attributes.
		// TODO:
		// - Add support for "EntryPointAssembly"s that don't have a .entrypoint or Main() method
		// - Use MSBuild logic to set the EntryPointAssembly to Mono.Android
		Context.Annotations.GetType ().GetField ("entry_assembly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue (Context.Annotations, MonoAndroidAssembly);
		foreach (var type in typesToInject) {
			AssemblyToInjectTypeMap.MainModule.Types.Add (type);
			Debug.Assert (type.Module.Assembly is not null);
		}

		// Generate TypeMap attributes for external mappings
		// When multiple types share the same Java name, generate an alias type
		foreach (var mapping in externalMappings) {
			var javaName = mapping.Key;
			var types = mapping.Value;

			if (types.Count == 1) {
				// Single type - simple mapping
				var attr = GenerateTypeMapAttribute (types [0], javaName);
				// Context.LogMessage (MessageContainer.CreateInfoMessage ($"Injecting [{attr.AttributeType.FullName}({string.Join (", ", attr.ConstructorArguments.Select (caa => caa.ToString ()))})] into {AssemblyToInjectTypeMap.Name}"));
				AssemblyToInjectTypeMap.CustomAttributes.Add (attr);
			} else {
				// Multiple types - generate alias type and indexed mappings
				var aliasKeys = new string [types.Count];
				for (int i = 0; i < types.Count; i++) {
					aliasKeys [i] = $"{javaName}[{i}]";

					// Generate TypeMap for each aliased type: "javaName[i]" -> type
					var attr = GenerateTypeMapAttribute (types [i], aliasKeys [i]);
					// Context.LogMessage (MessageContainer.CreateInfoMessage ($"Injecting aliased [{attr.AttributeType.FullName}({string.Join (", ", attr.ConstructorArguments.Select (caa => caa.ToString ()))})] into {AssemblyToInjectTypeMap.Name}"));
					AssemblyToInjectTypeMap.CustomAttributes.Add (attr);
				}

				// Generate the alias type with [JavaInteropAliases("javaName[0]", "javaName[1]", ...)]
				var aliasType = GenerateAliasType (javaName, aliasKeys);
				AssemblyToInjectTypeMap.MainModule.Types.Add (aliasType);

				// Generate TypeMap for the main Java name -> alias type
				var mainAttr = GenerateTypeMapAttribute (aliasType, javaName);
				// Context.LogMessage (MessageContainer.CreateInfoMessage ($"Injecting alias [{mainAttr.AttributeType.FullName}({string.Join (", ", mainAttr.ConstructorArguments.Select (caa => caa.ToString ()))})] into {AssemblyToInjectTypeMap.Name}"));
				AssemblyToInjectTypeMap.CustomAttributes.Add (mainAttr);
			}
		}

		// Apply proxy attributes directly to target types (AOT-safe: uses GetCustomAttribute instead of Activator.CreateInstance)
		foreach (var mapping in proxyMappings) {
			ApplyProxyAttributeToTargetType (mapping.Key, mapping.Value);
		}
		// Generate TypeMapAssociation<InvokerUniverse> for interface-to-invoker mappings
		foreach (var mapping in invokerMappings) {
			var attr = GenerateInvokerTypeMapAssociationAttribute (mapping.Key, mapping.Value);
			// Context.LogMessage (MessageContainer.CreateInfoMessage ($"Injecting [{attr.AttributeType.FullName}({string.Join (", ", attr.ConstructorArguments.Select (caa => caa.ToString ()))})] into {AssemblyToInjectTypeMap.Name}"));
			AssemblyToInjectTypeMap.CustomAttributes.Add (attr);
		}

		// JNIEnvInit sets Mono.Android as the entrypoint assembly. Forward the typemap logic to the user/custom assembly;
		CustomAttribute targetAssembly = new (TypeMapAssemblyTargetAttributeCtor);
		targetAssembly.ConstructorArguments.Add (new (SystemStringType, AssemblyToInjectTypeMap.Name.FullName));
		MonoAndroidAssembly.CustomAttributes.Add (targetAssembly);

		// Force the Linker to write the modified Mono.Android assembly
		Context.Annotations.SetAction (MonoAndroidAssembly, AssemblyAction.Save);

		// Generate JCW (Java Callable Wrappers) and LLVM IR files for marshal methods
		GenerateJcwAndLlvmIrFiles ();
		} catch (Exception ex) {
			throw new InvalidOperationException ($"GenerateTypeMapAttributesStep.EndProcess crashed: {ex}");
		}
	}

	/// <summary>
	/// Generates Java Callable Wrapper (.java) and LLVM IR (.ll) files for types with marshal methods.
	/// </summary>
	void GenerateJcwAndLlvmIrFiles ()
	{
		// Get output paths from custom data
		if (!Context.TryGetCustomData ("JavaOutputPath", out string? javaOutputPath) ||
		    !Context.TryGetCustomData ("LlvmIrOutputPath", out string? llvmIrOutputPath)) {
			Context.LogMessage (MessageContainer.CreateInfoMessage (
				"JavaOutputPath or LlvmIrOutputPath not set, skipping JCW/LLVM IR generation"));
			File.WriteAllText ("/tmp/linker-debug.txt", "JavaOutputPath or LlvmIrOutputPath not set");
			return;
		}

		File.WriteAllText ("/tmp/linker-debug.txt", $"JavaOutputPath={javaOutputPath}, LlvmIrOutputPath={llvmIrOutputPath}, marshalMethodMappings.Count={marshalMethodMappings.Count}\n");

		Context.LogMessage (MessageContainer.CreateInfoMessage (
			$"DEBUG: JavaOutputPath={javaOutputPath}, LlvmIrOutputPath={llvmIrOutputPath}"));

		// Normalize paths for the current platform
		javaOutputPath = javaOutputPath.Replace ('\\', Path.DirectorySeparatorChar);
		llvmIrOutputPath = llvmIrOutputPath.Replace ('\\', Path.DirectorySeparatorChar);

		Context.TryGetCustomData ("TargetArch", out string? targetArch);
		targetArch ??= "unknown";

		// Output JCW files to the staging directory - these will be copied after GenerateJavaStubs
		// to overwrite the old-style JCW that calls TypeManager.Activate
		string typeMapJcwOutputPath = javaOutputPath;

		Context.LogMessage (MessageContainer.CreateInfoMessage (
			$"Generating JCW files to {typeMapJcwOutputPath}, LLVM IR to {llvmIrOutputPath}, marshalMethodMappings.Count={marshalMethodMappings.Count}"));

		File.AppendAllText ("/tmp/linker-debug.txt", $"Generating JCW to {typeMapJcwOutputPath}, mappings={marshalMethodMappings.Count}\n");

		// Generate JCW Java and LLVM IR files for each type with marshal methods
		foreach (var kvp in marshalMethodMappings) {
			var targetType = kvp.Key;
			var marshalMethods = kvp.Value;

			if (marshalMethods.Count == 0) {
				continue;
			}

			string jniTypeName = JavaNativeTypeManager.ToJniName (targetType, Context);

			File.AppendAllText ("/tmp/linker-debug.txt", $"  {targetType.FullName} -> {jniTypeName} with {marshalMethods.Count} methods\n");

			Context.LogMessage (MessageContainer.CreateInfoMessage (
				$"DEBUG: Generating JCW for {targetType.FullName} -> {jniTypeName} with {marshalMethods.Count} methods"));

			// Generate JCW Java file - replaces the existing JCW generator output
			GenerateJcwJavaFile (typeMapJcwOutputPath, targetType, jniTypeName, marshalMethods);

			// Generate LLVM IR file for native JNI method stubs
			GenerateLlvmIrFile (llvmIrOutputPath, targetArch, targetType, jniTypeName, marshalMethods);
		}

		// Generate the initialization file that defines get_function_pointer
		GenerateLlvmIrInitFile (llvmIrOutputPath, targetArch);
	}

	/// <summary>
	/// Generates the marshal_methods_init.ll file that defines the get_function_pointer global.
	/// </summary>
	void GenerateLlvmIrInitFile (string outputPath, string targetArch)
	{
		Directory.CreateDirectory (outputPath);
		string llFilePath = Path.Combine (outputPath, "marshal_methods_init.ll");

		using var writer = new StreamWriter (llFilePath);

		writer.Write ("""
			; ModuleID = 'marshal_methods_init.ll'
			source_filename = "marshal_methods_init.ll"
			target datalayout = "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128"
			target triple = "aarch64-unknown-linux-android21"

			; Global get_function_pointer callback - set directly from JNIEnvInit.Initialize
			@get_function_pointer = local_unnamed_addr global ptr null, align 8

			""".Replace ("\t", ""));
	}

	/// <summary>
	/// Generates a Java Callable Wrapper file for the given type.
	/// This generates JCW files that are independent of the existing JCW generator and use
	/// native method resolution via get_function_pointer for all callbacks including constructors.
	/// </summary>
	void GenerateJcwJavaFile (string outputPath, TypeDefinition type, string jniTypeName, List<MarshalMethodInfo> marshalMethods)
	{
		// Convert JNI type name to Java package and class name
		// e.g., "helloworld/MainActivity" -> package "helloworld", class "MainActivity"
		int lastSlash = jniTypeName.LastIndexOf ('/');
		string package = lastSlash > 0 ? jniTypeName.Substring (0, lastSlash).Replace ('/', '.') : "";
		string className = lastSlash > 0 ? jniTypeName.Substring (lastSlash + 1) : jniTypeName;
		className = className.Replace ('$', '_'); // Handle nested classes

		// Get the Java base class name from the .NET base type
		string baseClassName = "java.lang.Object";
		if (type.BaseType != null) {
			var baseType = type.BaseType.Resolve ();
			if (baseType != null) {
				string? baseJniName = JavaNativeTypeManager.ToJniName (baseType, Context);
				if (!string.IsNullOrEmpty (baseJniName)) {
					baseClassName = baseJniName.Replace ('/', '.');
				}
			}
		}

		// Create directory structure
		string packageDir = Path.Combine (outputPath, package.Replace ('.', Path.DirectorySeparatorChar));
		Directory.CreateDirectory (packageDir);

		string javaFilePath = Path.Combine (packageDir, className + ".java");

		using var writer = new StreamWriter (javaFilePath);

		// Build method declarations - both public wrappers and private native methods
		var publicMethods = new StringBuilder ();
		var nativeMethods = new StringBuilder ();

		foreach (var method in marshalMethods) {
			string returnType = JniSignatureToJavaType (method.JniSignature, returnOnly: true);
			string parameters = JniSignatureToJavaParameters (method.JniSignature);
			string parameterNames = JniSignatureToJavaParameterNames (method.JniSignature);

			// Replace <init> with _ctor for valid Java identifier
			string javaMethodName = method.JniName.Replace ("<init>", "_ctor").Replace ("<clinit>", "_cctor");

			// Skip constructor methods for now - they need special handling
			if (method.JniName == "<init>" || method.JniName == "<clinit>") {
				continue;
			}

			// Generate public wrapper method
			string returnStatement = returnType == "void" ? "" : "return ";
			publicMethods.AppendLine ($$"""
			    public {{returnType}} {{method.JniName}} ({{parameters}})
			    {
			        {{returnStatement}}n_{{javaMethodName}} ({{parameterNames}});
			    }

			""".Replace ("\t", ""));

			// Generate private native declaration
			nativeMethods.AppendLine ($"    private native {returnType} n_{javaMethodName} ({parameters});");
		}

		// Generate package declaration and class
		var sb = new StringBuilder ();

		if (!string.IsNullOrEmpty (package)) {
			sb.AppendLine ($"package {package};");
			sb.AppendLine ();
		}

		sb.AppendLine ($$"""
			public class {{className}}
			    extends {{baseClassName}}
			    implements mono.android.IGCUserPeer
			{
			    // Default constructor - calls native activation via get_function_pointer
			    public {{className}} ()
			    {
			        super ();
			    }

			{{publicMethods}}
			{{nativeMethods}}
			    // IGCUserPeer implementation for preventing premature GC
			    private java.util.ArrayList refList;
			    public void monodroidAddReference (java.lang.Object obj)
			    {
			        if (refList == null)
			            refList = new java.util.ArrayList ();
			        refList.add (obj);
			    }

			    public void monodroidClearReferences ()
			    {
			        if (refList != null)
			            refList.clear ();
			    }
			}
			""".Replace ("\t", ""));

		writer.Write (sb.ToString ());
	}

	/// <summary>
	/// Converts JNI signature to Java parameter names only (e.g., "p0, p1, p2").
	/// </summary>
	static string JniSignatureToJavaParameterNames (string jniSignature)
	{
		var result = new StringBuilder ();
		int paramIndex = 0;
		int i = 1; // Skip opening '('

		while (i < jniSignature.Length && jniSignature [i] != ')') {
			if (paramIndex > 0) {
				result.Append (", ");
			}
			result.Append ($"p{paramIndex}");
			paramIndex++;

			// Skip the type descriptor
			char c = jniSignature [i];
			switch (c) {
				case 'L':
					while (i < jniSignature.Length && jniSignature [i] != ';') i++;
					i++; // Skip ';'
					break;
				case '[':
					while (i < jniSignature.Length && jniSignature [i] == '[') i++;
					if (i < jniSignature.Length && jniSignature [i] == 'L') {
						while (i < jniSignature.Length && jniSignature [i] != ';') i++;
						i++; // Skip ';'
					} else {
						i++; // Skip primitive type
					}
					break;
				default:
					i++;
					break;
			}
		}

		return result.ToString ();
	}

	/// <summary>
	/// Generates an LLVM IR file for the given type's marshal methods.
	/// Each native JNI method stub calls get_function_pointer to resolve the UCO wrapper,
	/// caches it, and forwards the call to the managed method.
	/// </summary>
	void GenerateLlvmIrFile (string outputPath, string targetArch, TypeDefinition type, string jniTypeName, List<MarshalMethodInfo> marshalMethods)
	{
		// Create output directory
		Directory.CreateDirectory (outputPath);

		// Sanitize type name for filename
		string sanitizedName = type.FullName.Replace ('.', '_').Replace ('/', '_').Replace ('+', '_');
		string llFilePath = Path.Combine (outputPath, $"marshal_methods_{sanitizedName}.ll");

		using var writer = new StreamWriter (llFilePath);

		// LLVM IR header
		writer.Write ($"""
			; ModuleID = 'marshal_methods_{sanitizedName}.ll'
			source_filename = "marshal_methods_{sanitizedName}.ll"
			target datalayout = "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128"
			target triple = "aarch64-unknown-linux-android21"

			; External get_function_pointer callback - resolves UCO wrapper by class name and method index
			@get_function_pointer = external local_unnamed_addr global ptr, align 8

			; Cached function pointers for marshal methods

			""");

		// Class name constant (null-terminated string)
		byte[] classNameBytes = System.Text.Encoding.UTF8.GetBytes (jniTypeName);
		string classNameBytesEncoded = string.Join("", classNameBytes.Select(b => $"\\{b:X2}"));
		int classNameLength = classNameBytes.Length;

		// Cached function pointers for marshal methods
		var fnPointers = new StringBuilder ();
		for (int i = 0; i < marshalMethods.Count; i++) {
			fnPointers.AppendLine ($"@fn_ptr_{i} = internal unnamed_addr global ptr null, align 8");
		}

		writer.Write ($"""
			{fnPointers}
			; Class name for \"{jniTypeName}\" (length={classNameLength})
			@class_name = internal constant [{classNameLength} x i8] c\"{classNameBytesEncoded}\", align 1

			; JNI native method stubs
			""");

		for (int i = 0; i < marshalMethods.Count; i++) {
			var method = marshalMethods [i];
			string nativeSymbol = MakeJniNativeSymbol (jniTypeName, method.JniName, method.JniSignature);
			string llvmParams = JniSignatureToLlvmParams (method.JniSignature);
			string llvmArgs = JniSignatureToLlvmArgs (method.JniSignature);
			string llvmRetType = JniSignatureToLlvmReturnType (method.JniSignature);

			if (llvmRetType == "void") {
				writer.Write ($$"""

					; Method: {{method.JniName}}{{method.JniSignature}}
					define default {{llvmRetType}} @{{nativeSymbol}}(ptr %env, ptr %obj{{llvmParams}}) #0 {
					entry:
					  %cached_ptr = load ptr, ptr @fn_ptr_{{i}}, align 8
					  %is_null = icmp eq ptr %cached_ptr, null
					  br i1 %is_null, label %resolve, label %call

					resolve:
					  %get_fn = load ptr, ptr @get_function_pointer, align 8
					  call void %get_fn(ptr @class_name, i32 {{classNameLength}}, i32 {{i}}, ptr @fn_ptr_{{i}})
					  %resolved_ptr = load ptr, ptr @fn_ptr_{{i}}, align 8
					  br label %call

					call:
					  %fn = phi ptr [ %cached_ptr, %entry ], [ %resolved_ptr, %resolve ]
					  tail call void %fn(ptr %env, ptr %obj{{llvmArgs}})
					  ret void
					}

					""");
			} else {
				writer.Write ($$"""

					; Method: {{method.JniName}}{{method.JniSignature}}
					define default {{llvmRetType}} @{{nativeSymbol}}(ptr %env, ptr %obj{{llvmParams}}) #0 {
					entry:
					  %cached_ptr = load ptr, ptr @fn_ptr_{{i}}, align 8
					  %is_null = icmp eq ptr %cached_ptr, null
					  br i1 %is_null, label %resolve, label %call

					resolve:
					  %get_fn = load ptr, ptr @get_function_pointer, align 8
					  call void %get_fn(ptr @class_name, i32 {{classNameLength}}, i32 {{i}}, ptr @fn_ptr_{{i}})
					  %resolved_ptr = load ptr, ptr @fn_ptr_{{i}}, align 8
					  br label %call

					call:
					  %fn = phi ptr [ %cached_ptr, %entry ], [ %resolved_ptr, %resolve ]
					  %result = tail call {{llvmRetType}} %fn(ptr %env, ptr %obj{{llvmArgs}})
					  ret {{llvmRetType}} %result
					}

					""");
			}
		}

		writer.Write ("""

			; Function attributes
			attributes #0 = { mustprogress nofree norecurse nosync nounwind willreturn memory(argmem: read) uwtable }

			; Metadata
			!llvm.module.flags = !{!0}
			!0 = !{i32 1, !"wchar_size", i32 4}

			""");
	}

	/// <summary>
	/// Converts JNI signature parameters to LLVM IR argument references with types (e.g., ", ptr %p0, ptr %p1").
	/// </summary>
	static string JniSignatureToLlvmArgs (string jniSignature)
	{
		var args = new StringBuilder ();
		int paramIndex = 0;
		int i = 1; // Skip opening '('

		while (i < jniSignature.Length && jniSignature [i] != ')') {
			char c = jniSignature [i];

			// Determine the type
			string llvmType;
			switch (c) {
				case 'Z': llvmType = "i8"; i++; break;  // boolean
				case 'B': llvmType = "i8"; i++; break;  // byte
				case 'C': llvmType = "i16"; i++; break; // char
				case 'S': llvmType = "i16"; i++; break; // short
				case 'I': llvmType = "i32"; i++; break; // int
				case 'J': llvmType = "i64"; i++; break; // long
				case 'F': llvmType = "float"; i++; break;
				case 'D': llvmType = "double"; i++; break;
				case 'L':
					llvmType = "ptr";
					while (i < jniSignature.Length && jniSignature [i] != ';') i++;
					i++; // Skip ';'
					break;
				case '[':
					llvmType = "ptr";
					// Skip all array dimensions
					while (i < jniSignature.Length && jniSignature [i] == '[') i++;
					// Skip the element type
					if (i < jniSignature.Length) {
						if (jniSignature [i] == 'L') {
							while (i < jniSignature.Length && jniSignature [i] != ';') i++;
							i++; // Skip ';'
						} else {
							i++; // Skip primitive type
						}
					}
					break;
				default:
					llvmType = "ptr";
					i++;
					break;
			}

			args.Append (", ");
			args.Append (llvmType);
			args.Append (" %p");
			args.Append (paramIndex);
			paramIndex++;
		}

		return args.ToString ();
	}

	/// <summary>
	/// Converts JNI signature parameters to LLVM IR type list (e.g., ", ptr, ptr").
	/// </summary>
	static string JniSignatureToLlvmParamTypes (string jniSignature)
	{
		var types = new StringBuilder ();
		int i = 1; // Skip opening '('

		while (i < jniSignature.Length && jniSignature [i] != ')') {
			char c = jniSignature [i];
			string llvmType;

			switch (c) {
				case 'Z': llvmType = "i8"; i++; break;  // boolean
				case 'B': llvmType = "i8"; i++; break;  // byte
				case 'C': llvmType = "i16"; i++; break; // char
				case 'S': llvmType = "i16"; i++; break; // short
				case 'I': llvmType = "i32"; i++; break; // int
				case 'J': llvmType = "i64"; i++; break; // long
				case 'F': llvmType = "float"; i++; break;
				case 'D': llvmType = "double"; i++; break;
				case 'L':
					llvmType = "ptr";
					while (i < jniSignature.Length && jniSignature [i] != ';') i++;
					i++; // Skip ';'
					break;
				case '[':
					llvmType = "ptr";
					// Skip all array dimensions
					while (i < jniSignature.Length && jniSignature [i] == '[') i++;
					// Skip the element type
					if (i < jniSignature.Length) {
						if (jniSignature [i] == 'L') {
							while (i < jniSignature.Length && jniSignature [i] != ';') i++;
							i++; // Skip ';'
						} else {
							i++; // Skip primitive type
						}
					}
					break;
				default:
					llvmType = "ptr";
					i++;
					break;
			}

			types.Append (", ");
			types.Append (llvmType);
		}

		return types.ToString ();
	}

	/// <summary>
	/// Creates a JNI native symbol name from the type name, method name, and signature.
	/// For overloaded methods, the signature is mangled and appended to make the symbol unique.
	/// </summary>
	static string MakeJniNativeSymbol (string jniTypeName, string methodName, string jniSignature)
	{
		// Replace <init> with _ctor for valid JNI symbol
		string sanitizedMethodName = methodName.Replace ("<init>", "_ctor").Replace ("<clinit>", "_cctor");
		var sb = new StringBuilder ("Java_");
		sb.Append (MangleForJni (jniTypeName));
		sb.Append ('_');
		sb.Append (MangleForJni ($"n_{sanitizedMethodName}"));
		// Always append mangled signature to handle overloads
		sb.Append ("__");
		sb.Append (MangleJniSignature (jniSignature));
		return sb.ToString ();
	}

	/// <summary>
	/// Mangles a JNI signature for use in native symbol names.
	/// Converts (Ljava/lang/String;)V to Ljava_lang_String_2V
	/// </summary>
	static string MangleJniSignature (string signature)
	{
		var sb = new StringBuilder ();
		foreach (char c in signature) {
			if (c == ')')
				break; // Stop at closing parenthesis (return type is not part of signature in symbol name)
			
			switch (c) {
				case '(':
					// Skip opening parenthesis
					break;
				case '/':
					sb.Append ('_');
					break;
				case ';':
					sb.Append ("_2");
					break;
				case '[':
					sb.Append ("_3");
					break;
				default:
					sb.Append (c);
					break;
			}
		}
		return sb.ToString ();
	}

	/// <summary>
	/// Mangles a string for use in JNI native symbol names.
	/// </summary>
	static string MangleForJni (string name)
	{
		var sb = new StringBuilder (name.Length);
		foreach (char c in name) {
			switch (c) {
				case '/':
				case '.':
					sb.Append ('_');
					break;
				case '_':
					sb.Append ("_1");
					break;
				case ';':
					sb.Append ("_2");
					break;
				case '[':
					sb.Append ("_3");
					break;
				case '$':
					sb.Append ("_00024");
					break;
				default:
					sb.Append (c);
					break;
			}
		}
		return sb.ToString ();
	}

	/// <summary>
	/// Converts JNI signature return type to Java type.
	/// </summary>
	static string JniSignatureToJavaType (string signature, bool returnOnly)
	{
		int parenEnd = signature.LastIndexOf (')');
		if (parenEnd < 0) return "void";

		char returnChar = signature [parenEnd + 1];
		return returnChar switch {
			'V' => "void",
			'Z' => "boolean",
			'B' => "byte",
			'C' => "char",
			'S' => "short",
			'I' => "int",
			'J' => "long",
			'F' => "float",
			'D' => "double",
			'L' => "Object", // Mapping non-primitive types to Object is sufficient for native method resolution
			'[' => "Object[]", // Mapping arrays to Object[] is sufficient for native method resolution
			_ => "Object",
		};
	}

	/// <summary>
	/// Converts JNI signature parameters to Java parameter list.
	/// </summary>
	static string JniSignatureToJavaParameters (string signature)
	{
		// NOTE: Mapping parameters to generic Object types is sufficient for native method resolution.
		// The JNI runtime only needs the method signature to match at the native layer; the .java stubs
		// don't need exact type information for JNI resolution to work correctly.
		int parenStart = signature.IndexOf ('(');
		int parenEnd = signature.IndexOf (')');
		if (parenStart < 0 || parenEnd < 0 || parenEnd == parenStart + 1) {
			return "";
		}

		// Simplified parameter extraction
		string paramSig = signature.Substring (parenStart + 1, parenEnd - parenStart - 1);
		var @params = new List<string> ();
		int idx = 0;
		int paramNum = 0;

		while (idx < paramSig.Length) {
			char c = paramSig [idx];
			string type = c switch {
				'Z' => "boolean",
				'B' => "byte",
				'C' => "char",
				'S' => "short",
				'I' => "int",
				'J' => "long",
				'F' => "float",
				'D' => "double",
				'L' => "Object",
				'[' => "Object[]",
				_ => "Object",
			};

			if (c == 'L') {
				// Skip to semicolon
				while (idx < paramSig.Length && paramSig [idx] != ';') idx++;
			} else if (c == '[') {
				// Skip array and element type
				while (idx < paramSig.Length && paramSig [idx] == '[') idx++;
				if (idx < paramSig.Length && paramSig [idx] == 'L') {
					while (idx < paramSig.Length && paramSig [idx] != ';') idx++;
				}
			}

			@params.Add ($"{type} p{paramNum++}");
			idx++;
		}

		return string.Join (", ", @params);
	}

	/// <summary>
	/// Converts JNI signature to LLVM IR parameter types.
	/// </summary>
	static string JniSignatureToLlvmParams (string signature)
	{
		int parenStart = signature.IndexOf ('(');
		int parenEnd = signature.IndexOf (')');
		if (parenStart < 0 || parenEnd < 0 || parenEnd == parenStart + 1) {
			return "";
		}

		string paramSig = signature.Substring (parenStart + 1, parenEnd - parenStart - 1);
		var @params = new List<string> ();
		int idx = 0;
		int paramNum = 0;

		while (idx < paramSig.Length) {
			char c = paramSig [idx];
			string type = c switch {
				'Z' => "i8",
				'B' => "i8",
				'C' => "i16",
				'S' => "i16",
				'I' => "i32",
				'J' => "i64",
				'F' => "float",
				'D' => "double",
				'L' => "ptr",
				'[' => "ptr",
				_ => "ptr",
			};

			if (c == 'L') {
				while (idx < paramSig.Length && paramSig [idx] != ';') idx++;
				idx++; // Skip ';'
			} else if (c == '[') {
				// Skip all array dimensions
				while (idx < paramSig.Length && paramSig [idx] == '[') idx++;
				// Skip the element type
				if (idx < paramSig.Length) {
					if (paramSig [idx] == 'L') {
						while (idx < paramSig.Length && paramSig [idx] != ';') idx++;
						idx++; // Skip ';'
					} else {
						idx++; // Skip primitive type
					}
				}
			} else {
				idx++;
			}

			@params.Add ($", {type} %p{paramNum++}");
		}

		return string.Concat (@params);
	}

	/// <summary>
	/// Converts JNI signature return type to LLVM IR type.
	/// </summary>
	static string JniSignatureToLlvmReturnType (string signature)
	{
		int parenEnd = signature.LastIndexOf (')');
		if (parenEnd < 0 || parenEnd + 1 >= signature.Length) return "void";

		char returnChar = signature [parenEnd + 1];
		return returnChar switch {
			'V' => "void",
			'Z' => "i8",
			'B' => "i8",
			'C' => "i16",
			'S' => "i16",
			'I' => "i32",
			'J' => "i64",
			'F' => "float",
			'D' => "double",
			'L' => "ptr",
			'[' => "ptr",
			_ => "ptr",
		};
	}

	/// <summary>
	/// Generates an alias type with [JavaInteropAliases(...)] attribute for Java names that map to multiple .NET types.
	/// <code>
	/// [JavaInteropAliases("javaName[0]", "javaName[1]", ...)]
	/// class javaName_Aliases { }
	/// </code>
	/// </summary>
	TypeDefinition GenerateAliasType (string javaName, string[] aliasKeys)
	{
		// Create a valid C# type name from the Java name
		var typeName = javaName.Replace ('/', '_').Replace ('$', '_') + "_Aliases";

		var aliasType = new TypeDefinition (
			"Java.Interop.TypeMap._",
			typeName,
			TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Sealed,
			AssemblyToInjectTypeMap.MainModule.TypeSystem.Object);

		// Add [JavaInteropAliases("javaName[0]", "javaName[1]", ...)]
		var stringArrayType = new ArrayType (SystemStringType);
		var attr = new CustomAttribute (JavaInteropAliasesAttributeCtor);
		attr.ConstructorArguments.Add (new CustomAttributeArgument (stringArrayType,
			aliasKeys.Select (k => new CustomAttributeArgument (SystemStringType, k)).ToArray ()));
		aliasType.CustomAttributes.Add (attr);

		return aliasType;
	}

	/// <summary>
    /// Generates <code>[TypeMapAssociation(typeof(type), typeof(proxyType))]</code>
    /// </summary>
	CustomAttribute GenerateTypeMapAssociationAttribute (TypeDefinition type, TypeDefinition proxyType)
	{
		var ca = new CustomAttribute (TypeMapAssociationAttributeCtor);
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference(type)));
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference(proxyType)));
		return ca;
	}

	/// <summary>
	/// Generates <code>[TypeMapAssociation&lt;InvokerUniverse&gt;(typeof(interfaceType), typeof(invokerType))]</code>
	/// </summary>
	CustomAttribute GenerateInvokerTypeMapAssociationAttribute (TypeDefinition interfaceType, TypeDefinition invokerType)
	{
		var ca = new CustomAttribute (InvokerTypeMapAssociationAttributeCtor);
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference (interfaceType)));
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference (invokerType)));
		return ca;
	}

	/// <summary>
	/// Generates <code>[TypeMap("javaName", typeof(type), typeof(type))]</code>
	/// </summary>
	CustomAttribute GenerateTypeMapAttribute (TypeDefinition type, string javaName)
	{
		CustomAttribute ca = new (TypeMapAttributeCtor);
		ca.ConstructorArguments.Add (new (SystemStringType, javaName));
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference(type)));
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference(type)));
		return ca;
	}

	/// <summary>
	/// Applies the proxy attribute to the target type.
	/// This enables AOT-safe lookup via <c>type.GetCustomAttribute&lt;JavaPeerProxy&gt;()</c>
	/// instead of using <c>Activator.CreateInstance(proxyType)</c>.
	/// </summary>
	void ApplyProxyAttributeToTargetType (TypeDefinition targetType, TypeDefinition proxyType)
	{
		// Import the proxy type's constructor into the target type's module
		var proxyCtorDef = proxyType.Methods.Single (m => m.IsConstructor && !m.IsStatic && !m.HasParameters);
		var proxyCtorRef = targetType.Module.ImportReference (proxyCtorDef);

		// Create the custom attribute and apply it to the target type
		var attr = new CustomAttribute (proxyCtorRef);
		targetType.CustomAttributes.Add (attr);

		// Context.LogMessage (MessageContainer.CreateInfoMessage ($"Applied [{proxyType.FullName}] attribute to {targetType.FullName}"));
	}

	/// <summary>
	/// Generates a proxy attribute type that extends JavaPeerProxy with an annotated TargetType property.
	/// The proxy is an attribute that will be applied to the target type.
	/// <code>
	/// [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
	/// [TypeMapProxy("javaClassName")]
	/// sealed class AssemblyName._.mappedTypeFullName_Proxy : JavaPeerProxy
	/// {
	///     public override Type TargetType => typeof(MappedType);
	///     public override IntPtr GetFunctionPointer(int methodIndex) => methodIndex switch { 0 => ..., _ => IntPtr.Zero };
	/// }
	/// </code>
	/// </summary>
	TypeDefinition GenerateTypeMapProxyType (string javaClassName, TypeDefinition mappedType, List<MarshalMethodInfo> marshalMethods)
	{
		StringBuilder mappedName = new (mappedType.Name);
		TypeDefinition? declaringType = mappedType;
		while (declaringType is not null) {
			mappedName.Insert (0, "_");
			mappedName.Insert (0, declaringType.Name);
			if (declaringType.DeclaringType is null)
				break;
			declaringType = declaringType.DeclaringType;
		}

		// Create the proxy type extending JavaPeerProxy (which extends Attribute)
		// Note: JavaPeerProxy already has [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
		var proxyType = new TypeDefinition (
			mappedType.Module.Assembly.Name.Name + "._." + declaringType.Namespace,
			mappedName.ToString () + "_Proxy",
			TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Sealed,
			JavaPeerProxyType);

		// Add [TypeMapProxy("javaClassName")] attribute
		var ca = new CustomAttribute (TypeMapProxyAttributeCtor);
		ca.ConstructorArguments.Add (new CustomAttributeArgument (SystemStringType, javaClassName));
		proxyType.CustomAttributes.Add (ca);

		// Add default constructor that calls base()
		var ctor = new MethodDefinition (
			".ctor",
			MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
			AssemblyToInjectTypeMap.MainModule.TypeSystem.Void);

		var ctorIl = ctor.Body.GetILProcessor ();
		ctorIl.Emit (Mono.Cecil.Cil.OpCodes.Ldarg_0);
		ctorIl.Emit (Mono.Cecil.Cil.OpCodes.Call, JavaPeerProxyDefaultCtor);
		ctorIl.Emit (Mono.Cecil.Cil.OpCodes.Ret);
		proxyType.Methods.Add (ctor);

		// Generate UCO wrappers and GetFunctionPointer if there are marshal methods
		if (marshalMethods.Count > 0) {
			GenerateUcoWrappers (proxyType, mappedType, marshalMethods);
		}
		// Always generate GetFunctionPointer as it is abstract in JavaPeerProxy
		GenerateGetFunctionPointerMethod (proxyType, mappedType, marshalMethods);

		// Generate CreateInstance for AOT-safe instance creation without reflection
		GenerateCreateInstanceMethod (proxyType, mappedType);

		return proxyType;
	}

	/// <summary>
	/// Generates [UnmanagedCallersOnly] wrapper methods for marshal methods.
	/// These wrappers are added to the proxy type and call the original native callbacks.
	/// </summary>
	void GenerateUcoWrappers (TypeDefinition proxyType, TypeDefinition mappedType, List<MarshalMethodInfo> marshalMethods)
	{
		for (int i = 0; i < marshalMethods.Count; i++) {
			var methodInfo = marshalMethods [i];
			var callback = methodInfo.NativeCallback;

			// Check if it already has [UnmanagedCallersOnly]
			if (HasUnmanagedCallersOnlyAttribute (callback)) {
				methodInfo.UcoWrapper = callback;
				// Context.LogMessage (MessageContainer.CreateInfoMessage (
					// $"Method {callback.FullName} already has [UnmanagedCallersOnly]"));
				continue;
			}

			// Generate a UCO wrapper method in the proxy type
			string wrapperName = $"n_{methodInfo.JniName}_mm_{i}";
			var wrapper = GenerateUcoWrapperMethod (proxyType, callback, wrapperName);
			if (wrapper != null) {
				methodInfo.UcoWrapper = wrapper;
				proxyType.Methods.Add (wrapper);
				// Context.LogMessage (MessageContainer.CreateInfoMessage (
					// $"Generated UCO wrapper {proxyType.FullName}.{wrapperName} for {callback.FullName}"));
			} else {
				// Don't set UcoWrapper - it will remain null and be skipped in GetFunctionPointer
				// The original callback is in a different assembly and cannot be imported
				// Context.LogMessage (MessageContainer.CreateInfoMessage (
					// $"Failed to generate UCO wrapper for {callback.FullName}, method will use dynamic registration"));
			}
		}
	}

	static bool HasUnmanagedCallersOnlyAttribute (MethodDefinition method)
	{
		foreach (CustomAttribute ca in method.CustomAttributes) {
			if (ca.Constructor.DeclaringType.FullName == "System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute") {
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Generates a single [UnmanagedCallersOnly] wrapper method that calls the original native callback.
	/// The wrapper handles exception propagation and type conversions for non-blittable types.
	/// </summary>
	MethodDefinition? GenerateUcoWrapperMethod (TypeDefinition proxyType, MethodDefinition callback, string wrapperName)
	{
		try {
			// Map return type to blittable
			TypeReference retType = MapToBlittableTypeIfNecessary (callback.ReturnType, out bool returnTypeMapped);
			bool hasReturnValue = callback.ReturnType.FullName != "System.Void";

			var wrapperMethod = new MethodDefinition (
				wrapperName,
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
				AssemblyToInjectTypeMap.MainModule.ImportReference (retType));

			// Add [UnmanagedCallersOnly] attribute
			wrapperMethod.CustomAttributes.Add (new CustomAttribute (UnmanagedCallersOnlyAttributeCtor));

			var body = wrapperMethod.Body;
			body.InitLocals = true;
			var il = body.GetILProcessor ();

			// Add return value variable if needed
			VariableDefinition? retval = null;
			if (hasReturnValue) {
				retval = new VariableDefinition (AssemblyToInjectTypeMap.MainModule.ImportReference (retType));
				body.Variables.Add (retval);
			}

			// Call WaitForBridgeProcessing if available
			if (WaitForBridgeProcessingMethod != null) {
				il.Emit (Mono.Cecil.Cil.OpCodes.Call, WaitForBridgeProcessingMethod);
			}

			// Set up exception handler
			var exceptionHandler = new ExceptionHandler (ExceptionHandlerType.Catch) {
				CatchType = SystemExceptionType,
			};
			body.ExceptionHandlers.Add (exceptionHandler);

			// Load parameters and call the original callback
			Instruction? firstTryInstruction = null;
			int paramIndex = 0;
			foreach (var pdef in callback.Parameters) {
				TypeReference newType = MapToBlittableTypeIfNecessary (pdef.ParameterType, out bool paramMapped);
				wrapperMethod.Parameters.Add (new ParameterDefinition (pdef.Name, pdef.Attributes,
					AssemblyToInjectTypeMap.MainModule.ImportReference (newType)));

				var loadInst = GetLoadArgInstruction (paramIndex++);
				if (firstTryInstruction == null) {
					firstTryInstruction = loadInst;
				}
				il.Append (loadInst);

				// Handle non-blittable parameter conversion (e.g., byte -> bool)
				if (paramMapped && pdef.ParameterType.FullName == "System.Boolean") {
					// Convert byte to bool: param != 0
					il.Emit (Mono.Cecil.Cil.OpCodes.Ldc_I4_0);
					il.Emit (Mono.Cecil.Cil.OpCodes.Cgt_Un);
				}
			}

			// Call the original callback
			var callInst = Mono.Cecil.Cil.Instruction.Create (Mono.Cecil.Cil.OpCodes.Call,
				AssemblyToInjectTypeMap.MainModule.ImportReference (callback));
			if (firstTryInstruction == null) {
				firstTryInstruction = callInst;
			}
			il.Append (callInst);

			exceptionHandler.TryStart = firstTryInstruction;

			// Handle return value
			if (hasReturnValue) {
				if (returnTypeMapped && callback.ReturnType.FullName == "System.Boolean") {
					// Convert bool to byte
					var insLoadOne = Mono.Cecil.Cil.Instruction.Create (Mono.Cecil.Cil.OpCodes.Ldc_I4_1);
					var insConvert = Mono.Cecil.Cil.Instruction.Create (Mono.Cecil.Cil.OpCodes.Conv_U1);
					il.Emit (Mono.Cecil.Cil.OpCodes.Brtrue_S, insLoadOne);
					il.Emit (Mono.Cecil.Cil.OpCodes.Ldc_I4_0);
					il.Emit (Mono.Cecil.Cil.OpCodes.Br_S, insConvert);
					il.Append (insLoadOne);
					il.Append (insConvert);
				}
				il.Emit (Mono.Cecil.Cil.OpCodes.Stloc, retval);
			}

			// Leave try block
			var ret = Mono.Cecil.Cil.Instruction.Create (Mono.Cecil.Cil.OpCodes.Ret);
			Instruction leaveTarget;
			Instruction? retValLoadInst = null;
			if (hasReturnValue) {
				retValLoadInst = Mono.Cecil.Cil.Instruction.Create (Mono.Cecil.Cil.OpCodes.Ldloc, retval);
				leaveTarget = retValLoadInst;
			} else {
				leaveTarget = ret;
			}
			il.Emit (Mono.Cecil.Cil.OpCodes.Leave_S, leaveTarget);

			// Exception handler
			var exceptionVar = new VariableDefinition (SystemExceptionType);
			body.Variables.Add (exceptionVar);

			var catchStartInst = Mono.Cecil.Cil.Instruction.Create (Mono.Cecil.Cil.OpCodes.Stloc, exceptionVar);
			exceptionHandler.HandlerStart = catchStartInst;
			exceptionHandler.TryEnd = catchStartInst;

			il.Append (catchStartInst);
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldarg_0); // jnienv
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldloc, exceptionVar);

			if (UnhandledExceptionMethod != null) {
				il.Emit (Mono.Cecil.Cil.OpCodes.Call, UnhandledExceptionMethod);
				// Set default return value
				if (hasReturnValue) {
					AddSetDefaultValueInstructions (il, retType, retval!);
				}
			} else {
				// If no unhandled exception method, just rethrow
				il.Emit (Mono.Cecil.Cil.OpCodes.Pop); // pop jnienv
				il.Emit (Mono.Cecil.Cil.OpCodes.Throw);
			}

			il.Emit (Mono.Cecil.Cil.OpCodes.Leave_S, leaveTarget);

			// Return
			if (hasReturnValue) {
				il.Append (retValLoadInst);
				exceptionHandler.HandlerEnd = retValLoadInst;
			} else {
				exceptionHandler.HandlerEnd = ret;
			}
			il.Append (ret);

			return wrapperMethod;
		} catch (Exception ex) {
			// Context.LogMessage (MessageContainer.CreateInfoMessage (
				// $"Failed to generate UCO wrapper for {callback.FullName}: {ex.Message}"));
			return null;
		}
	}

	static Instruction GetLoadArgInstruction (int paramIndex)
	{
		return paramIndex switch {
			0 => Mono.Cecil.Cil.Instruction.Create (Mono.Cecil.Cil.OpCodes.Ldarg_0),
			1 => Mono.Cecil.Cil.Instruction.Create (Mono.Cecil.Cil.OpCodes.Ldarg_1),
			2 => Mono.Cecil.Cil.Instruction.Create (Mono.Cecil.Cil.OpCodes.Ldarg_2),
			3 => Mono.Cecil.Cil.Instruction.Create (Mono.Cecil.Cil.OpCodes.Ldarg_3),
			_ => Mono.Cecil.Cil.Instruction.Create (Mono.Cecil.Cil.OpCodes.Ldarg_S, (byte)paramIndex),
		};
	}

	TypeReference MapToBlittableTypeIfNecessary (TypeReference type, out bool typeMapped)
	{
		if (type.FullName == "System.Void" || IsBlittable (type)) {
			typeMapped = false;
			return type;
		}

		if (type.FullName == "System.Boolean") {
			typeMapped = true;
			return AssemblyToInjectTypeMap.MainModule.TypeSystem.Byte;
		}

		// For other non-blittable types, just return as-is and hope for the best
		typeMapped = false;
		return type;
	}

	static bool IsBlittable (TypeReference type)
	{
		return type.FullName switch {
			"System.Void" => true,
			"System.Boolean" => false, // Not blittable!
			"System.Byte" => true,
			"System.SByte" => true,
			"System.Int16" => true,
			"System.UInt16" => true,
			"System.Int32" => true,
			"System.UInt32" => true,
			"System.Int64" => true,
			"System.UInt64" => true,
			"System.IntPtr" => true,
			"System.UIntPtr" => true,
			"System.Single" => true,
			"System.Double" => true,
			_ => type.IsValueType,
		};
	}

	void AddSetDefaultValueInstructions (ILProcessor il, TypeReference type, VariableDefinition retval)
	{
		switch (type.FullName) {
			case "System.Boolean":
			case "System.Byte":
			case "System.Int16":
			case "System.Int32":
			case "System.SByte":
			case "System.UInt16":
			case "System.UInt32":
				il.Emit (Mono.Cecil.Cil.OpCodes.Ldc_I4_0);
				break;

			case "System.Int64":
			case "System.UInt64":
				il.Emit (Mono.Cecil.Cil.OpCodes.Ldc_I4_0);
				il.Emit (Mono.Cecil.Cil.OpCodes.Conv_I8);
				break;

			case "System.IntPtr":
			case "System.UIntPtr":
				il.Emit (Mono.Cecil.Cil.OpCodes.Ldc_I4_0);
				il.Emit (Mono.Cecil.Cil.OpCodes.Conv_I);
				break;

			case "System.Single":
				il.Emit (Mono.Cecil.Cil.OpCodes.Ldc_R4, 0.0f);
				break;

			case "System.Double":
				il.Emit (Mono.Cecil.Cil.OpCodes.Ldc_R8, 0.0);
				break;

			default:
				// For other types, just load 0
				il.Emit (Mono.Cecil.Cil.OpCodes.Ldc_I4_0);
				break;
		}
		il.Emit (Mono.Cecil.Cil.OpCodes.Stloc, retval);
	}

	/// <summary>
	/// Generates the GetFunctionPointer method override that returns function pointers for marshal methods:
	/// <code>
	/// public override IntPtr GetFunctionPointer(int methodIndex)
	///     => methodIndex switch {
	///         0 => (IntPtr)(delegate*&lt;IntPtr, IntPtr, ...&gt;)&amp;TargetType.n_Method0,
	///         1 => (IntPtr)(delegate*&lt;IntPtr, IntPtr, ...&gt;)&amp;TargetType.n_Method1,
	///         _ => IntPtr.Zero,
	///     };
	/// </code>
	/// </summary>
	void GenerateGetFunctionPointerMethod (TypeDefinition proxyType, TypeDefinition mappedType, List<MarshalMethodInfo> marshalMethods)
	{
		// Get IntPtr type
		var intPtrTypeDef = Context.GetType ("System.IntPtr");
		var intPtrType = AssemblyToInjectTypeMap.MainModule.ImportReference (intPtrTypeDef);

		// Get IntPtr.Zero field
		var intPtrZeroField = intPtrTypeDef.Fields.FirstOrDefault (f => f.Name == "Zero")
			?? throw new InvalidOperationException ("Could not find IntPtr.Zero");
		var intPtrZeroRef = AssemblyToInjectTypeMap.MainModule.ImportReference (intPtrZeroField);

		// Create the override method
		var method = new MethodDefinition (
			"GetFunctionPointer",
			MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
			intPtrType);

		method.Parameters.Add (new ParameterDefinition ("methodIndex", ParameterAttributes.None,
			AssemblyToInjectTypeMap.MainModule.ImportReference (Context.GetType ("System.Int32"))));

		var il = method.Body.GetILProcessor ();

		// For now, generate a simple switch using if/else pattern
		// if (methodIndex == 0) return (IntPtr)&TargetType.n_Method0;
		// if (methodIndex == 1) return (IntPtr)&TargetType.n_Method1;
		// return IntPtr.Zero;

		var returnZeroLabel = il.Create (Mono.Cecil.Cil.OpCodes.Ldsfld, intPtrZeroRef);

		for (int i = 0; i < marshalMethods.Count; i++) {
			var methodInfo = marshalMethods [i];

			// Skip if no wrapper was generated
			if (methodInfo.UcoWrapper == null) {
				continue;
			}

			// Load methodIndex argument
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldarg_1);
			// Load constant i
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldc_I4, i);
			// Compare: if (methodIndex != i) goto next
			var nextLabel = il.Create (Mono.Cecil.Cil.OpCodes.Nop);
			il.Emit (Mono.Cecil.Cil.OpCodes.Bne_Un, nextLabel);

			// Load function pointer for the UCO wrapper method
			// ldftn ProxyType::n_MethodName_mm_N
			// Import the reference as it might be in a different assembly (e.g. user assembly with [UCO])
			MethodReference ucoWrapperRef;
			if (methodInfo.UcoWrapper.DeclaringType?.Module == null) {
				// Generated wrapper in the new proxy type (not yet added to module)
				// Use the definition directly
				ucoWrapperRef = methodInfo.UcoWrapper;
			} else {
				try {
					ucoWrapperRef = AssemblyToInjectTypeMap.MainModule.ImportReference (methodInfo.UcoWrapper);
				} catch (Exception ex) {
					throw new InvalidOperationException ($"Failed to import reference for {methodInfo.UcoWrapper?.FullName ?? "null"} into {AssemblyToInjectTypeMap?.Name?.Name ?? "null"}", ex);
				}
			}
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldftn, ucoWrapperRef);
			// Return it (function pointer is already an IntPtr-sized value)
			il.Emit (Mono.Cecil.Cil.OpCodes.Ret);

			// next:
			il.Append (nextLabel);
		}

		// return IntPtr.Zero
		il.Append (returnZeroLabel);
		il.Emit (Mono.Cecil.Cil.OpCodes.Ret);

		proxyType.Methods.Add (method);

		// Context.LogMessage (MessageContainer.CreateInfoMessage (
			// $"Generated GetFunctionPointer for {proxyType.FullName} with {marshalMethods.Count} methods"));
	}

	/// <summary>
	/// Generates the CreateInstance factory method for AOT-safe instance creation:
	/// <code>
	/// public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
	///     => new MappedType(handle, transfer);
	/// </code>
	/// </summary>
	void GenerateCreateInstanceMethod (TypeDefinition proxyType, TypeDefinition mappedType)
	{
		// Skip static classes - they can't have instances
		if (mappedType.IsAbstract && mappedType.IsSealed) {
			// Static class in C# is abstract sealed - no need for CreateInstance
			return;
		}

		// Find the (IntPtr, JniHandleOwnership) constructor on the mapped type
		var ctor = mappedType.Methods.FirstOrDefault (m =>
			m.IsConstructor && !m.IsStatic &&
			m.Parameters.Count == 2 &&
			m.Parameters [0].ParameterType.FullName == "System.IntPtr" &&
			m.Parameters [1].ParameterType.FullName == "Android.Runtime.JniHandleOwnership");

		bool isJIConstructor = false;
		if (ctor == null) {
			// Try to find (ref JniObjectReference, JniObjectReferenceOptions) constructor
			ctor = mappedType.Methods.FirstOrDefault (m =>
				m.IsConstructor && !m.IsStatic &&
				m.Parameters.Count == 2 &&
				m.Parameters [0].ParameterType.IsByReference &&
				m.Parameters [0].ParameterType.GetElementType ().FullName == "Java.Interop.JniObjectReference" &&
				m.Parameters [1].ParameterType.FullName == "Java.Interop.JniObjectReferenceOptions");

			if (ctor != null) {
				isJIConstructor = true;
			}
		}

		// Get the return type (IJavaPeerable)
		var iJavaPeerableType = Context.GetType ("Java.Interop.IJavaPeerable");
		var iJavaPeerableRef = AssemblyToInjectTypeMap.MainModule.ImportReference (iJavaPeerableType);

		// Get parameter types
		var intPtrType = AssemblyToInjectTypeMap.MainModule.ImportReference (Context.GetType ("System.IntPtr"));
		var jniHandleOwnershipType = AssemblyToInjectTypeMap.MainModule.ImportReference (Context.GetType ("Android.Runtime.JniHandleOwnership"));

		// Create the method
		var method = new MethodDefinition (
			"CreateInstance",
			MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
			iJavaPeerableRef);

		method.Parameters.Add (new ParameterDefinition ("handle", ParameterAttributes.None, intPtrType));
		method.Parameters.Add (new ParameterDefinition ("transfer", ParameterAttributes.None, jniHandleOwnershipType));

		var il = method.Body.GetILProcessor ();

		// No suitable constructor found - generate method that throws
		if (ctor == null) {
			// throw new NotSupportedException($"No suitable constructor found for {mappedType.FullName}")
			var notSupportedExTypeDef = Context.GetType ("System.NotSupportedException");
			var notSupportedExCtor = notSupportedExTypeDef.Methods.FirstOrDefault (m =>
				m.IsConstructor && !m.IsStatic &&
				m.Parameters.Count == 1 &&
				m.Parameters [0].ParameterType.FullName == "System.String");
			
			if (notSupportedExCtor != null) {
				var notSupportedExCtorRef = AssemblyToInjectTypeMap.MainModule.ImportReference (notSupportedExCtor);
				il.Emit (Mono.Cecil.Cil.OpCodes.Ldstr, $"No suitable constructor found for type '{mappedType.FullName}'. Expected (IntPtr, JniHandleOwnership) or (ref JniObjectReference, JniObjectReferenceOptions) constructor.");
				il.Emit (Mono.Cecil.Cil.OpCodes.Newobj, notSupportedExCtorRef);
				il.Emit (Mono.Cecil.Cil.OpCodes.Throw);
			} else {
				// Fallback: just return null
				il.Emit (Mono.Cecil.Cil.OpCodes.Ldnull);
				il.Emit (Mono.Cecil.Cil.OpCodes.Ret);
			}
			
			proxyType.Methods.Add (method);
			return;
		}

		// Import the constructor reference
		var ctorRef = AssemblyToInjectTypeMap.MainModule.ImportReference (ctor);

		if (!isJIConstructor) {
			// Direct XA constructor: new MappedType(handle, transfer)
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldarg_1); // handle
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldarg_2); // transfer
			il.Emit (Mono.Cecil.Cil.OpCodes.Newobj, ctorRef);
		} else {
			// JI constructor: new MappedType(ref new JniObjectReference(handle), JniObjectReferenceOptions.Copy)
			// Then call JNIEnv.DeleteRef(handle, transfer)
			
			// Get types we need
			var jniObjRefTypeDef = Context.GetType ("Java.Interop.JniObjectReference");
			var jniObjRefType = AssemblyToInjectTypeMap.MainModule.ImportReference (jniObjRefTypeDef);
			var jniObjRefOptionsTypeDef = Context.GetType ("Java.Interop.JniObjectReferenceOptions");
			
			// Find JniObjectReference(IntPtr) constructor
			var jniObjRefCtor = jniObjRefTypeDef.Methods.FirstOrDefault (m =>
				m.IsConstructor && !m.IsStatic &&
				m.Parameters.Count == 1 &&
				m.Parameters [0].ParameterType.FullName == "System.IntPtr");
			
			if (jniObjRefCtor == null) {
				// Try with optional second parameter
				jniObjRefCtor = jniObjRefTypeDef.Methods.FirstOrDefault (m =>
					m.IsConstructor && !m.IsStatic &&
					m.Parameters.Count == 2 &&
					m.Parameters [0].ParameterType.FullName == "System.IntPtr" &&
					m.Parameters [1].ParameterType.FullName == "Java.Interop.JniObjectReferenceType");
			}

			if (jniObjRefCtor == null) {
				// Fallback: just return null if we can't find the constructor
				il.Emit (Mono.Cecil.Cil.OpCodes.Ldnull);
				il.Emit (Mono.Cecil.Cil.OpCodes.Ret);
				proxyType.Methods.Add (method);
				return;
			}
			
			var jniObjRefCtorRef = AssemblyToInjectTypeMap.MainModule.ImportReference (jniObjRefCtor);
			
			// Declare local for JniObjectReference
			method.Body.InitLocals = true;
			var jniRefLocal = new Mono.Cecil.Cil.VariableDefinition (jniObjRefType);
			method.Body.Variables.Add (jniRefLocal);
			
			// Create JniObjectReference: local = new JniObjectReference(handle)
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldloca_S, jniRefLocal);
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldarg_1); // handle
			if (jniObjRefCtor.Parameters.Count == 2) {
				// Need to pass JniObjectReferenceType.Invalid (0)
				il.Emit (Mono.Cecil.Cil.OpCodes.Ldc_I4_0);
			}
			il.Emit (Mono.Cecil.Cil.OpCodes.Call, jniObjRefCtorRef);
			
			// Call constructor: new MappedType(ref local, JniObjectReferenceOptions.Copy)
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldloca_S, jniRefLocal); // ref jniObjRef
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldc_I4_1); // JniObjectReferenceOptions.Copy = 1
			il.Emit (Mono.Cecil.Cil.OpCodes.Newobj, ctorRef);
			
			// Store result in local variable (we need it after DeleteRef)
			var resultLocal = new Mono.Cecil.Cil.VariableDefinition (iJavaPeerableRef);
			method.Body.Variables.Add (resultLocal);
			il.Emit (Mono.Cecil.Cil.OpCodes.Stloc, resultLocal);
			
			// Call JNIEnv.DeleteRef(handle, transfer)
			var jniEnvTypeDef = Context.GetType ("Android.Runtime.JNIEnv");
			var deleteRefMethod = jniEnvTypeDef.Methods.FirstOrDefault (m =>
				m.Name == "DeleteRef" && m.IsStatic &&
				m.Parameters.Count == 2 &&
				m.Parameters [0].ParameterType.FullName == "System.IntPtr" &&
				m.Parameters [1].ParameterType.FullName == "Android.Runtime.JniHandleOwnership");
			
			if (deleteRefMethod != null) {
				var deleteRefRef = AssemblyToInjectTypeMap.MainModule.ImportReference (deleteRefMethod);
				il.Emit (Mono.Cecil.Cil.OpCodes.Ldarg_1); // handle
				il.Emit (Mono.Cecil.Cil.OpCodes.Ldarg_2); // transfer
				il.Emit (Mono.Cecil.Cil.OpCodes.Call, deleteRefRef);
			}
			
			// Load result and return
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldloc, resultLocal);
		}

		il.Emit (Mono.Cecil.Cil.OpCodes.Ret);

		proxyType.Methods.Add (method);
	}
}
