#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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

		static int _instanceCounter = 0;

		public TypeMapAttributeTypeMap ()
		{
			var instanceId = ++_instanceCounter;
			var stackTrace = Environment.StackTrace;
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"TypeMapAttributeTypeMap constructor starting (instance #{instanceId})");
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"Stack trace for instance #{instanceId}: {stackTrace}");
			
			if (RuntimeFeature.IsNativeAotRuntime) {
				// For NativeAOT, use the .NET 10+ TypeMapping API which is an intrinsic
				// that ILC recognizes and generates the type map at compile time.
				_externalTypeMap = TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object> ();
				// Note: Cannot call .Count on the lazy dictionary - it throws NotSupportedException
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"TypeMap: NativeAOT mode, initialized from TypeMapping API (instance #{instanceId})");
			} else {
				// For MonoVM/CoreCLR, we need to load the assembly and scan for TypeMapAttribute entries.
				SetEntryAssemblyWorkaround ();
				_externalTypeMap = CollectTypeMapEntriesForMonoVM ();
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"TypeMap: MonoVM/CoreCLR mode, got {_externalTypeMap.Count} entries from assembly scan");
			}
		}

		[RequiresUnreferencedCode ("MonoVM type map uses Assembly.Load which is not trim-safe")]
		static void SetEntryAssemblyWorkaround ()
		{
			var typeMapsAssembly = Assembly.Load (TypeMapsAssemblyName);
			Assembly.SetEntryAssembly (typeMapsAssembly);
		}

		[RequiresUnreferencedCode ("MonoVM type map uses Assembly.Load and reflection which are not trim-safe")]
		static IReadOnlyDictionary<string, Type> CollectTypeMapEntriesForMonoVM ()
		{
			var typeMapsAssembly = Assembly.Load (TypeMapsAssemblyName);
			return CollectTypeMapEntries (typeMapsAssembly);
		}

		static IReadOnlyDictionary<string, Type> CollectTypeMapEntries (Assembly typeMapsAssembly)
		{
			var result = new Dictionary<string, Type> (StringComparer.Ordinal);
			var typeMapAttrType = typeof (TypeMapAttribute<>);

			foreach (var attrData in typeMapsAssembly.GetCustomAttributesData ()) {
				if (!attrData.AttributeType.IsGenericType)
					continue;
				if (attrData.AttributeType.GetGenericTypeDefinition () != typeMapAttrType)
					continue;

				try {
					var args = attrData.ConstructorArguments;
					if (args.Count < 2)
						continue;

					var jniName = args[0].Value as string;
					var targetType = args[1].Value as Type;

					if (jniName != null && targetType != null) {
						result[jniName] = targetType;
					}
				} catch {
					// Skip entries that fail to resolve
				}
			}

			return result;
		}

		/// <inheritdoc/>
		public Type? TryGetExactTypeMapping (string jniTypeName)
		{
			if (!_externalTypeMap.TryGetValue (jniTypeName, out Type? proxyType)) {
				return null;
			}

			// The external type map contains proxy types (JavaPeerProxy subclasses).
			// We need to return the TargetType from the proxy, not the proxy itself.
			var proxy = GetProxyForType (proxyType);
			if (proxy?.TargetType != null) {
				return proxy.TargetType;
			}

			// Fallback: if proxy doesn't have TargetType, return the type as-is
			return proxyType;
		}

		/// <inheritdoc/>
		public bool TryGetTypesForJniName (string jniSimpleReference, [NotNullWhen (true)] out IEnumerable<Type>? types)
		{
			if (!_externalTypeMap.TryGetValue (jniSimpleReference, out Type? type)) {
				types = null;
				return false;
			}

			var cachedAliases = _aliasCache.GetOrAdd (type, t => ResolveAliases (t, _externalTypeMap));
			types = cachedAliases ?? [type];
			return true;
		}

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

		/// <inheritdoc/>
		public bool TryGetJniNameForType (Type type, [NotNullWhen (true)] out string? jniName)
		{
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"TryGetJniNameForType called for: {type.FullName}");
			// Use GetOrAdd for thread-safe caching (empty string = not found)
			var cached = _jniNameCache.GetOrAdd (type, ComputeJniNameForType);
			if (cached.Length == 0) {
				Logger.Log (LogLevel.Warn, "monodroid-typemap", $"TryGetJniNameForType: {type.FullName} NOT FOUND");
				jniName = null;
				return false;
			}
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"TryGetJniNameForType: {type.FullName} -> {cached}");
			jniName = cached;
			return true;
		}

		string ComputeJniNameForType (Type type)
		{
			var allAttrs = type.GetCustomAttributes (inherit: false);
			var attrNames = string.Join (", ", allAttrs.Select (a => a.GetType ().Name).ToArray ());
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"ComputeJniNameForType: {type.FullName} has {allAttrs.Length} attrs: {attrNames}");

			var attrs = type.GetCustomAttributes (typeof (IJniNameProviderAttribute), inherit: false);
			if (attrs.Length > 0 && attrs[0] is IJniNameProviderAttribute jniNameProvider && !string.IsNullOrEmpty (jniNameProvider.Name)) {
				var jniName = jniNameProvider.Name.Replace ('.', '/');
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"ComputeJniNameForType: {type.FullName} -> {jniName} (via IJniNameProviderAttribute)");
				return jniName;
			}

			var sigAttr = type.GetCustomAttribute<JniTypeSignatureAttribute> (inherit: false);
			if (sigAttr != null && !string.IsNullOrEmpty (sigAttr.SimpleReference)) {
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"ComputeJniNameForType: {type.FullName} -> {sigAttr.SimpleReference} (via JniTypeSignatureAttribute)");
				return sigAttr.SimpleReference;
			}

			var jniNameFromManager = JavaNativeTypeManager.ToJniName (type);
			if (!string.IsNullOrEmpty (jniNameFromManager)) {
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"ComputeJniNameForType: {type.FullName} -> {jniNameFromManager} (via JavaNativeTypeManager)");
				return jniNameFromManager;
			}

			Logger.Log (LogLevel.Warn, "monodroid-typemap", $"ComputeJniNameForType: {type.FullName} -> NOT FOUND");
			return "";
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
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"TryGetInvokerType: Looking for invoker for {type.FullName}");
			// Look up the proxy for the interface/abstract class
			var proxy = GetProxyForManagedType (type);
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"TryGetInvokerType: proxy={proxy?.GetType().FullName ?? "null"}, InvokerType={proxy?.InvokerType?.FullName ?? "null"}");
			if (proxy?.InvokerType != null) {
				invokerType = proxy.InvokerType;
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"TryGetInvokerType: Found invoker {invokerType.FullName}");
				return true;
			}

			invokerType = null;
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"TryGetInvokerType: No invoker found for {type.FullName}");
			return false;
		}

		/// <inheritdoc/>
		public JavaPeerProxy? GetProxyForManagedType (Type managedType)
		{
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetProxyForManagedType: Looking for proxy for {managedType.FullName}");
			var proxy = GetProxyForType (managedType);
			if (proxy != null) {
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetProxyForManagedType: Found direct proxy {proxy.GetType().FullName}");
				return proxy;
			}

			Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetProxyForManagedType: No direct proxy, trying JNI name lookup");
			if (TryGetJniNameForType (managedType, out string? jniName)) {
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetProxyForManagedType: JNI name = {jniName}");
				if (_externalTypeMap.TryGetValue (jniName, out Type? proxyType)) {
					Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetProxyForManagedType: Found proxy type {proxyType.FullName} in external map");
					return GetProxyForType (proxyType);
				}
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetProxyForManagedType: JNI name {jniName} not in external map");
			} else {
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetProxyForManagedType: Could not get JNI name for {managedType.FullName}");
			}

			Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetProxyForManagedType: No proxy found for {managedType.FullName}");
			return null;
		}

		JavaPeerProxy? GetProxyForType (Type type)
		{
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetProxyForType: Looking for JavaPeerProxy attribute on {type.FullName}");
			var proxy = _proxyInstances.GetOrAdd (type, static t => {
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetProxyForType: Cache miss, calling GetCustomAttribute for {t.FullName}");
				var attrs = t.GetCustomAttributes (typeof(JavaPeerProxy), inherit: false);
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetProxyForType: GetCustomAttributes returned {attrs?.Length ?? 0} attributes");
				if (attrs != null && attrs.Length > 0) {
					Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetProxyForType: First attribute type: {attrs[0]?.GetType().FullName ?? "null"}");
				}
				return t.GetCustomAttribute<JavaPeerProxy> (inherit: false);
			});
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetProxyForType: Returning {(proxy != null ? proxy.GetType().FullName : "null")} for {type.FullName}");
			return proxy;
		}

		/// <inheritdoc/>
		public IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType)
		{
			global::Android.Util.Log.Info ("TypeMapV3", $"CreatePeer ENTRY: targetType={targetType?.FullName ?? "null"}");
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
			global::Android.Util.Log.Info ("TypeMapV3", $"ResolveTypeWithCaching: class_name={class_name}");
			return _classToTypeCache.GetOrAdd (class_name, _ => FindTypeInHierarchy (class_ptr, class_name));
		}

		IJavaPeerable? CreateInstance (Type type, IntPtr handle, JniHandleOwnership transfer)
		{
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"CreateInstance ENTRY: type={type.FullName}, handle=0x{handle:x}");
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"CreateInstance: Type assembly: {type.Assembly.FullName}");
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"CreateInstance: Type IsAbstract={type.IsAbstract}, IsInterface={type.IsInterface}, BaseType={type.BaseType?.FullName ?? "null"}");
			
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"CreateInstance: Looking for proxy for type {type.FullName}");
			
			// First, try to get the proxy directly for this type
			var proxy = GetProxyForType (type);
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"CreateInstance: GetProxyForType returned: {(proxy != null ? proxy.GetType().FullName : "null")}");
			
			// If not found directly, try via JNI name lookup (for types in external map)
			if (proxy is null) {
				proxy = GetProxyForManagedType (type);
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"CreateInstance: GetProxyForManagedType returned: {(proxy != null ? proxy.GetType().FullName : "null")}");
			}
			
			if (proxy is null) {
				Logger.Log (LogLevel.Warn, "monodroid-typemap", $"CreateInstance: No proxy found for {type.FullName}, returning null");
				return null;
			}

			Logger.Log (LogLevel.Info, "monodroid-typemap", $"CreateInstance: proxy {proxy.GetType().FullName}, InvokerType={proxy.InvokerType?.FullName ?? "null"}, TargetType={proxy.TargetType?.FullName ?? "null"}");
			
			try {
				// CreateInstance for abstract/interface proxies automatically creates the invoker type
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"CreateInstance: CALLING proxy.CreateInstance NOW for {type.FullName}...");
				var result = proxy.CreateInstance (handle, transfer);
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"CreateInstance: proxy.CreateInstance RETURNED, result={result?.GetType().FullName ?? "null"}");
				return result;
			} catch (Exception ex) {
				Logger.Log (LogLevel.Error, "monodroid-typemap", $"CreateInstance: proxy.CreateInstance THREW: {ex.GetType().Name}: {ex.Message}");
				Logger.Log (LogLevel.Error, "monodroid-typemap", $"CreateInstance: Stack trace: {ex.StackTrace}");
				throw;
			}
		}

		/// <summary>
		/// Walks the Java class hierarchy to find a registered .NET type.
		/// </summary>
		Type? FindTypeInHierarchy (IntPtr class_ptr, string class_name)
		{
			if (class_name.StartsWith ("[", StringComparison.Ordinal)) {
				return GetArrayType (class_name);
			}

			return PeerCreationHelper.WalkHierarchy (class_ptr, class_name, this);
		}

		Type? GetArrayType (string jniName)
		{
			if (jniName.StartsWith ("[[", StringComparison.Ordinal)) {
				return null; // Nested arrays not supported
			}

			return jniName switch {
				"[Z" => typeof (JavaArray<bool>),
				"[B" => typeof (JavaArray<byte>),
				"[C" => typeof (JavaArray<char>),
				"[S" => typeof (JavaArray<short>),
				"[I" => typeof (JavaArray<int>),
				"[J" => typeof (JavaArray<long>),
				"[F" => typeof (JavaArray<float>),
				"[D" => typeof (JavaArray<double>),
				_ => GetObjectArrayType (jniName)
			};
		}

		Type? GetObjectArrayType (string jniName)
		{
			if (_externalTypeMap.TryGetValue (jniName, out Type? arrayType)) {
				return arrayType;
			}

			// Fallback for java/lang/String arrays
			if (jniName.StartsWith ("[L", StringComparison.Ordinal) && jniName.EndsWith (";", StringComparison.Ordinal)) {
				string elementJni = jniName.Substring (2, jniName.Length - 3);
				if (string.Equals (elementJni, "java/lang/String", StringComparison.Ordinal)) {
					return typeof (JavaArray<string>);
				}
			}

			return null;
		}

		/// <inheritdoc/>
		public IntPtr GetFunctionPointer (ReadOnlySpan<char> className, int methodIndex)
		{
			string classNameStr = className.ToString ();

			if (!_externalTypeMap.TryGetValue (classNameStr, out Type? type)) {
				throw new TypeMapException (
					$"XA4301: Type lookup failed for '{classNameStr}'. " +
					"Ensure the type has [Register] attribute and is not trimmed away.");
			}

			JavaPeerProxy? proxy = GetProxyForType (type!);
			if (proxy is IAndroidCallableWrapper acw) {
				return acw.GetFunctionPointer (methodIndex);
			}

			if (proxy is null) {
				throw new TypeMapException (
					$"XA4302: No proxy found for type '{type!.FullName}' when looking up function pointer for '{classNameStr}' at index {methodIndex}.");
			}

			throw new TypeMapException (
				$"XA4303: Type '{type!.FullName}' is an MCW and does not have function pointers. " +
				$"Requested function pointer for '{classNameStr}' at index {methodIndex}.");
		}

		/// <summary>
		/// Gets the function pointer for a given class name and method index.
		/// This overload accepts a string for convenience when called from native code.
		/// </summary>
		public IntPtr GetFunctionPointer (string className, int methodIndex)
		{
			return GetFunctionPointer (className.AsSpan (), methodIndex);
		}

		/// <summary>
		/// Creates a 1D .NET array of the specified element type using the pre-generated proxy.
		/// </summary>
		public Array CreateArray (Type elementType, int length, int rank)
		{
			global::Android.Util.Log.Info ("TYPEMAP", $"CreateArray: elementType={elementType.FullName}, length={length}, rank={rank}");
			if (rank < 1 || rank > 3) {
				throw new ArgumentOutOfRangeException (nameof (rank), rank, "Rank must be 1, 2, or 3");
			}

			if (elementType.IsArray) {
				var innerType = elementType.GetElementType ()
					?? throw new InvalidOperationException ($"Cannot get element type from array type {elementType.FullName}");
				return CreateArray (innerType, length, rank + 1);
			}

			if (!TryGetJniNameForType (elementType, out string? jniName)) {
				global::Android.Util.Log.Error ("TYPEMAP", $"CreateArray: No JNI name for {elementType.FullName}");
				throw new InvalidOperationException ($"No JNI name found for element type {elementType.FullName}");
			}

			global::Android.Util.Log.Info ("TYPEMAP", $"CreateArray: JNI name = {jniName}");
			if (!_externalTypeMap.TryGetValue (jniName, out Type? proxyType)) {
				global::Android.Util.Log.Error ("TYPEMAP", $"CreateArray: No proxy registered for {jniName}");
				throw new InvalidOperationException ($"No proxy registered for {jniName}");
			}

			global::Android.Util.Log.Info ("TYPEMAP", $"CreateArray: proxy type = {proxyType?.FullName}");
			var proxy = GetProxyForType (proxyType)
				?? throw new InvalidOperationException ($"No proxy instance for {proxyType.FullName}");

			global::Android.Util.Log.Info ("TYPEMAP", $"CreateArray: calling proxy.GetJavaPeerContainerFactory()");
			var factory = proxy.GetJavaPeerContainerFactory ();
			global::Android.Util.Log.Info ("TYPEMAP", $"CreateArray: factory = {factory?.GetType().FullName ?? "NULL"}");
			return factory.CreateArray (length, rank);
		}
	}
}
