#nullable enable

using System;
using Java.Interop;
using Microsoft.Android.Runtime;

namespace Android.Runtime
{
	/// <summary>
	/// Shared algorithm for creating managed peers from Java object handles.
	/// Includes hierarchy walking to find registered .NET types.
	/// </summary>
	static class PeerCreationHelper
	{
		public delegate IJavaPeerable? InstanceCreatorDelegate (Type type, IntPtr handle, JniHandleOwnership transfer);
		public delegate Type? TypeResolverDelegate (IntPtr class_ptr, string class_name);

		/// <summary>
		/// Creates a managed peer for the given Java object handle.
		/// </summary>
		/// <param name="typeResolver">Optional custom type resolver. If null, uses WalkHierarchy directly.</param>
		public static IJavaPeerable? CreatePeer (
			IntPtr handle,
			JniHandleOwnership transfer,
			Type? targetType,
			ITypeMap typeMap,
			InstanceCreatorDelegate instanceCreator,
			bool resolveInvokerTypes,
			TypeResolverDelegate? typeResolver = null)
		{
			IntPtr class_ptr = JNIEnv.GetObjectClass (handle);
			string? class_name = Java.Interop.TypeManager.GetClassName (class_ptr);

			Type? type = null;
			if (class_name != null) {
				type = typeResolver != null
					? typeResolver (class_ptr, class_name)
					: WalkHierarchy (class_ptr, class_name, typeMap);
			}

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

		/// <summary>
		/// Walks the Java class hierarchy starting from <paramref name="class_ptr"/> and queries
		/// the type map for each class until a type mapping is found.
		/// </summary>
		/// <param name="class_ptr">The starting Java class pointer. This ref is NOT deleted by this method.</param>
		/// <param name="class_name">The JNI name of the starting class.</param>
		/// <param name="typeMap">The type map to query for exact type mappings.</param>
		/// <returns>The first mapped .NET type found, or null if no mapping exists.</returns>
		public static Type? WalkHierarchy (IntPtr class_ptr, string class_name, ITypeMap typeMap)
		{
			if (class_ptr == IntPtr.Zero) {
				return null;
			}

			Type? result = null;
			IntPtr currentPtr = class_ptr;
			string? currentName = class_name;

			while (currentPtr != IntPtr.Zero) {
				if (currentName != null) {
					result = typeMap.TryGetExactTypeMapping (currentName);
					if (result != null) {
						break;
					}
				}

				IntPtr super_class_ptr = JNIEnv.GetSuperclass (currentPtr);

				// Delete local refs we created, but not the original class_ptr
				if (currentPtr != class_ptr) {
					JNIEnv.DeleteLocalRef (currentPtr);
				}

				currentPtr = super_class_ptr;
				currentName = currentPtr != IntPtr.Zero
					? Java.Interop.TypeManager.GetClassName (currentPtr)
					: null;
			}

			// Clean up the last pointer if it's not the original
			if (currentPtr != IntPtr.Zero && currentPtr != class_ptr) {
				JNIEnv.DeleteLocalRef (currentPtr);
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
