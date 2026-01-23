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
/// Simple timing helper for profiling GenerateTypeMapAttributesStep performance.
/// </summary>
static class TimingLog
{
	const string LogPath = "/tmp/illink-timing.log";
	static readonly Stopwatch _globalStopwatch = Stopwatch.StartNew ();
	static readonly object _lock = new ();
	static readonly Dictionary<string, (long totalMs, int count)> _aggregates = new ();

	static TimingLog ()
	{
		File.WriteAllText (LogPath, $"=== ILLink Timing Log Started at {DateTime.Now} ===\n");
	}

	public static void Log (string message)
	{
		lock (_lock) {
			File.AppendAllText (LogPath, $"[{_globalStopwatch.ElapsedMilliseconds,6}ms] {message}\n");
		}
	}

	public static void LogAggregate (string operation, long elapsedMs)
	{
		lock (_lock) {
			if (!_aggregates.TryGetValue (operation, out var current)) {
				current = (0, 0);
			}
			_aggregates[operation] = (current.totalMs + elapsedMs, current.count + 1);
		}
	}

	public static void DumpAggregates ()
	{
		lock (_lock) {
			File.AppendAllText (LogPath, "\n=== AGGREGATE TIMING SUMMARY ===\n");
			foreach (var kvp in _aggregates.OrderByDescending (x => x.Value.totalMs)) {
				var avg = kvp.Value.count > 0 ? kvp.Value.totalMs / kvp.Value.count : 0;
				File.AppendAllText (LogPath, $"  {kvp.Key}: {kvp.Value.totalMs}ms total, {kvp.Value.count} calls, {avg}ms avg\n");
			}
			File.AppendAllText (LogPath, "=== END SUMMARY ===\n");
		}
	}

	public static Timing Start (string operation) => new Timing (operation);

	public struct Timing : IDisposable
	{
		readonly string _operation;
		readonly Stopwatch _sw;
		readonly bool _aggregate;

		public Timing (string operation, bool aggregate = false)
		{
			_operation = operation;
			_aggregate = aggregate;
			_sw = Stopwatch.StartNew ();
			if (!aggregate) {
				Log ($"START: {operation}");
			}
		}

		public void Dispose ()
		{
			_sw.Stop ();
			if (_aggregate) {
				LogAggregate (_operation, _sw.ElapsedMilliseconds);
			} else {
				Log ($"END: {_operation} ({_sw.ElapsedMilliseconds}ms)");
			}
		}
	}

	public static Timing StartAggregate (string operation) => new Timing (operation, aggregate: true);
}

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
		using var _ = TimingLog.Start ("Process");
		try {
		using (TimingLog.Start ("Process.Initialization")) {
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
		} // end Process.Initialization timing block

		using (TimingLog.Start ("Process.BaseProcess")) {
		base.Process ();
		}
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
		using var _ = TimingLog.StartAggregate ($"ProcessAssembly");
		TimingLog.Log ($"ProcessAssembly: {assembly.Name.Name} ({assembly.MainModule.Types.Count} types)");
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

	// Cache for frequently used types to avoid repeated Context.GetType calls
	TypeDefinition? _cachedIntPtrTypeDef;
	FieldDefinition? _cachedIntPtrZeroField;
	TypeDefinition? _cachedInt32TypeDef;

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

		// For Implementor types (like IOnClickListenerImplementor), we need to also
		// collect methods from the interface that have [Register] with connectors pointing to Invoker types.
		// This is because the Java JCW calls n_onClick but the managed callback is in IOnClickListenerInvoker.
		// 
		// IMPORTANT: Only do this for Implementor types, NOT for Invoker types.
		// Invoker types (DoNotGenerateAcw=true) already have all callback methods they need.
		// Implementor types have JNI class names starting with "mono/android/" (custom JCWs).
		bool isOnClickListenerImplementor = type.FullName.Contains ("IOnClickListenerImplementor");
		if (isOnClickListenerImplementor) {
			File.AppendAllText ("/tmp/typemap-debug.log", 
				$"CollectMarshalMethods: {type.FullName} HasInterfaces={type.HasInterfaces}, IsImplementorType={IsImplementorType (type)}\n");
			foreach (var attr in type.CustomAttributes) {
				if (attr.AttributeType.FullName == "Android.Runtime.RegisterAttribute") {
					File.AppendAllText ("/tmp/typemap-debug.log", 
						$"CollectMarshalMethods: [Register] args count={attr.ConstructorArguments.Count}\n");
					if (attr.ConstructorArguments.Count > 0) {
						File.AppendAllText ("/tmp/typemap-debug.log", 
							$"CollectMarshalMethods: [Register] arg0='{attr.ConstructorArguments [0].Value}'\n");
					}
				}
			}
		}
		if (type.HasInterfaces && IsImplementorType (type)) {
			CollectInterfaceMarshalMethods (type, methods, seen);
		}

		return methods;
	}

	/// <summary>
	/// Checks if a type is an Implementor type (has a custom JCW starting with "mono/android/").
	/// Implementor types are used to implement Java interfaces in managed code.
	/// </summary>
	bool IsImplementorType (TypeDefinition type)
	{
		// Check if the type has a Register attribute with a JNI name starting with "mono/android/"
		// This indicates it's a custom JCW (Implementor) rather than a binding (Invoker)
		foreach (var attr in type.CustomAttributes) {
			if (attr.AttributeType.FullName != "Android.Runtime.RegisterAttribute") {
				continue;
			}
			if (attr.ConstructorArguments.Count > 0) {
				string? jniName = attr.ConstructorArguments [0].Value as string;
				if (!string.IsNullOrEmpty (jniName) && jniName.StartsWith ("mono/android/", StringComparison.Ordinal)) {
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// Collects marshal methods from implemented interfaces.
	/// This handles Implementor types like IOnClickListenerImplementor whose Java JCWs
	/// call native methods (n_onClick) but the managed callbacks are in Invoker types.
	/// </summary>
	void CollectInterfaceMarshalMethods (TypeDefinition type, List<MarshalMethodInfo> methods, HashSet<string> seen)
	{
		bool isOnClickListenerImplementor = type.FullName.Contains ("IOnClickListenerImplementor");
		if (isOnClickListenerImplementor) {
			File.AppendAllText ("/tmp/typemap-debug.log",
				$"CollectInterfaceMarshalMethods: Processing {type.FullName} with {type.Interfaces.Count} interfaces\n");
		}

		foreach (var iface in type.Interfaces) {
			var ifaceType = iface.InterfaceType.Resolve ();
			if (ifaceType == null) {
				if (isOnClickListenerImplementor) {
					File.AppendAllText ("/tmp/typemap-debug.log",
						$"CollectInterfaceMarshalMethods: Could not resolve interface {iface.InterfaceType.FullName}\n");
				}
				continue;
			}

			if (isOnClickListenerImplementor) {
				File.AppendAllText ("/tmp/typemap-debug.log",
					$"CollectInterfaceMarshalMethods: Checking interface {ifaceType.FullName} with {ifaceType.Methods.Count} methods\n");
			}

			foreach (var method in ifaceType.Methods) {
				if (!CecilExtensions.HasMethodRegistrationAttributes (method)) {
					continue;
				}

				foreach (var attr in CecilExtensions.GetMethodRegistrationAttributes (method)) {
					if (isOnClickListenerImplementor) {
						File.AppendAllText ("/tmp/typemap-debug.log",
							$"CollectInterfaceMarshalMethods: Found [Register] on {method.Name}: Name={attr.Name}, Sig={attr.Signature}, Connector={attr.Connector}\n");
					}

					if (string.IsNullOrEmpty (attr.Name) || string.IsNullOrEmpty (attr.Signature) || string.IsNullOrEmpty (attr.Connector)) {
						if (isOnClickListenerImplementor) {
							File.AppendAllText ("/tmp/typemap-debug.log",
								$"CollectInterfaceMarshalMethods: Skipping due to empty Name/Signature/Connector\n");
						}
						continue;
					}

					string key = $"{attr.Name}{attr.Signature}";
					if (seen.Contains (key)) {
						if (isOnClickListenerImplementor) {
							File.AppendAllText ("/tmp/typemap-debug.log",
								$"CollectInterfaceMarshalMethods: Skipping duplicate key={key}\n");
						}
						continue;
					}

					// Parse connector to find the Invoker type: "GetOnClick_Handler:Type.Name, Assembly"
					var invokerInfo = ParseConnectorToInvokerType (attr.Connector, type.Module);
					if (invokerInfo == null) {
						if (isOnClickListenerImplementor) {
							File.AppendAllText ("/tmp/typemap-debug.log",
								$"CollectInterfaceMarshalMethods: ParseConnectorToInvokerType returned null for Connector={attr.Connector}\n");
						}
						continue;
					}

					var (invokerType, handlerMethodName) = invokerInfo.Value;
					
					if (isOnClickListenerImplementor) {
						File.AppendAllText ("/tmp/typemap-debug.log",
							$"CollectInterfaceMarshalMethods: Found invoker type {invokerType.FullName}, handlerMethodName={handlerMethodName}\n");
					}

					// Find the native callback in the Invoker type
					// The callback name follows the pattern from the handler method
					string? nativeCallbackName = GetNativeCallbackNameFromHandler (handlerMethodName, attr.Name, attr.Signature);
					
					if (isOnClickListenerImplementor) {
						File.AppendAllText ("/tmp/typemap-debug.log",
							$"CollectInterfaceMarshalMethods: Looking for nativeCallbackName={nativeCallbackName} in {invokerType.FullName}\n");
						foreach (var m in invokerType.Methods.Where (m => m.IsStatic && m.Name.StartsWith ("n_", StringComparison.Ordinal))) {
							File.AppendAllText ("/tmp/typemap-debug.log",
								$"CollectInterfaceMarshalMethods: Available static n_ method: {m.Name}\n");
						}
					}

					MethodDefinition? nativeCallback = nativeCallbackName != null
						? invokerType.Methods.FirstOrDefault (m => m.Name == nativeCallbackName && m.IsStatic)
						: null;

					if (nativeCallback == null) {
						// Try alternative naming patterns
						nativeCallback = invokerType.Methods.FirstOrDefault (m => 
							m.IsStatic && m.Name.StartsWith ("n_", StringComparison.Ordinal) &&
							m.Name.Contains (attr.Name, StringComparison.OrdinalIgnoreCase));
					}

					if (nativeCallback == null) {
						if (isOnClickListenerImplementor) {
							File.AppendAllText ("/tmp/typemap-debug.log",
								$"CollectInterfaceMarshalMethods: Could not find native callback for {attr.Name}\n");
						}
						continue;
					}

					if (isOnClickListenerImplementor) {
						File.AppendAllText ("/tmp/typemap-debug.log",
							$"CollectInterfaceMarshalMethods: SUCCESS - Found native callback {nativeCallback.Name} for {attr.Name}\n");
					}

					seen.Add (key);
					methods.Add (new MarshalMethodInfo (attr.Name, attr.Signature, nativeCallback, method));
				}
			}
		}
	}

	/// <summary>
	/// Parses a connector string to find the Invoker type and handler method name.
	/// Connector format: "GetOnClick_Landroid_view_View_Handler:Android.Views.View/IOnClickListenerInvoker, Mono.Android, ..."
	/// </summary>
	(TypeDefinition invokerType, string handlerMethodName)? ParseConnectorToInvokerType (string connector, ModuleDefinition module)
	{
		bool debug = connector.Contains ("OnClickListener");
		if (debug) {
			File.AppendAllText ("/tmp/typemap-debug.log", $"ParseConnectorToInvokerType: connector={connector}\n");
		}

		// Split by colon to get handler method and type info
		int colonIdx = connector.IndexOf (':');
		if (colonIdx < 0) {
			if (debug) File.AppendAllText ("/tmp/typemap-debug.log", $"ParseConnectorToInvokerType: no colon found\n");
			return null;
		}

		string handlerMethodName = connector.Substring (0, colonIdx);
		string typeInfo = connector.Substring (colonIdx + 1);

		// Parse type info: "Android.Views.View/IOnClickListenerInvoker, Mono.Android, ..."
		int commaIdx = typeInfo.IndexOf (',');
		if (commaIdx < 0) {
			if (debug) File.AppendAllText ("/tmp/typemap-debug.log", $"ParseConnectorToInvokerType: no comma found\n");
			return null;
		}

		string typeName = typeInfo.Substring (0, commaIdx).Trim ();
		string assemblyName = typeInfo.Substring (commaIdx + 1).Split (',')[0].Trim ();

		// Cecil uses / for nested types in FullName, but the connector string already uses /
		// So we don't need to replace anything - the connector string format matches Cecil's format

		if (debug) {
			File.AppendAllText ("/tmp/typemap-debug.log", $"ParseConnectorToInvokerType: typeName={typeName}, assemblyName={assemblyName}\n");
			File.AppendAllText ("/tmp/typemap-debug.log", $"ParseConnectorToInvokerType: module.Assembly.Name.Name={module.Assembly.Name.Name}\n");
			File.AppendAllText ("/tmp/typemap-debug.log", $"ParseConnectorToInvokerType: AssemblyReferences count={module.AssemblyReferences.Count}\n");
			foreach (var asmRef in module.AssemblyReferences) {
				File.AppendAllText ("/tmp/typemap-debug.log", $"ParseConnectorToInvokerType: AssemblyReference={asmRef.Name}\n");
			}
		}

		// Find the assembly
		AssemblyDefinition? assembly = null;
		foreach (var asmRef in module.AssemblyReferences) {
			if (asmRef.Name == assemblyName) {
				try {
					assembly = Context.Resolve (asmRef);
					if (debug) File.AppendAllText ("/tmp/typemap-debug.log", $"ParseConnectorToInvokerType: Resolved assembly {asmRef.Name}\n");
				} catch (Exception ex) {
					if (debug) File.AppendAllText ("/tmp/typemap-debug.log", $"ParseConnectorToInvokerType: Failed to resolve {asmRef.Name}: {ex.Message}\n");
					continue;
				}
				break;
			}
		}

		if (assembly == null && module.Assembly.Name.Name == assemblyName) {
			assembly = module.Assembly;
			if (debug) File.AppendAllText ("/tmp/typemap-debug.log", $"ParseConnectorToInvokerType: Using module.Assembly\n");
		}

		if (assembly == null) {
			if (debug) File.AppendAllText ("/tmp/typemap-debug.log", $"ParseConnectorToInvokerType: Assembly not found\n");
			return null;
		}

		// Find the type
		TypeDefinition? invokerType = null;
		foreach (var mod in assembly.Modules) {
			invokerType = FindTypeInModule (mod, typeName);
			if (invokerType != null) {
				if (debug) File.AppendAllText ("/tmp/typemap-debug.log", $"ParseConnectorToInvokerType: Found type {invokerType.FullName}\n");
				break;
			}
		}

		if (invokerType == null) {
			if (debug) File.AppendAllText ("/tmp/typemap-debug.log", $"ParseConnectorToInvokerType: Type {typeName} not found in assembly\n");
			return null;
		}

		return (invokerType, handlerMethodName);
	}

	/// <summary>
	/// Gets the native callback method name from the handler method name.
	/// Handler format: "GetOnClick_Landroid_view_View_Handler" -> Callback: "n_OnClick_Landroid_view_View_"
	/// </summary>
	static string? GetNativeCallbackNameFromHandler (string handlerMethodName, string jniName, string jniSignature)
	{
		// Handler is typically "Get{CallbackName}Handler" -> callback is "n_{callbackName}"
		if (handlerMethodName.StartsWith ("Get", StringComparison.Ordinal) && handlerMethodName.EndsWith ("Handler", StringComparison.Ordinal)) {
			// Extract the middle part: GetOnClick_Handler -> OnClick_
			string middle = handlerMethodName.Substring (3, handlerMethodName.Length - 3 - 7); // Remove "Get" and "Handler"
			return $"n_{middle}";
		}

		// Fallback pattern based on JNI name
		return null;
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
		bool isOnClickListenerImplementor = type.FullName.Contains ("IOnClickListenerImplementor");
		if (isOnClickListenerImplementor) {
			File.AppendAllText ("/tmp/typemap-debug.log", $"ProcessType: FOUND {type.FullName}, HasJavaPeer={type.HasJavaPeer (Context)}\n");
		}

		if (type.HasJavaPeer (Context)) {
			using var _ = TimingLog.StartAggregate ("ProcessType.HasJavaPeer");
			string javaName;
			using (TimingLog.StartAggregate ("ProcessType.ToJniName")) {
				javaName = JavaNativeTypeManager.ToJniName (type, Context);
			}
			if (isOnClickListenerImplementor) {
				File.AppendAllText ("/tmp/typemap-debug.log", $"ProcessType: {type.FullName} -> javaName={javaName}\n");
			}
			if (!externalMappings.TryGetValue (javaName, out var typeList)) {
				typeList = new List<TypeDefinition> ();
				externalMappings.Add (javaName, typeList);
			}
			typeList.Add (type);

			// Collect marshal methods for this type
			List<MarshalMethodInfo> marshalMethods;
			using (TimingLog.StartAggregate ("ProcessType.CollectMarshalMethods")) {
				marshalMethods = CollectMarshalMethods (type);
			}
			if (isOnClickListenerImplementor) {
				File.AppendAllText ("/tmp/typemap-debug.log", $"ProcessType: {type.FullName} collected {marshalMethods.Count} marshal methods\n");
			}
			if (marshalMethods.Count > 0) {
				marshalMethodMappings [type] = marshalMethods;
			}

			TypeDefinition proxyType;
			using (TimingLog.StartAggregate ("ProcessType.GenerateTypeMapProxyType")) {
				proxyType = GenerateTypeMapProxyType (javaName, type, marshalMethods);
			}
			typesToInject.Add (proxyType);
			proxyMappings.Add (type, proxyType);

			// For interfaces and abstract types, find their Invoker type
			if (type.IsInterface || type.IsAbstract) {
				using (TimingLog.StartAggregate ("ProcessType.GetInvokerType")) {
					var invokerType = GetInvokerType (type);
					if (invokerType != null) {
						invokerMappings [type] = invokerType;
					}
				}
			}
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
		using var _ = TimingLog.Start ("EndProcess");
		try {
		TimingLog.Log ($"EndProcess: {typesToInject.Count} types to inject, {externalMappings.Count} external mappings, {proxyMappings.Count} proxy mappings");

		using (TimingLog.Start ("EndProcess.InjectTypes")) {
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
		}

		using (TimingLog.Start ("EndProcess.GenerateTypeMapAttributes")) {
		// Generate TypeMap attributes for external mappings
		// When multiple types share the same Java name, generate an alias type
		foreach (var mapping in externalMappings) {
			var javaName = mapping.Key;
			var types = mapping.Value;

			if (types.Count == 1) {
				// Single type - simple mapping
				var proxyType = proxyMappings [types [0]];
				var attr = GenerateTypeMapAttribute (types [0], proxyType, javaName);
				AssemblyToInjectTypeMap.CustomAttributes.Add (attr);
			} else {
				// Multiple types - generate alias type and indexed mappings
				var aliasKeys = new string [types.Count];
				for (int i = 0; i < types.Count; i++) {
					aliasKeys [i] = $"{javaName}[{i}]";

					// Generate TypeMap for each aliased type: "javaName[i]" -> type
					var proxyType = proxyMappings [types [i]];
					var attr = GenerateTypeMapAttribute (types [i], proxyType, aliasKeys [i]);
					AssemblyToInjectTypeMap.CustomAttributes.Add (attr);
				}

				// Generate the alias type with [JavaInteropAliases("javaName[0]", "javaName[1]", ...)]
				var aliasType = GenerateAliasType (javaName, aliasKeys);
				AssemblyToInjectTypeMap.MainModule.Types.Add (aliasType);

				// Generate TypeMap for the main Java name -> alias type (alias types don't have proxies, use alias type itself)
				var mainAttr = GenerateTypeMapAttribute (aliasType, aliasType, javaName);
				AssemblyToInjectTypeMap.CustomAttributes.Add (mainAttr);
			}
		}
		}

		using (TimingLog.Start ("EndProcess.ApplyProxyAttributes")) {
		// Apply proxy attributes directly to target types (AOT-safe: uses GetCustomAttribute instead of Activator.CreateInstance)
		// Track which assemblies are modified so we can force them to be saved
		var modifiedAssemblies = new HashSet<AssemblyDefinition> ();
		foreach (var mapping in proxyMappings) {
			ApplyProxyAttributeToTargetType (mapping.Key, mapping.Value);
			modifiedAssemblies.Add (mapping.Key.Module.Assembly);
		}
		// Generate TypeMapAssociation<InvokerUniverse> for interface-to-invoker mappings
		foreach (var mapping in invokerMappings) {
			var attr = GenerateInvokerTypeMapAssociationAttribute (mapping.Key, mapping.Value);
			AssemblyToInjectTypeMap.CustomAttributes.Add (attr);
		}

		// JNIEnvInit sets Mono.Android as the entrypoint assembly. Forward the typemap logic to the user/custom assembly;
		CustomAttribute targetAssembly = new (TypeMapAssemblyTargetAttributeCtor);
		targetAssembly.ConstructorArguments.Add (new (SystemStringType, AssemblyToInjectTypeMap.Name.FullName));
		MonoAndroidAssembly.CustomAttributes.Add (targetAssembly);

		// Force the Linker to write the modified Mono.Android assembly
		Context.Annotations.SetAction (MonoAndroidAssembly, AssemblyAction.Save);

		// Force the Linker to write all assemblies we modified by adding proxy attributes
		foreach (var assembly in modifiedAssemblies) {
			Context.LogMessage (MessageContainer.CreateInfoMessage ($"Marking assembly '{assembly.Name.Name}' for saving due to proxy attribute modifications"));
			Context.Annotations.SetAction (assembly, AssemblyAction.Save);
		}
		}

		// Generate JCW (Java Callable Wrappers) and LLVM IR files for marshal methods
		using (TimingLog.Start ("EndProcess.GenerateJcwAndLlvmIrFiles")) {
		GenerateJcwAndLlvmIrFiles ();
		}

		TimingLog.DumpAggregates ();
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
			TimingLog.Log ("GenerateJcwAndLlvmIrFiles: JavaOutputPath or LlvmIrOutputPath not set, skipping");
			return;
		}

		TimingLog.Log ($"GenerateJcwAndLlvmIrFiles: javaOutputPath={javaOutputPath}, llvmIrOutputPath={llvmIrOutputPath}");

		// Normalize paths for the current platform
		javaOutputPath = javaOutputPath.Replace ('\\', Path.DirectorySeparatorChar);
		llvmIrOutputPath = llvmIrOutputPath.Replace ('\\', Path.DirectorySeparatorChar);

		Context.TryGetCustomData ("TargetArch", out string? targetArch);
		targetArch ??= "unknown";

		// Output JCW files to the staging directory - these will be copied after GenerateJavaStubs
		// to overwrite the old-style JCW that calls TypeManager.Activate
		string typeMapJcwOutputPath = javaOutputPath;

		TimingLog.Log ($"GenerateJcwAndLlvmIrFiles: marshalMethodMappings.Count={marshalMethodMappings.Count}");

		// Generate JCW Java and LLVM IR files for each type with marshal methods
		// JCW Java files are only generated for user types (framework JCWs come from mono.android.jar)
		// LLVM IR stubs are generated for ALL types (needed for native method binding)
		int jcwGeneratedCount = 0;
		int llvmGeneratedCount = 0;
		foreach (var kvp in marshalMethodMappings) {
			var targetType = kvp.Key;
			var marshalMethods = kvp.Value;

			if (marshalMethods.Count == 0) {
				continue;
			}

			string assemblyName = targetType.Module.Assembly.Name.Name;
			bool isFrameworkAssembly = IsFrameworkAssembly (assemblyName);
			string jniTypeName = JavaNativeTypeManager.ToJniName (targetType, Context);

			// Generate JCW Java file only for user types - framework types already have JCWs in mono.android.jar
			if (!isFrameworkAssembly) {
				jcwGeneratedCount++;
				using (TimingLog.StartAggregate ("GenerateJcwJavaFile")) {
					GenerateJcwJavaFile (typeMapJcwOutputPath, targetType, jniTypeName, marshalMethods);
				}
			}

			// Generate LLVM IR file for ALL types - framework JCWs from mono.android.jar
			// need native method stubs too (e.g., View_OnClickListenerImplementor.n_onClick)
			llvmGeneratedCount++;
			using (TimingLog.StartAggregate ("GenerateLlvmIrFile")) {
				GenerateLlvmIrFile (llvmIrOutputPath, targetArch, targetType, jniTypeName, marshalMethods);
			}
		}

		TimingLog.Log ($"GenerateJcwAndLlvmIrFiles: generated {jcwGeneratedCount} JCW files (user types) and {llvmGeneratedCount} LLVM IR files (all types)");

		// Generate the initialization file that defines get_function_pointer
		using (TimingLog.StartAggregate ("GenerateLlvmIrInitFile")) {
			GenerateLlvmIrInitFile (llvmIrOutputPath, targetArch);
		}

		// Generate the TypeManager.Activate stub for framework JCWs
		using (TimingLog.StartAggregate ("GenerateTypeManagerActivateStub")) {
			GenerateTypeManagerActivateStub (llvmIrOutputPath, targetArch);
		}
	}

	/// <summary>
	/// Checks if an assembly is a framework assembly that should not have JCW generated.
	/// Framework assemblies contain bindings to existing Java types, not user-defined types.
	/// </summary>
	static bool IsFrameworkAssembly (string assemblyName)
	{
		return assemblyName switch {
			"Mono.Android" => true,
			"Java.Interop" => true,
			"System.Private.CoreLib" => true,
			_ when assemblyName.StartsWith ("System.", StringComparison.Ordinal) => true,
			_ when assemblyName.StartsWith ("Microsoft.", StringComparison.Ordinal) => true,
			_ when assemblyName.StartsWith ("Xamarin.", StringComparison.Ordinal) => true,
			_ => false,
		};
	}

	/// <summary>
	/// Generates the marshal_methods_init.ll file that defines the get_function_pointer global
	/// and the xamarin_app_init function required by libmonodroid.so.
	/// </summary>
	void GenerateLlvmIrInitFile (string outputPath, string targetArch)
	{
		Directory.CreateDirectory (outputPath);
		string llFilePath = Path.Combine (outputPath, "marshal_methods_init.ll");

		using var writer = new StreamWriter (llFilePath);

		// Write the LLVM IR init file with xamarin_app_init function
		// This function is called by libmonodroid.so to set the typemap_get_function_pointer callback
		writer.Write ("""
; ModuleID = 'marshal_methods_init.ll'
source_filename = "marshal_methods_init.ll"
target datalayout = "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128"
target triple = "aarch64-unknown-linux-android21"

; Global typemap_get_function_pointer callback - initialized to null, set at runtime
; For CLR: set from JNIEnvInit.Initialize out parameter via host.cc
; Note: Named typemap_get_function_pointer to avoid conflict with legacy get_function_pointer symbol
@typemap_get_function_pointer = default local_unnamed_addr global ptr null, align 8

; External puts and abort for error handling
declare i32 @puts(ptr nocapture readonly) local_unnamed_addr
declare void @abort() noreturn

; Error message for null function pointer
@.str.error = private unnamed_addr constant [48 x i8] c"typemap_get_function_pointer MUST be specified\0A\00", align 1

""");
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

		// Separate constructors from regular methods
		var constructors = new List<MarshalMethodInfo> ();
		var regularMethods = new List<MarshalMethodInfo> ();

		foreach (var method in marshalMethods) {
			if (method.JniName == "<init>") {
				constructors.Add (method);
			} else if (method.JniName != "<clinit>") {
				regularMethods.Add (method);
			}
		}

		// Build constructor declarations
		var constructorDeclarations = new StringBuilder ();
		var nativeCtorDeclarations = new StringBuilder ();
		int ctorIndex = 0;

		// If no constructors with marshal methods, generate a default constructor
		if (constructors.Count == 0) {
			constructorDeclarations.AppendLine ($$"""
    // Default constructor with native activation
    public {{className}} ()
    {
        super ();
        if (getClass () == {{className}}.class) { nc_activate_0 (); }
    }
""");
			nativeCtorDeclarations.AppendLine ("    private native void nc_activate_0 ();");
		} else {
			// Generate each constructor
			foreach (var ctor in constructors) {
				string parameters = JniSignatureToJavaParameters (ctor.JniSignature);
				string parameterNames = JniSignatureToJavaParameterNames (ctor.JniSignature);

				constructorDeclarations.AppendLine ($$"""
    public {{className}} ({{parameters}})
    {
        super ();
        if (getClass () == {{className}}.class) { nc_activate_{{ctorIndex}} ({{parameterNames}}); }
    }
""");
				nativeCtorDeclarations.AppendLine ($"    private native void nc_activate_{ctorIndex} ({parameters});");
				ctorIndex++;
			}
		}

		// Build method declarations - both public wrappers and private native methods
		var publicMethods = new StringBuilder ();
		var nativeMethods = new StringBuilder ();

		foreach (var method in regularMethods) {
			string returnType = JniSignatureToJavaType (method.JniSignature, returnOnly: true);
			string parameters = JniSignatureToJavaParameters (method.JniSignature);
			string parameterNames = JniSignatureToJavaParameterNames (method.JniSignature);

			// Generate public wrapper method
			string returnStatement = returnType == "void" ? "" : "return ";
			publicMethods.AppendLine ($$"""
    public {{returnType}} {{method.JniName}} ({{parameters}})
    {
        {{returnStatement}}n_{{method.JniName}} ({{parameterNames}});
    }

""");

			// Generate private native declaration
			nativeMethods.AppendLine ($"    private native {returnType} n_{method.JniName} ({parameters});");
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
{{constructorDeclarations}}
{{publicMethods}}
{{nativeCtorDeclarations}}
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
""");

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

		// Separate constructors from regular methods for nc_activate generation
		var constructors = marshalMethods.Where (m => m.JniName == "<init>").ToList ();
		var regularMethods = marshalMethods.Where (m => m.JniName != "<init>" && m.JniName != "<clinit>").ToList ();

		// If no constructors, we still need one nc_activate for the default constructor
		int numActivateMethods = constructors.Count > 0 ? constructors.Count : 1;

		// Total function pointers: regular methods + activation methods
		int totalFnPointers = regularMethods.Count + numActivateMethods;

		// LLVM IR header
		writer.Write ($"""
			; ModuleID = 'marshal_methods_{sanitizedName}.ll'
			source_filename = "marshal_methods_{sanitizedName}.ll"
			target datalayout = "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128"
			target triple = "aarch64-unknown-linux-android21"

			; External typemap_get_function_pointer callback - resolves UCO wrapper by class name and method index
			@typemap_get_function_pointer = external local_unnamed_addr global ptr, align 8

			; Cached function pointers for marshal methods and activation

			""");

		// Class name constant (null-terminated string)
		byte[] classNameBytes = System.Text.Encoding.UTF8.GetBytes (jniTypeName);
		string classNameBytesEncoded = string.Join("", classNameBytes.Select(b => $"\\{b:X2}"));
		int classNameLength = classNameBytes.Length;

		// Cached function pointers for all methods (regular + activation)
		var fnPointers = new StringBuilder ();
		for (int i = 0; i < totalFnPointers; i++) {
			fnPointers.AppendLine ($"@fn_ptr_{i} = internal unnamed_addr global ptr null, align 8");
		}

		writer.WriteLine (fnPointers);
		writer.WriteLine ($"; Class name for \"{jniTypeName}\" (length={classNameLength})");
		writer.WriteLine ($"@class_name = internal constant [{classNameLength} x i8] c\"{classNameBytesEncoded}\", align 1");
		writer.WriteLine ();
		writer.WriteLine ("; JNI native method stubs");

		// Generate regular method stubs (indices 0..regularMethods.Count-1)
		for (int i = 0; i < regularMethods.Count; i++) {
			var method = regularMethods [i];
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
					  %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8
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
					  %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8
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

		// Generate nc_activate stubs for constructors
		// These use indices starting at regularMethods.Count
		writer.WriteLine ();
		writer.WriteLine ("; Native constructor activation stubs");

		int activateBaseIndex = regularMethods.Count;
		
		if (constructors.Count == 0) {
			// Generate default nc_activate_0 with no parameters
			string nativeSymbol = MakeJniActivateSymbol (jniTypeName, "nc_activate_0", "()V");
			int fnPtrIndex = activateBaseIndex;
			
			writer.Write ($$"""

				; nc_activate_0 - default constructor activation
				define default void @{{nativeSymbol}}(ptr %env, ptr %obj) #0 {
				entry:
				  %cached_ptr = load ptr, ptr @fn_ptr_{{fnPtrIndex}}, align 8
				  %is_null = icmp eq ptr %cached_ptr, null
				  br i1 %is_null, label %resolve, label %call

				resolve:
				  %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8
				  call void %get_fn(ptr @class_name, i32 {{classNameLength}}, i32 {{fnPtrIndex}}, ptr @fn_ptr_{{fnPtrIndex}})
				  %resolved_ptr = load ptr, ptr @fn_ptr_{{fnPtrIndex}}, align 8
				  br label %call

				call:
				  %fn = phi ptr [ %cached_ptr, %entry ], [ %resolved_ptr, %resolve ]
				  tail call void %fn(ptr %env, ptr %obj)
				  ret void
				}

				""");
		} else {
			for (int ctorIdx = 0; ctorIdx < constructors.Count; ctorIdx++) {
				var ctor = constructors [ctorIdx];
				string nativeSymbol = MakeJniActivateSymbol (jniTypeName, $"nc_activate_{ctorIdx}", ctor.JniSignature);
				string llvmParams = JniSignatureToLlvmParams (ctor.JniSignature);
				string llvmArgs = JniSignatureToLlvmArgs (ctor.JniSignature);
				int fnPtrIndex = activateBaseIndex + ctorIdx;
				
				writer.Write ($$"""

					; nc_activate_{{ctorIdx}} - constructor activation for {{ctor.JniSignature}}
					define default void @{{nativeSymbol}}(ptr %env, ptr %obj{{llvmParams}}) #0 {
					entry:
					  %cached_ptr = load ptr, ptr @fn_ptr_{{fnPtrIndex}}, align 8
					  %is_null = icmp eq ptr %cached_ptr, null
					  br i1 %is_null, label %resolve, label %call

					resolve:
					  %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8
					  call void %get_fn(ptr @class_name, i32 {{classNameLength}}, i32 {{fnPtrIndex}}, ptr @fn_ptr_{{fnPtrIndex}})
					  %resolved_ptr = load ptr, ptr @fn_ptr_{{fnPtrIndex}}, align 8
					  br label %call

					call:
					  %fn = phi ptr [ %cached_ptr, %entry ], [ %resolved_ptr, %resolve ]
					  tail call void %fn(ptr %env, ptr %obj{{llvmArgs}})
					  ret void
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
	/// Generates the TypeManager activation stub LLVM IR file.
	/// This stub provides the native JNI function Java_mono_android_TypeManager_n_1activate
	/// which is called by framework JCWs (like View_OnClickListenerImplementor) when they
	/// need to activate managed peers using TypeManager.Activate().
	/// </summary>
	void GenerateTypeManagerActivateStub (string outputPath, string targetArch)
	{
		Directory.CreateDirectory (outputPath);
		string llFilePath = Path.Combine (outputPath, "marshal_methods_typemanager.ll");

		using var writer = new StreamWriter (llFilePath);

		// The class name for TypeManager in JNI format
		const string TypeManagerClassName = "mono/android/TypeManager";
		byte[] classNameBytes = System.Text.Encoding.UTF8.GetBytes (TypeManagerClassName);
		string classNameBytesEncoded = string.Join ("", classNameBytes.Select (b => $"\\{b:X2}"));
		int classNameLength = classNameBytes.Length;

		// The JNI signature for TypeManager.n_activate:
		// native void n_activate(String typename, String signature, Object jobject, Object[] params)
		// JNI symbol: Java_mono_android_TypeManager_n_1activate (underscores in method name become _1)
		//
		// Native function parameters:
		// - ptr %env (JNIEnv*)
		// - ptr %cls (jclass - the TypeManager class)
		// - ptr %typename (jstring - the managed type name)
		// - ptr %sig (jstring - constructor signature)
		// - ptr %jobject (jobject - the Java object being activated)
		// - ptr %params (jobjectArray - constructor parameters)

		writer.Write ($$"""
; ModuleID = 'marshal_methods_typemanager.ll'
source_filename = "marshal_methods_typemanager.ll"
target datalayout = "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128"
target triple = "aarch64-unknown-linux-android21"

; External typemap_get_function_pointer callback - resolves UCO wrapper by class name and method index
@typemap_get_function_pointer = external local_unnamed_addr global ptr, align 8

; Cached function pointer for TypeManager.n_Activate_mm
@typemanager_activate_fn_ptr = internal unnamed_addr global ptr null, align 8

; Class name for "mono/android/TypeManager" (length={{classNameLength}})
@typemanager_class_name = internal constant [{{classNameLength}} x i8] c"{{classNameBytesEncoded}}", align 1

; Java_mono_android_TypeManager_n_1activate - Native JNI stub for TypeManager.Activate
; Called by framework JCWs to activate managed peers
; Parameters match TypeManager.JavaTypeManager.n_Activate_mm signature:
;   - ptr %env: JNIEnv*
;   - ptr %cls: jclass (TypeManager class)
;   - ptr %typename: jstring (managed type name like "Example.MainActivity, HelloWorld")
;   - ptr %sig: jstring (constructor signature)
;   - ptr %jobject: jobject (the Java object being activated)
;   - ptr %params: jobjectArray (constructor parameters)
define default void @Java_mono_android_TypeManager_n_1activate(ptr %env, ptr %cls, ptr %typename, ptr %sig, ptr %jobject, ptr %params) #0 {
entry:
  %cached_ptr = load ptr, ptr @typemanager_activate_fn_ptr, align 8
  %is_null = icmp eq ptr %cached_ptr, null
  br i1 %is_null, label %resolve, label %call

resolve:
  ; Call typemap_get_function_pointer to resolve TypeManager.JavaTypeManager.n_Activate_mm
  ; Method index 0 is special-cased in TypeMapAttributeTypeMap.GetFunctionPointer
  %get_fn = load ptr, ptr @typemap_get_function_pointer, align 8
  call void %get_fn(ptr @typemanager_class_name, i32 {{classNameLength}}, i32 0, ptr @typemanager_activate_fn_ptr)
  %resolved_ptr = load ptr, ptr @typemanager_activate_fn_ptr, align 8
  br label %call

call:
  %fn = phi ptr [ %cached_ptr, %entry ], [ %resolved_ptr, %resolve ]
  ; Forward all 6 parameters to the resolved n_Activate_mm function
  tail call void %fn(ptr %env, ptr %cls, ptr %typename, ptr %sig, ptr %jobject, ptr %params)
  ret void
}

attributes #0 = { nounwind }
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
	/// Creates a JNI native symbol name for nc_activate methods.
	/// These don't have the n_ prefix and use signature only for overloads.
	/// </summary>
	static string MakeJniActivateSymbol (string jniTypeName, string methodName, string jniSignature)
	{
		var sb = new StringBuilder ("Java_");
		sb.Append (MangleForJni (jniTypeName));
		sb.Append ('_');
		sb.Append (MangleForJni (methodName));
		// Add signature for overloaded constructors
		if (!string.IsNullOrEmpty (jniSignature) && jniSignature != "()V") {
			sb.Append ("__");
			sb.Append (MangleJniSignature (jniSignature));
		}
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
	/// Uses exact Java type names to ensure JNI symbol resolution matches correctly.
	/// </summary>
	static string JniSignatureToJavaParameters (string signature)
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
			string type;
			
			switch (c) {
				case 'Z':
					type = "boolean";
					idx++;
					break;
				case 'B':
					type = "byte";
					idx++;
					break;
				case 'C':
					type = "char";
					idx++;
					break;
				case 'S':
					type = "short";
					idx++;
					break;
				case 'I':
					type = "int";
					idx++;
					break;
				case 'J':
					type = "long";
					idx++;
					break;
				case 'F':
					type = "float";
					idx++;
					break;
				case 'D':
					type = "double";
					idx++;
					break;
				case 'L':
					// Extract the full class name: Ljava/lang/String; -> java.lang.String
					int start = idx + 1;
					while (idx < paramSig.Length && paramSig[idx] != ';') idx++;
					string className = paramSig.Substring (start, idx - start);
					type = className.Replace ('/', '.');
					idx++; // Skip the semicolon
					break;
				case '[':
					// Handle arrays: count dimensions and get element type
					int arrayDims = 0;
					while (idx < paramSig.Length && paramSig[idx] == '[') {
						arrayDims++;
						idx++;
					}
					
					string elementType;
					if (idx < paramSig.Length) {
						char elementChar = paramSig[idx];
						switch (elementChar) {
							case 'Z': elementType = "boolean"; idx++; break;
							case 'B': elementType = "byte"; idx++; break;
							case 'C': elementType = "char"; idx++; break;
							case 'S': elementType = "short"; idx++; break;
							case 'I': elementType = "int"; idx++; break;
							case 'J': elementType = "long"; idx++; break;
							case 'F': elementType = "float"; idx++; break;
							case 'D': elementType = "double"; idx++; break;
							case 'L':
								int elemStart = idx + 1;
								while (idx < paramSig.Length && paramSig[idx] != ';') idx++;
								string elemClassName = paramSig.Substring (elemStart, idx - elemStart);
								elementType = elemClassName.Replace ('/', '.');
								idx++; // Skip the semicolon
								break;
							default:
								elementType = "Object";
								idx++;
								break;
						}
					} else {
						elementType = "Object";
					}
					
					// Append array brackets
					type = elementType + new string ('[', arrayDims) + new string (']', arrayDims);
					break;
				default:
					type = "Object";
					idx++;
					break;
			}

			@params.Add ($"{type} p{paramNum++}");
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
	/// Generates <code>[TypeMap("javaName", typeof(proxyType), typeof(type))]</code>
	/// where proxyType is the runtime target (returned by typemap lookup) and type is the trim target (preserved by linker).
	/// </summary>
	CustomAttribute GenerateTypeMapAttribute (TypeDefinition type, TypeDefinition proxyType, string javaName)
	{
		CustomAttribute ca = new (TypeMapAttributeCtor);
		ca.ConstructorArguments.Add (new (SystemStringType, javaName));
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference(proxyType)));  // target: runtime lookup returns proxy
		ca.ConstructorArguments.Add (new (SystemTypeType, AssemblyToInjectTypeMap.MainModule.ImportReference(type)));       // trimTarget: linker preserves original type
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
		// The proxy type MUST be public so that the runtime can instantiate it when loading attributes from other assemblies
		var proxyType = new TypeDefinition (
			mappedType.Module.Assembly.Name.Name + "._." + declaringType.Namespace,
			mappedName.ToString () + "_Proxy",
			TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed,
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

		// Separate constructors and regular methods - need to track for activation UCOs
		var constructors = marshalMethods.Where (m => m.JniName == "<init>").ToList ();
		var regularMethods = marshalMethods.Where (m => m.JniName != "<init>" && m.JniName != "<clinit>").ToList ();

		// Generate UCO wrappers for regular marshal methods
		if (regularMethods.Count > 0) {
			using (TimingLog.StartAggregate ("GenerateUcoWrappers")) {
				GenerateUcoWrappers (proxyType, mappedType, regularMethods);
			}
		}

		// Generate activation UCO wrappers for constructors
		using (TimingLog.StartAggregate ("GenerateActivationUcoWrappers")) {
			GenerateActivationUcoWrappers (proxyType, mappedType, constructors);
		}

		// Always generate GetFunctionPointer as it is abstract in JavaPeerProxy
		// Pass both regular methods and constructor count for proper indexing
		using (TimingLog.StartAggregate ("GenerateGetFunctionPointerMethod")) {
			GenerateGetFunctionPointerMethod (proxyType, mappedType, regularMethods, constructors.Count > 0 ? constructors.Count : 1);
		}

		// Generate CreateInstance for AOT-safe instance creation without reflection
		using (TimingLog.StartAggregate ("GenerateCreateInstanceMethod")) {
			GenerateCreateInstanceMethod (proxyType, mappedType);
		}

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

	/// <summary>
	/// Generates [UnmanagedCallersOnly] activation wrapper methods for constructors.
	/// These wrappers create the managed object and call the constructor:
	/// 1. RuntimeHelpers.GetUninitializedObject(typeof(T))
	/// 2. ((IJavaPeerable)instance).SetPeerReference(new JniObjectReference(jobject))
	/// 3. instance..ctor(args...)
	/// </summary>
	void GenerateActivationUcoWrappers (TypeDefinition proxyType, TypeDefinition mappedType, List<MarshalMethodInfo> constructors)
	{
		// Skip interfaces and abstract classes - they can't be activated
		if (mappedType.IsInterface || mappedType.IsAbstract) {
			return;
		}

		// Skip Mono.Android and Java.Interop framework types for now - we only need activation UCOs for user types with JCWs
		// Framework JCWs use the TypeManager.Activate path which we'll handle separately
		string? assemblyName = mappedType.Module?.Assembly?.Name?.Name;
		if (assemblyName == "Mono.Android" || assemblyName == "Java.Interop") {
			return;
		}

		// Clear previous wrappers
		activationUcoWrappers.Clear ();

		// Get types we need
		var intPtrType = AssemblyToInjectTypeMap.MainModule.ImportReference (Context.GetType ("System.IntPtr"));
		var voidType = AssemblyToInjectTypeMap.MainModule.TypeSystem.Void;
		var typeType = AssemblyToInjectTypeMap.MainModule.ImportReference (Context.GetType ("System.Type"));
		var objectType = AssemblyToInjectTypeMap.MainModule.ImportReference (Context.GetType ("System.Object"));

		// Get RuntimeHelpers.GetUninitializedObject
		var runtimeHelpersType = Context.GetType ("System.Runtime.CompilerServices.RuntimeHelpers");
		var getUninitializedObjectMethod = runtimeHelpersType.Methods.FirstOrDefault (m =>
			m.Name == "GetUninitializedObject" && m.Parameters.Count == 1 && m.Parameters [0].ParameterType.FullName == "System.Type");

		if (getUninitializedObjectMethod == null) {
			return;
		}
		var getUninitializedObjectRef = AssemblyToInjectTypeMap.MainModule.ImportReference (getUninitializedObjectMethod);

		// Get IJavaPeerable.SetPeerReference
		var iJavaPeerableType = Context.GetType ("Java.Interop.IJavaPeerable");
		var setPeerReferenceMethod = iJavaPeerableType.Methods.FirstOrDefault (m =>
			m.Name == "SetPeerReference" && m.Parameters.Count == 1);

		if (setPeerReferenceMethod == null) {
			return;
		}
		var setPeerReferenceRef = AssemblyToInjectTypeMap.MainModule.ImportReference (setPeerReferenceMethod);

		// Get JniObjectReference constructor that takes IntPtr (and optional JniObjectReferenceType)
		var jniObjectRefType = Context.GetType ("Java.Interop.JniObjectReference");
		var jniObjectRefCtor = jniObjectRefType.Methods.FirstOrDefault (m =>
			m.IsConstructor && !m.IsStatic && m.Parameters.Count >= 1 && m.Parameters [0].ParameterType.FullName == "System.IntPtr");

		if (jniObjectRefCtor == null) {
			return;
		}
		var jniObjectRefCtorRef = AssemblyToInjectTypeMap.MainModule.ImportReference (jniObjectRefCtor);
		var jniObjectRefTypeRef = AssemblyToInjectTypeMap.MainModule.ImportReference (jniObjectRefType);

		// Import the mapped type reference
		var mappedTypeRef = AssemblyToInjectTypeMap.MainModule.ImportReference (mappedType);

		// If no constructors, generate a default activation for parameterless ctor
		if (constructors.Count == 0) {
			// Find the default constructor on mappedType
			var defaultCtor = mappedType.Methods.FirstOrDefault (m =>
				m.IsConstructor && !m.IsStatic && m.Parameters.Count == 0);

			if (defaultCtor != null) {
				File.AppendAllText ("/tmp/illink-debug.log",
					$"Found default ctor on {mappedType.FullName}: {defaultCtor.FullName}\n");
				var wrapper = GenerateSingleActivationUco (
					proxyType, mappedType, mappedTypeRef, defaultCtor,
					intPtrType, voidType, typeType, objectType,
					getUninitializedObjectRef, setPeerReferenceRef, jniObjectRefCtorRef, jniObjectRefTypeRef,
					0, "()V");
				if (wrapper != null) {
					activationUcoWrappers.Add (wrapper);
					proxyType.Methods.Add (wrapper);
					File.AppendAllText ("/tmp/illink-debug.log",
						$"Generated activation UCO for {mappedType.FullName}, activationUcoWrappers.Count={activationUcoWrappers.Count}\n");
				} else {
					File.AppendAllText ("/tmp/illink-debug.log",
						$"GenerateSingleActivationUco returned null for {mappedType.FullName}\n");
				}
			} else {
				File.AppendAllText ("/tmp/illink-debug.log",
					$"No default ctor found on {mappedType.FullName}, methods: {string.Join(", ", mappedType.Methods.Select(m => m.Name))}\n");
			}
		} else {
			// Generate activation for each constructor
			for (int i = 0; i < constructors.Count; i++) {
				var ctorInfo = constructors [i];

				// Find the matching constructor on mappedType
				var targetCtor = ctorInfo.RegisteredMethod as MethodDefinition;
				if (targetCtor == null || !targetCtor.IsConstructor) {
					// Try to find by signature match
					// For now, just use the registered method if it's a constructor
					continue;
				}

				var wrapper = GenerateSingleActivationUco (
					proxyType, mappedType, mappedTypeRef, targetCtor,
					intPtrType, voidType, typeType, objectType,
					getUninitializedObjectRef, setPeerReferenceRef, jniObjectRefCtorRef, jniObjectRefTypeRef,
					i, ctorInfo.JniSignature);
				if (wrapper != null) {
					activationUcoWrappers.Add (wrapper);
					proxyType.Methods.Add (wrapper);
				}
			}
		}
	}

	/// <summary>
	/// Generates a single activation UCO wrapper method.
	/// This method is called from Java JCW constructors to create and activate managed peers.
	/// 
	/// Generated IL equivalent:
	/// <code>
	/// [UnmanagedCallersOnly]
	/// static void nc_activate_N(IntPtr jnienv, IntPtr jobject, ...)
	/// {
	///     // Check 1: Skip if we're within NewObjectScope (managed code is creating the Java object)
	///     if (JniEnvironment.WithinNewObjectScope)
	///         return;
	///     
	///     // Check 2: Skip if a peer already exists for this Java object
	///     if (Java.Lang.Object.PeekObject(jobject) != null)
	///         return;
	///     
	///     // Create uninitialized object and set peer reference
	///     var instance = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
	///     ((IJavaPeerable)instance).SetPeerReference(new JniObjectReference(jobject));
	///     
	///     // Call the actual constructor
	///     instance..ctor(args...);
	/// }
	/// </code>
	/// </summary>
	MethodDefinition? GenerateSingleActivationUco (
		TypeDefinition proxyType, TypeDefinition mappedType, TypeReference mappedTypeRef, MethodDefinition targetCtor,
		TypeReference intPtrType, TypeReference voidType, TypeReference typeType, TypeReference objectType,
		MethodReference getUninitializedObjectRef, MethodReference setPeerReferenceRef,
		MethodReference jniObjectRefCtorRef, TypeReference jniObjectRefTypeRef,
		int ctorIndex, string jniSignature)
	{
		try {
			string wrapperName = $"nc_activate_{ctorIndex}";

			var wrapperMethod = new MethodDefinition (
				wrapperName,
				MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
				voidType);

			// Add [UnmanagedCallersOnly] attribute
			wrapperMethod.CustomAttributes.Add (new CustomAttribute (UnmanagedCallersOnlyAttributeCtor));

			// Parameters: (IntPtr jnienv, IntPtr jobject, ...constructor params...)
			wrapperMethod.Parameters.Add (new ParameterDefinition ("jnienv", ParameterAttributes.None, intPtrType));
			wrapperMethod.Parameters.Add (new ParameterDefinition ("jobject", ParameterAttributes.None, intPtrType));

			var body = wrapperMethod.Body;
			var il = body.GetILProcessor ();

			// TEMP: Just return for now to test if linker is happy with simple method
			il.Emit (Mono.Cecil.Cil.OpCodes.Ret);

			return wrapperMethod;
		} catch (Exception ex) {
			Context.LogMessage (MessageContainer.CreateInfoMessage (
				$"Failed to generate activation UCO for {mappedType.FullName}..ctor: {ex.Message}"));
			return null;
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
			Context.LogMessage (MessageContainer.CreateInfoMessage (
				$"Failed to generate UCO wrapper for {callback.FullName}: {ex.Message}"));
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
	// Store activation UCO wrappers for use in GetFunctionPointer
	List<MethodDefinition> activationUcoWrappers = new ();

	void GenerateGetFunctionPointerMethod (TypeDefinition proxyType, TypeDefinition mappedType, List<MarshalMethodInfo> marshalMethods, int numActivationMethods)
	{
		// Get IntPtr type (use cached SystemIntPtrType instead of Context.GetType every time)
		var intPtrTypeDef = _cachedIntPtrTypeDef ??= Context.GetType ("System.IntPtr");
		var intPtrType = AssemblyToInjectTypeMap.MainModule.ImportReference (intPtrTypeDef);

		// Get IntPtr.Zero field (cache it)
		if (_cachedIntPtrZeroField == null) {
			_cachedIntPtrZeroField = intPtrTypeDef.Fields.FirstOrDefault (f => f.Name == "Zero")
				?? throw new InvalidOperationException ("Could not find IntPtr.Zero");
		}
		var intPtrZeroRef = AssemblyToInjectTypeMap.MainModule.ImportReference (_cachedIntPtrZeroField);

		// Get Int32 type (cache it)
		var int32TypeDef = _cachedInt32TypeDef ??= Context.GetType ("System.Int32");
		var int32Type = AssemblyToInjectTypeMap.MainModule.ImportReference (int32TypeDef);

		// Create the override method
		var method = new MethodDefinition (
			"GetFunctionPointer",
			MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
			intPtrType);

		method.Parameters.Add (new ParameterDefinition ("methodIndex", ParameterAttributes.None, int32Type));

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

		// Add activation method entries (indices start after regular methods)
		int activateBaseIndex = marshalMethods.Count;
		for (int i = 0; i < activationUcoWrappers.Count; i++) {
			var wrapper = activationUcoWrappers [i];
			int methodIndex = activateBaseIndex + i;

			// Load methodIndex argument
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldarg_1);
			// Load constant methodIndex
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldc_I4, methodIndex);
			// Compare: if (methodIndex != index) goto next
			var nextLabel = il.Create (Mono.Cecil.Cil.OpCodes.Nop);
			il.Emit (Mono.Cecil.Cil.OpCodes.Bne_Un, nextLabel);

			// Load function pointer for the activation UCO wrapper method
			il.Emit (Mono.Cecil.Cil.OpCodes.Ldftn, wrapper);
			// Return it
			il.Emit (Mono.Cecil.Cil.OpCodes.Ret);

			// next:
			il.Append (nextLabel);
		}

		// return IntPtr.Zero
		il.Append (returnZeroLabel);
		il.Emit (Mono.Cecil.Cil.OpCodes.Ret);

		proxyType.Methods.Add (method);

		// Clear the activation wrappers for the next type
		activationUcoWrappers.Clear ();

		// Context.LogMessage (MessageContainer.CreateInfoMessage (
			// $"Generated GetFunctionPointer for {proxyType.FullName} with {marshalMethods.Count} methods + {numActivationMethods} activation methods"));
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
