#nullable enable

using System;
using Java.Interop;
using Microsoft.Android.Runtime;

namespace Android.Runtime
{
	/// <summary>
	/// Shared algorithm for creating managed peers from Java object handles.
	/// </summary>
	static class PeerCreationHelper
	{
		public delegate IJavaPeerable? InstanceCreatorDelegate (Type type, IntPtr handle, JniHandleOwnership transfer);
		public delegate Type? TypeResolverDelegate (IntPtr class_ptr, string class_name);

		public static IJavaPeerable? CreatePeer (
			IntPtr handle,
			JniHandleOwnership transfer,
			Type? targetType,
			ITypeMap typeMap,
			TypeResolverDelegate typeResolver,
			InstanceCreatorDelegate instanceCreator,
			bool resolveInvokerTypes)
		{
			IntPtr class_ptr = JNIEnv.GetObjectClass (handle);
			string? class_name = Java.Interop.TypeManager.GetClassName (class_ptr);

			Type? type = class_name != null ? typeResolver (class_ptr, class_name) : null;

			if (class_ptr != IntPtr.Zero) {
				JNIEnv.DeleteLocalRef (class_ptr);
			}

			if (targetType != null && (type == null || !targetType.IsAssignableFrom (type))) {
				type = targetType;
			}

			if (type == null) {
				class_name = JNIEnv.GetClassNameFromInstance (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (
					FormattableString.Invariant ($"Internal error finding wrapper class for '{class_name}'."),
					Java.Interop.TypeManager.CreateJavaLocationException ());
			}

			if (type.IsGenericTypeDefinition) {
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException ($"Cannot create peer for open generic type '{type.FullName}'.");
			}

			if (resolveInvokerTypes && (type.IsInterface || type.IsAbstract)) {
				if (!typeMap.TryGetInvokerType (type, out Type? invokerType)) {
					JNIEnv.DeleteRef (handle, transfer);
					throw new NotSupportedException (
						$"Unable to find Invoker for type '{type.FullName}'. Was it linked away?",
						Java.Interop.TypeManager.CreateJavaLocationException ());
				}
				type = invokerType;
			}

			var typeSig = JNIEnvInit.androidRuntime?.TypeManager.GetTypeSignature (type) ?? default;
			if (!typeSig.IsValid || typeSig.SimpleReference == null) {
				JNIEnv.DeleteRef (handle, transfer);
				throw new ArgumentException (
					$"Could not determine Java type corresponding to `{type.AssemblyQualifiedName}`.",
					nameof (targetType));
			}

			if (!IsJavaTypeAssignableFrom (handle, typeSig.SimpleReference)) {
				return null;
			}

			IJavaPeerable? result = instanceCreator (type, handle, transfer);
			if (result == null) {
				var key_handle = JNIEnv.IdentityHash (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (FormattableString.Invariant (
					$"Unable to activate instance of type {type} from native handle 0x{handle:x} (key_handle 0x{key_handle:x})."));
			}

			if (Java.Interop.Runtime.IsGCUserPeer (result.PeerReference.Handle)) {
				result.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);
			}

			return result;
		}

		static bool IsJavaTypeAssignableFrom (IntPtr handle, string jniTypeName)
		{
			JniObjectReference typeClass = default;
			JniObjectReference handleClass = default;
			try {
				try {
					typeClass = JniEnvironment.Types.FindClass (jniTypeName);
				} catch (Exception e) {
					throw new ArgumentException ($"Could not find Java class `{jniTypeName}`.", "targetType", e);
				}

				handleClass = JniEnvironment.Types.GetObjectClass (new JniObjectReference (handle));
				if (!JniEnvironment.Types.IsAssignableFrom (handleClass, typeClass)) {
					if (RuntimeFeature.IsAssignableFromCheck) {
						return false;
					}
				}
				return true;
			} finally {
				JniObjectReference.Dispose (ref handleClass);
				JniObjectReference.Dispose (ref typeClass);
			}
		}
	}
}
