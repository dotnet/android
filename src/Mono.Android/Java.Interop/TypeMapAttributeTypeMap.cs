#nullable enable

using System;
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

		// Cache of JavaPeerProxy instances keyed by the target type
		static readonly Dictionary<Type, JavaPeerProxy?> s_proxyInstances = new ();
		static readonly Lock s_proxyInstancesLock = new ();

		const string TypeMapsAssemblyName = "_Microsoft.Android.TypeMaps";

		public TypeMapAttributeTypeMap ()
		{
			var typeMapAssembly = Assembly.Load (TypeMapsAssemblyName);
			Assembly.SetEntryAssembly (typeMapAssembly);
			_externalTypeMap = TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object> ();
		}

		/// <inheritdoc/>
		public bool TryGetTypesForJniName (string jniSimpleReference, [NotNullWhen (true)] out IEnumerable<Type>? types)
		{
			if (!_externalTypeMap.TryGetValue (jniSimpleReference, out Type? type)) {
				types = null;
				return false;
			}

			// Check if this is an alias type (multiple .NET types map to same Java name)
			var aliasesAttr = type.GetCustomAttribute<JavaInteropAliasesAttribute> ();
			if (aliasesAttr != null) {
				var aliasedTypes = new List<Type> ();
				foreach (var aliasKey in aliasesAttr.AliasKeys) {
					if (_externalTypeMap.TryGetValue (aliasKey, out Type? aliasedType)) {
						aliasedTypes.Add (aliasedType);
					}
				}
				if (aliasedTypes.Count > 0) {
					types = aliasedTypes;
					return true;
				}
			}

			types = [type];
			return true;
		}

		/// <inheritdoc/>
		public bool TryGetJniNameForType (Type type, [NotNullWhen (true)] out string? jniName)
		{
			// 1. Try to get explicit JNI name from [Register] attribute (or any IJniNameProviderAttribute)
			var attrs = type.GetCustomAttributes (typeof (IJniNameProviderAttribute), inherit: false);
			if (attrs.Length > 0 && attrs[0] is IJniNameProviderAttribute jniNameProvider && !string.IsNullOrEmpty (jniNameProvider.Name)) {
				jniName = jniNameProvider.Name.Replace ('.', '/');
				return true;
			}

			// 2. Fallback: use [JniTypeSignature] if present
			var sigAttr = type.GetCustomAttribute<JniTypeSignatureAttribute> (inherit: false);
			if (sigAttr != null && !string.IsNullOrEmpty (sigAttr.SimpleReference)) {
				jniName = sigAttr.SimpleReference;
				return true;
			}

			// 3. Fallback: derive JNI name using naming conventions
			jniName = JavaNativeTypeManager.ToJniName (type);
			return !string.IsNullOrEmpty (jniName);
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
		internal static JavaPeerProxy? GetProxyForType (Type type)
		{
			lock (s_proxyInstancesLock) {
				if (s_proxyInstances.TryGetValue (type, out var cached)) {
					return cached;
				}

				var proxy = type.GetCustomAttribute<JavaPeerProxy> (inherit: false);
				return s_proxyInstances [type] = proxy;
			}
		}

		/// <inheritdoc/>
		public IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType)
		{
			Type? type = null;
			Type? proxyType = null;
			IntPtr class_ptr = JNIEnv.GetObjectClass (handle);
			string? class_name = GetClassNameFromJavaClassHandle (class_ptr);

			lock (TypeManagerMapDictionaries.AccessLock) {
				while (class_ptr != IntPtr.Zero) {
					if (class_name != null) {
						_externalTypeMap.TryGetValue (class_name, out type);
						if (type != null) {
							if (typeof (JavaPeerProxy).IsAssignableFrom (type)) {
								proxyType = type;
							}
							break;
						}
					}

					IntPtr super_class_ptr = JNIEnv.GetSuperclass (class_ptr);
					JNIEnv.DeleteLocalRef (class_ptr);
					class_name = null;
					class_ptr = super_class_ptr;
					if (class_ptr != IntPtr.Zero) {
						class_name = GetClassNameFromJavaClassHandle (class_ptr);
					}
				}
			}

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

			static bool IsJavaTypeAssignableFrom (IntPtr handle, string jniName)
			{
				JniObjectReference typeClass = default;
				try {
					typeClass = JniEnvironment.Types.FindClass (jniName);
				} catch (Exception e) {
					throw new ArgumentException ($"Could not find Java class `{jniName}`.", nameof (targetType), e);
				}

				JniObjectReference handleClass = default;
				try {
					handleClass = JniEnvironment.Types.GetObjectClass (new JniObjectReference (handle));
					return JniEnvironment.Types.IsAssignableFrom (handleClass, typeClass);
				} finally {
					JniObjectReference.Dispose (ref handleClass);
					JniObjectReference.Dispose (ref typeClass);
				}
			}

			static string? GetClassNameFromJavaClassHandle (IntPtr class_ptr) =>
				Java.Interop.TypeManager.GetClassName (class_ptr);
		}

		bool TryCreateInstance (Type type, Type? proxyType, IntPtr handle, JniHandleOwnership transfer, [NotNullWhen (true)] out IJavaPeerable? result)
		{
			JavaPeerProxy? proxy = null;

			if (proxyType != null && typeof (JavaPeerProxy).IsAssignableFrom (proxyType)) {
				proxy = (JavaPeerProxy?) Activator.CreateInstance (proxyType);
			}

			proxy ??= GetProxyForType (type);

			if (proxy == null) {
				result = null;
				return false;
			}

			result = proxy.CreateInstance (handle, transfer);
			return result != null;
		}

		/// <inheritdoc/>
		public IntPtr GetFunctionPointer (ReadOnlySpan<char> className, int methodIndex)
		{
			string classNameStr = className.ToString ();
			IntPtr result;

			if (classNameStr == "mono/android/TypeManager" && methodIndex == 0) {
				result = Java.Interop.TypeManager.GetActivateFunctionPointer ();
			} else if (!_externalTypeMap.TryGetValue (classNameStr, out Type? type)) {
				result = IntPtr.Zero;
			} else {
				JavaPeerProxy? proxy = GetProxyForType (type);
				result = proxy?.GetFunctionPointer (methodIndex) ?? IntPtr.Zero;
			}

			Logger.Log (LogLevel.Info, "monodroid-typemap",
				$"GetFunctionPointer: class='{classNameStr}', methodIndex={methodIndex}, result=0x{result:X}");
			return result;
		}
	}
}
