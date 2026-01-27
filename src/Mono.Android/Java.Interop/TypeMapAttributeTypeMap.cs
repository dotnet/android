#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;

namespace Android.Runtime
{
	/// <summary>
	/// Provides type mappings for Java-to-.NET type resolution using compile-time generated attributes.
	/// Used for NativeAOT and CoreCLR runtimes where the Type Mapping API is available.
	/// </summary>
	class TypeMapAttributeTypeMap : ITypeMap
	{
		readonly IReadOnlyDictionary<string, Type> _externalTypeMap;

		readonly ConcurrentDictionary<Type, JavaPeerProxy?> _proxyInstances = new ();
		readonly ConcurrentDictionary<Type, Type[]?> _aliasCache = new ();
		readonly ConcurrentDictionary<Type, string> _jniNameCache = new ();
		readonly ConcurrentDictionary<string, Type?> _classToTypeCache = new ();
		readonly ConcurrentDictionary<string, IntPtr> _jniClassCache = new ();

		public TypeMapAttributeTypeMap ()
		{
			WorkaroundForTheTimeBeing ();
			_externalTypeMap = TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object> ();

			static void WorkaroundForTheTimeBeing ()
			{
				var typeMappingEntryAssembly = AppContext.GetData ("System.Runtime.InteropServices.TypeMappingEntryAssembly");
				var asm = System.Reflection.Assembly.Load (typeMappingEntryAssembly);
				Assembly.SetExecutingAssembly (asm);
			}
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

		/// <summary>
		/// Gets or creates a cached JavaPeerProxy instance for the given type.
		/// </summary>
		JavaPeerProxy? GetProxyForType (Type type)
		{
			Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetProxyForType: Looking for proxy on type {type.FullName}");
			return _proxyInstances.GetOrAdd (type, static t => t.GetCustomAttribute<JavaPeerProxy> (inherit: false));
		}

		/// <inheritdoc/>
		public IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType)
		{
			Type? type = null;
			Type? proxyType = null;
			IntPtr class_ptr = JNIEnv.GetObjectClass (handle);
			string? class_name = GetClassNameFromJavaClassHandle (class_ptr);

			if (class_name != null) {
				type = _classToTypeCache.GetOrAdd (class_name, _ => FindTypeInHierarchy (class_ptr, class_name));

				if (type != null && typeof (JavaPeerProxy).IsAssignableFrom (type)) {
					proxyType = type;
				}
			}

			// Always clean up the original class_ptr
			if (class_ptr != IntPtr.Zero) {
				JNIEnv.DeleteLocalRef (class_ptr);
			}

			// If we found a proxy type, prefer targetType if provided
			if (proxyType != null) {
				if (targetType != null) {
					type = targetType;
				} else {
					type = proxyType;
				}
			} else if (targetType != null && (type == null || !targetType.IsAssignableFrom (type))) {
				type = targetType;
			}

			if (type == null) {
				class_name = JNIEnv.GetClassNameFromInstance (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (
						FormattableString.Invariant ($"Internal error finding wrapper class for '{class_name}'."),
						Java.Interop.TypeManager.CreateJavaLocationException ());
			}

			if (!TryGetJniNameForType (type, out string? jniName) || string.IsNullOrEmpty (jniName)) {
				throw new ArgumentException ($"Could not determine Java type corresponding to `{type.AssemblyQualifiedName}`.", nameof (targetType));
			}

			if (!IsJavaTypeAssignableFrom (handle, jniName)) {
				return null;
			}

			if (!TryCreateInstance (type, proxyType, handle, transfer, out var result)) {
				var key_handle = JNIEnv.IdentityHash (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (FormattableString.Invariant (
					$"Unable to activate instance of type {type} from native handle 0x{handle:x} (key_handle 0x{key_handle:x})."));
			}

			if (Java.Interop.Runtime.IsGCUserPeer (result!.PeerReference.Handle)) {
				result.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);
			}

			return result;

			static string? GetClassNameFromJavaClassHandle (IntPtr class_ptr) =>
				Java.Interop.TypeManager.GetClassName (class_ptr);
		}

		/// <summary>
		/// Walks the Java class hierarchy starting from <paramref name="class_ptr"/> to find a registered .NET type.
		/// </summary>
		/// <param name="class_ptr">The starting Java class pointer (not deleted by this method).</param>
		/// <param name="class_name">The JNI name of the starting class.</param>
		/// <returns>The found .NET type, or null if no type is registered for any class in the hierarchy.</returns>
		Type? FindTypeInHierarchy (IntPtr class_ptr, string class_name)
		{
			Type? foundType = null;
			IntPtr currentPtr = class_ptr;
			string? currentName = class_name;

			while (currentPtr != IntPtr.Zero) {
				if (currentName != null) {
					_externalTypeMap.TryGetValue (currentName, out foundType);
					if (foundType != null) {
						break;
					}
				}

				IntPtr super_class_ptr = JNIEnv.GetSuperclass (currentPtr);
				if (currentPtr != class_ptr) {
					// Only delete refs we created, not the original
					JNIEnv.DeleteLocalRef (currentPtr);
				}
				currentName = null;
				currentPtr = super_class_ptr;
				if (currentPtr != IntPtr.Zero) {
					currentName = Java.Interop.TypeManager.GetClassName (currentPtr);
				}
			}

			// Clean up the last pointer if it's not the original
			if (currentPtr != IntPtr.Zero && currentPtr != class_ptr) {
				JNIEnv.DeleteLocalRef (currentPtr);
			}

			return foundType;
		}

		bool IsJavaTypeAssignableFrom (IntPtr handle, string jniName)
		{
			// Use cached global reference for the type class to avoid repeated FindClass JNI calls
			IntPtr typeClassPtr = _jniClassCache.GetOrAdd (jniName, static name => {
				var classRef = JniEnvironment.Types.FindClass (name);
				// Convert to global reference so it persists beyond this call
				IntPtr globalRef = JNIEnv.NewGlobalRef (classRef.Handle);
				JniObjectReference.Dispose (ref classRef);
				return globalRef;
			});

			if (typeClassPtr == IntPtr.Zero) {
				throw new ArgumentException ($"Could not find Java class `{jniName}`.", "jniName");
			}

			JniObjectReference handleClass = default;
			try {
				handleClass = JniEnvironment.Types.GetObjectClass (new JniObjectReference (handle));
				return JniEnvironment.Types.IsAssignableFrom (handleClass, new JniObjectReference (typeClassPtr));
			} finally {
				JniObjectReference.Dispose (ref handleClass);
				// Don't dispose typeClassPtr - it's a cached global reference
			}
		}

		bool TryCreateInstance (Type type, Type? proxyType, IntPtr handle, JniHandleOwnership transfer, [NotNullWhen (true)] out IJavaPeerable? result)
		{
			JavaPeerProxy? proxy = null;

			// First try to get cached proxy for the specific proxy type (if provided)
			if (proxyType != null && typeof (JavaPeerProxy).IsAssignableFrom (proxyType)) {
				proxy = GetOrCreateProxyInstance (proxyType);
			}

			// Fall back to cached proxy for the target type
			proxy ??= GetProxyForType (type);

			if (proxy == null) {
				result = null;
				return false;
			}

			result = proxy.CreateInstance (handle, transfer);
			return result != null;
		}

		/// <summary>
		/// Gets or creates a cached JavaPeerProxy instance for the given proxy type.
		/// Uses Activator.CreateInstance only on first access; subsequent calls return cached instance.
		/// </summary>
		JavaPeerProxy? GetOrCreateProxyInstance (Type proxyType)
		{
			return _proxyInstances.GetOrAdd (proxyType, static t => {
				if (!typeof (JavaPeerProxy).IsAssignableFrom (t)) {
					return null;
				}
				return (JavaPeerProxy?) Activator.CreateInstance (t);
			});
		}

		/// <inheritdoc/>
		public IntPtr GetFunctionPointer (ReadOnlySpan<char> className, int methodIndex)
		{
			string classNameStr = className.ToString ();

			Logger.Log (LogLevel.Info, "monodroid-typemap", $"GetFunctionPointer called: className='{classNameStr}', methodIndex={methodIndex}");

			// Called once per function pointer during native method registration.
			// Result is cached in LLVM IR globals, so no managed-side caching needed.
			IntPtr result;
			if (!_externalTypeMap.TryGetValue (classNameStr, out Type? type)) {
				Logger.Log (LogLevel.Warn, "monodroid-typemap", $"  -> Class NOT FOUND in _externalTypeMap!");
				result = IntPtr.Zero;
			} else {
				Logger.Log (LogLevel.Info, "monodroid-typemap", $"  -> Found type: {type?.FullName ?? "NULL"}");
				JavaPeerProxy? proxy = GetProxyForType (type!);
				if (proxy == null) {
					Logger.Log (LogLevel.Warn, "monodroid-typemap", $"  -> GetProxyForType returned NULL!");
					result = IntPtr.Zero;
				} else if (proxy is IAndroidCallableWrapper acw) {
					// Only ACW types have GetFunctionPointer - they have generated Java classes that call back
					Logger.Log (LogLevel.Info, "monodroid-typemap", $"  -> Got ACW proxy: {proxy.GetType ().FullName}");
					result = acw.GetFunctionPointer (methodIndex);
					Logger.Log (LogLevel.Info, "monodroid-typemap", $"  -> Function pointer: 0x{result:X}");
				} else {
					// MCW types don't have GetFunctionPointer - they only wrap existing Java classes
					Logger.Log (LogLevel.Warn, "monodroid-typemap", $"  -> Proxy {proxy.GetType ().FullName} is MCW (no GetFunctionPointer)!");
					result = IntPtr.Zero;
				}
			}

			if (result == IntPtr.Zero) {
				Logger.Log (LogLevel.Error, "monodroid-typemap", $"  -> RETURNING NULL POINTER! This will crash!");
			}

			return result;
		}
	}
}
