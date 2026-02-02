#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Android.OS;
using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Runtime;

namespace Android.Runtime
{
	/// <summary>
	/// Provides type mappings for Java-to-.NET type resolution using compile-time generated attributes.
	/// Used for NativeAOT and CoreCLR runtimes where the Type Mapping API is available.
	/// </summary>
	class TypeMapAttributeTypeMap : ITypeMap
	{
		const string TypeMapsAssemblyName = "_Microsoft.Android.TypeMaps";

		readonly IReadOnlyDictionary<string, Type> _externalTypeMap;

		readonly ConcurrentDictionary<Type, JavaPeerProxy?> _proxyInstances = new ();
		readonly ConcurrentDictionary<Type, Type[]?> _aliasCache = new ();
		readonly ConcurrentDictionary<Type, string> _jniNameCache = new ();
		readonly ConcurrentDictionary<string, Type?> _classToTypeCache = new ();

		/// <summary>
		/// Logs a TypeMap trace message if typemap tracing is enabled.
		/// </summary>
		static void LogTrace (string message)
		{
			if (Logger.LogTypemapTrace) {
				Logger.Log (LogLevel.Info, "monodroid-typemap", message);
			}
		}

		/// <summary>
		/// Logs a TypeMap warning message if typemap tracing is enabled.
		/// </summary>
		static void LogWarn (string message)
		{
			if (Logger.LogTypemapTrace) {
				Logger.Log (LogLevel.Warn, "monodroid-typemap", message);
			}
		}

		public TypeMapAttributeTypeMap ()
		{
			WorkaroundForILLink ();

			if (RuntimeFeature.IsCoreClrRuntime) {
				_externalTypeMap = TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object> ();
			} else {
				_externalTypeMap = WorkaroundForMonoCollectTypeMapEntries ();
			}

			// DO NOT REMOVE: This method is used to correctly load type maps until we get newer
			// builds of the runtime which automatically loads it based on AppContext settings.
			void WorkaroundForILLink ()
			{
				// Load the TypeMaps assembly and set it as the entry assembly so that
				// TypeMapLazyDictionary will scan it for TypeMapAttribute entries.
				var typeMapsAssembly = Assembly.Load (TypeMapsAssemblyName);
				LogTrace ($"Loaded TypeMaps assembly: {typeMapsAssembly.FullName}");

				Assembly.SetEntryAssembly (typeMapsAssembly);
				LogTrace ("SetEntryAssembly called successfully");
			}

			// Workaround for Mono VM - scans the generated type maps assembly attributes
			// We parse the attributes manually because:
			// 1. TypeMapAttribute<T> doesn't expose its values as public properties
			// 2. GetCustomAttributesData fails when 3rd argument references types in the same assembly
			static IReadOnlyDictionary<string, Type> WorkaroundForMonoCollectTypeMapEntries ()
			{
				var typeMapsAssembly = Assembly.Load (TypeMapsAssemblyName);
				var result = new Dictionary<string, Type> (StringComparer.Ordinal);
				var typeMapAttrType = typeof (TypeMapAttribute<>);

				foreach (var attrData in typeMapsAssembly.GetCustomAttributesData ()) {
					// Check if this is a TypeMapAttribute<T> (generic type)
					if (!attrData.AttributeType.IsGenericType)
						continue;
					if (attrData.AttributeType.GetGenericTypeDefinition () != typeMapAttrType)
						continue;

					try {
						// TypeMapAttribute has 2 or 3 arguments:
						// - string value (JNI name)
						// - Type target
						// - Type trimTarget (optional)
						var args = attrData.ConstructorArguments;
						if (args.Count < 2)
							continue;

						var jniName = args[0].Value as string;
						var targetType = args[1].Value as Type;

						if (jniName != null && targetType != null) {
							result[jniName] = targetType;
						}
					} catch (Exception ex) {
						// Skip entries that fail to resolve (e.g., alias holder types)
						LogTrace ($"Skipping TypeMapAttribute that failed to resolve: {ex.Message}");
					}
				}

				LogTrace ($"Collected {result.Count} TypeMap entries for MonoVM");
				return result;
			}
		}

		/// <inheritdoc/>
		public Type? TryGetExactTypeMapping (string jniTypeName)
		{
			_externalTypeMap.TryGetValue (jniTypeName, out Type? type);
			return type;
		}

		/// <inheritdoc/>
		public bool TryGetTypesForJniName (string jniSimpleReference, [NotNullWhen (true)] out IEnumerable<Type>? types)
		{
			if (!_externalTypeMap.TryGetValue (jniSimpleReference, out Type? type)) {
				types = null;
				return false;
			}

			// Use GetOrAdd for thread-safe caching
			var cachedAliases = _aliasCache.GetOrAdd (type, t => ResolveAliases (t, _externalTypeMap));
			types = cachedAliases ?? [type];
			return true;

			static Type[]? ResolveAliases (Type type, IReadOnlyDictionary<string, Type> externalTypeMap)
			{
				var aliasesAttr = type.GetCustomAttribute<JavaInteropAliasesAttribute> ();
				if (aliasesAttr == null) {
					return null;
				}

				var aliasedTypes = new List<Type> ();
				foreach (var aliasKey in aliasesAttr.AliasKeys) {
					if (externalTypeMap.TryGetValue (aliasKey, out Type? aliasedType)) {
						aliasedTypes.Add (aliasedType);
					}
				}
				return aliasedTypes.Count > 0 ? aliasedTypes.ToArray () : null;
			}
		}

		/// <inheritdoc/>
		public bool TryGetJniNameForType (Type type, [NotNullWhen (true)] out string? jniName)
		{
			// Use GetOrAdd for thread-safe caching (empty string = not found)
			var cached = _jniNameCache.GetOrAdd (type, ComputeJniNameForType);
			if (cached.Length == 0) {
				jniName = null;
				return false;
			}
			jniName = cached;
			return true;
		}

		string ComputeJniNameForType (Type type)
		{
			// 1. Try to get explicit JNI name from [Register] attribute (or any IJniNameProviderAttribute)
			var attrs = type.GetCustomAttributes (typeof (IJniNameProviderAttribute), inherit: false);
			if (attrs.Length > 0 && attrs[0] is IJniNameProviderAttribute jniNameProvider && !string.IsNullOrEmpty (jniNameProvider.Name)) {
				return jniNameProvider.Name.Replace ('.', '/');
			}

			// 2. Fallback: use [JniTypeSignature] if present
			var sigAttr = type.GetCustomAttribute<JniTypeSignatureAttribute> (inherit: false);
			if (sigAttr != null && !string.IsNullOrEmpty (sigAttr.SimpleReference)) {
				return sigAttr.SimpleReference;
			}

			// 3. Fallback: derive JNI name using naming conventions
			var jniName = JavaNativeTypeManager.ToJniName (type);
			return string.IsNullOrEmpty (jniName) ? "" : jniName;
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetJniNamesForType (Type type)
		{
			if (TryGetJniNameForType (type, out string? jniName)) {
				return [jniName];
			}
			return [];
		}

		/// <inheritdoc/>
		public bool TryGetInvokerType (Type type, [NotNullWhen (true)] out Type? invokerType)
		{
			// Look up the proxy for the interface/abstract class
			var proxy = GetProxyForManagedType (type);
			if (proxy?.InvokerType != null) {
				invokerType = proxy.InvokerType;
				return true;
			}

			invokerType = null;
			return false;
		}

		/// <inheritdoc/>
		public JavaPeerProxy? GetProxyForManagedType (Type managedType)
		{
			// First check if the type itself has the proxy attribute
			var proxy = GetProxyForType (managedType);
			if (proxy != null) {
				return proxy;
			}

			// Try to find the proxy type from the TypeMap by JNI name
			if (TryGetJniNameForType (managedType, out string? jniName)) {
				if (_externalTypeMap.TryGetValue (jniName, out Type? proxyType)) {
					return GetProxyForType (proxyType);
				}
			}

			return null;
		}

		/// <summary>
		/// Gets or creates a cached JavaPeerProxy instance for the given type.
		/// </summary>
		JavaPeerProxy? GetProxyForType (Type type)
		{
			LogTrace ($"GetProxyForType: Looking for proxy on type {type.FullName}");
			return _proxyInstances.GetOrAdd (type, static t => t.GetCustomAttribute<JavaPeerProxy> (inherit: false));
		}

		/// <inheritdoc/>
		public IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType)
		{
			return PeerCreationHelper.CreatePeer (
				handle,
				transfer,
				targetType,
				typeMap: this,
				typeResolver: ResolveTypeWithCaching,
				instanceCreator: CreateInstance,
				resolveInvokerTypes: true);
		}

		/// <summary>
		/// Resolves type from class pointer with caching and array type handling.
		/// </summary>
		Type? ResolveTypeWithCaching (IntPtr class_ptr, string class_name)
		{
			return _classToTypeCache.GetOrAdd (class_name, _ => FindTypeInHierarchy (class_ptr, class_name));
		}

		/// <summary>
		/// Creates an instance using the proxy-based activation.
		/// </summary>
		IJavaPeerable? CreateInstance (Type type, IntPtr handle, JniHandleOwnership transfer)
		{
			var proxy = GetProxyForType (type);
			if (proxy is null) {
				// Try looking up via managed type mapping
				proxy = GetProxyForManagedType (type);
			}

			if (proxy is null) {
				// Log error and return null - caller will throw appropriate exception
				LogWarn ($"Activation failed for {type.FullName}: No [JavaPeerProxy] attribute found.");
				return null;
			}

			LogTrace ($"CreateInstance: Using proxy {proxy.GetType ().FullName} to create instance for {type.FullName}");
			return proxy.CreateInstance (handle, transfer);
		}

		/// <summary>
		/// Walks the Java class hierarchy starting from <paramref name="class_ptr"/> to find a registered .NET type.
		/// </summary>
		/// <param name="class_ptr">The starting Java class pointer (not deleted by this method).</param>
		/// <param name="class_name">The JNI name of the starting class.</param>
		/// <returns>The found .NET type, or null if no type is registered for any class in the hierarchy.</returns>
		Type? FindTypeInHierarchy (IntPtr class_ptr, string class_name)
		{
			if (class_name.StartsWith ("[", StringComparison.Ordinal)) {
				return GetArrayType (class_name);
			}

			return JavaHierarchyWalker.WalkHierarchy (class_ptr, class_name, this);
		}

		Type? GetArrayType (string jniName)
		{
			if (jniName.StartsWith ("[[", StringComparison.Ordinal)) {
				// Nested arrays not supported yet
				return null;
			}

			// Primitive arrays use hardcoded types (no TypeMap lookup needed)
			switch (jniName) {
				case "[Z": return typeof (JavaArray<bool>);
				case "[B": return typeof (JavaArray<byte>);
				case "[C": return typeof (JavaArray<char>);
				case "[S": return typeof (JavaArray<short>);
				case "[I": return typeof (JavaArray<int>);
				case "[J": return typeof (JavaArray<long>);
				case "[F": return typeof (JavaArray<float>);
				case "[D": return typeof (JavaArray<double>);
			}

			// Object arrays: lookup in typemap for pre-generated JavaObjectArray<T> proxy
			// The generator creates entries like: [Landroid/view/View; -> JavaObjectArray<View>
			if (_externalTypeMap.TryGetValue (jniName, out Type? arrayType)) {
				LogTrace ($"GetArrayType: Found {jniName} -> {arrayType.FullName}");
				return arrayType;
			}

			// Fallback for java/lang/String arrays
			if (jniName.StartsWith ("[L", StringComparison.Ordinal) && jniName.EndsWith (";", StringComparison.Ordinal)) {
				string elementJni = jniName.Substring (2, jniName.Length - 3);
				if (string.Equals (elementJni, "java/lang/String", StringComparison.Ordinal))
					return typeof (JavaArray<string>);
			}

			// No entry found - this means the element type isn't in our typemap
			LogWarn ($"GetArrayType: No typemap entry for {jniName}");
			return null;
		}

		/// <inheritdoc/>
		public IntPtr GetFunctionPointer (ReadOnlySpan<char> className, int methodIndex)
		{
			string classNameStr = className.ToString ();

			LogTrace ($"GetFunctionPointer called: className='{classNameStr}', methodIndex={methodIndex}");

			// Called once per function pointer during native method registration.
			// Result is cached in LLVM IR globals, so no managed-side caching needed.
			if (_externalTypeMap.TryGetValue (classNameStr, out Type? type)) {
				LogTrace ($"  -> Found type: {type?.FullName ?? "NULL"}");
				JavaPeerProxy? proxy = GetProxyForType (type!);
				if (proxy is IAndroidCallableWrapper acw) {
					// Only ACW types have GetFunctionPointer - they have generated Java classes that call back
					LogTrace ($"  -> Got ACW proxy: {proxy.GetType ().FullName}");
					var result = acw.GetFunctionPointer (methodIndex);
					LogTrace ($"  -> Function pointer: 0x{result:X}");
					return result;
				} else if (proxy is null) {
					throw new TypeMapException (
						$"XA4302: No proxy found for type '{type!.FullName}' when looking up function pointer for '{classNameStr}' at index {methodIndex}. " +
						"Ensure the type has [JavaPeerProxy] attribute.");
				} else {
					// MCW types don't have GetFunctionPointer - they only wrap existing Java classes
					throw new TypeMapException (
						$"XA4303: Type '{type!.FullName}' is an MCW (managed callable wrapper) and does not have function pointers. " +
						$"Requested function pointer for '{classNameStr}' at index {methodIndex}.");
				}
			} else {
				throw new TypeMapException (
					$"XA4301: Type lookup failed for '{classNameStr}'. " +
					"Ensure the type has [Register] attribute and is not trimmed away.");
			}
		}

		/// <summary>
		/// Creates a 1D .NET array (T[]) of the specified element type using the pre-generated proxy.
		/// This is AOT-safe - no Type.MakeGenericType or Array.CreateInstance at runtime.
		/// </summary>
		/// <param name="elementType">The element type of the array. May be T or T[] for nested arrays.</param>
		/// <param name="length">The length of the array.</param>
		/// <param name="rank">The array rank: 1 for T[], 2 for T[][], 3 for T[][][]. Default is 1.</param>
		/// <returns>A new array of the specified type and length.</returns>
		/// <exception cref="ArgumentOutOfRangeException">Thrown if rank is not 1, 2, or 3.</exception>
		/// <exception cref="InvalidOperationException">Thrown if no proxy is registered for the element type.</exception>
		public Array CreateArray (Type elementType, int length, int rank)
		{
			if (rank < 1 || rank > 3) {
				throw new ArgumentOutOfRangeException (nameof (rank), rank, "Rank must be 1, 2, or 3");
			}

			// Handle nested arrays: if elementType is T[], unwrap and bump rank
			if (elementType.IsArray) {
				var innerType = elementType.GetElementType ();
				if (innerType == null) {
					throw new InvalidOperationException ($"Cannot get element type from array type {elementType.FullName}");
				}
				return CreateArray (innerType, length, rank + 1);
			}

			// 1. Get JNI name for element type
			if (!TryGetJniNameForType (elementType, out string? jniName)) {
				throw new InvalidOperationException ($"No JNI name found for element type {elementType.FullName}");
			}

			// 2. Look up proxy for element type
			if (!_externalTypeMap.TryGetValue (jniName, out Type? proxyType)) {
				throw new InvalidOperationException ($"No proxy registered for {jniName}");
			}

			// 3. Get cached proxy instance and use factory to create array
			var proxy = GetProxyForType (proxyType);
			if (proxy == null) {
				throw new InvalidOperationException ($"No proxy instance for {proxyType.FullName}");
			}

			var factory = proxy.GetDerivedTypeFactory ();
			return factory.CreateArray (length, rank);
		}
	}
}
