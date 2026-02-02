#nullable enable

using System;
using Java.Interop;
using Microsoft.Android.Runtime;

namespace Android.Runtime
{
	/// <summary>
	/// Provides the shared algorithm for creating managed peers from Java object handles.
	/// This abstracts the common logic used by both LlvmIrTypeMap and TypeMapAttributeTypeMap.
	/// </summary>
	static class PeerCreationHelper
	{
		/// <summary>
		/// Delegate for creating a peer instance from a resolved type.
		/// </summary>
		/// <param name="type">The resolved .NET type to instantiate (may be interface/abstract for new typemap).</param>
		/// <param name="handle">The Java object handle.</param>
		/// <param name="transfer">The handle ownership transfer mode.</param>
		/// <returns>The created peer instance, or null if creation failed.</returns>
		public delegate IJavaPeerable? InstanceCreatorDelegate (Type type, IntPtr handle, JniHandleOwnership transfer);

		/// <summary>
		/// Delegate for resolving a type from a Java class handle and name.
		/// This allows different implementations (hierarchy walking, array handling, caching) per typemap.
		/// </summary>
		/// <param name="class_ptr">The Java class pointer (caller manages lifetime).</param>
		/// <param name="class_name">The JNI class name.</param>
		/// <returns>The resolved .NET type, or null if not found.</returns>
		public delegate Type? TypeResolverDelegate (IntPtr class_ptr, string class_name);

		/// <summary>
		/// Creates a managed peer for a Java object handle using the shared algorithm.
		/// </summary>
		/// <param name="handle">The Java object handle.</param>
		/// <param name="transfer">The handle ownership transfer mode.</param>
		/// <param name="targetType">Optional target type hint from the caller.</param>
		/// <param name="typeMap">The type map to use for invoker lookup and JNI name resolution.</param>
		/// <param name="typeResolver">Delegate to resolve type from class pointer/name. This should handle
		/// hierarchy walking, array types, and caching as needed by the specific typemap implementation.</param>
		/// <param name="instanceCreator">Delegate to create the peer instance. For legacy typemap, this uses
		/// reflection. For new typemap, this uses JavaPeerProxy.CreateInstance() which handles invoker
		/// creation internally for interfaces/abstract classes.</param>
		/// <param name="resolveInvokerTypes">If true, resolves invoker types for interfaces/abstract classes
		/// before calling instanceCreator. Set to true for legacy typemap (reflection-based), false for new
		/// typemap (proxy handles invoker creation internally).</param>
		/// <returns>The created peer, or null if assignability check fails.</returns>
		/// <exception cref="NotSupportedException">Thrown when no type mapping is found or instance creation fails.</exception>
		/// <exception cref="ArgumentException">Thrown when JNI type validation fails.</exception>
		public static IJavaPeerable? CreatePeer (
			IntPtr handle,
			JniHandleOwnership transfer,
			Type? targetType,
			ITypeMap typeMap,
			TypeResolverDelegate typeResolver,
			InstanceCreatorDelegate instanceCreator,
			bool resolveInvokerTypes)
		{
			// Step 1: Get the Java class from the handle
			IntPtr class_ptr = JNIEnv.GetObjectClass (handle);
			string? class_name = Java.Interop.TypeManager.GetClassName (class_ptr);

			// Step 2: Resolve type using the provided resolver (handles hierarchy walking, arrays, caching)
			Type? type = class_name != null
				? typeResolver (class_ptr, class_name)
				: null;

			// Step 3: Clean up the class_ptr we created
			if (class_ptr != IntPtr.Zero) {
				JNIEnv.DeleteLocalRef (class_ptr);
			}

			// Step 4: Apply targetType override
			// If targetType is provided and the hierarchy type doesn't satisfy it, use targetType
			if (targetType != null && (type == null || !targetType.IsAssignableFrom (type))) {
				type = targetType;
			}

			// Step 5: Validate we found a type
			if (type == null) {
				class_name = JNIEnv.GetClassNameFromInstance (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (
					FormattableString.Invariant ($"Internal error finding wrapper class for '{class_name}'. (Where is the Java.Lang.Object wrapper?!)"),
					Java.Interop.TypeManager.CreateJavaLocationException ());
			}

			// Step 6: Handle open generic types
			if (type.IsGenericTypeDefinition) {
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (
					$"Cannot create peer for open generic type '{type.FullName}'. " +
					"Ensure closed generic types are used at build time.");
			}

			// Step 7: Find invoker type for interfaces and abstract classes (legacy typemap only)
			// For the new typemap, the proxy's CreateInstance() handles invoker creation internally.
			if (resolveInvokerTypes && (type.IsInterface || type.IsAbstract)) {
				if (!typeMap.TryGetInvokerType (type, out Type? invokerType)) {
					JNIEnv.DeleteRef (handle, transfer);
					throw new NotSupportedException (
						$"Unable to find Invoker for type '{type.FullName}'. Was it linked away?",
						Java.Interop.TypeManager.CreateJavaLocationException ());
				}
				type = invokerType;
			}

			// Step 8: Get and validate the JNI type signature
			var typeSig = JNIEnvInit.androidRuntime?.TypeManager.GetTypeSignature (type) ?? default;
			if (!typeSig.IsValid || typeSig.SimpleReference == null) {
				JNIEnv.DeleteRef (handle, transfer);
				throw new ArgumentException (
					$"Could not determine Java type corresponding to `{type.AssemblyQualifiedName}`.",
					nameof (targetType));
			}

			// Step 9: Check IsAssignableFrom
			if (!IsJavaTypeAssignableFrom (handle, typeSig.SimpleReference)) {
				return null;
			}

			// Step 10: Create the peer instance
			IJavaPeerable? result = instanceCreator (type, handle, transfer);

			if (result == null) {
				var key_handle = JNIEnv.IdentityHash (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (FormattableString.Invariant (
					$"Unable to activate instance of type {type} from native handle 0x{handle:x} (key_handle 0x{key_handle:x})."));
			}

			// Step 11: Set peer state for GC user peers
			if (Java.Interop.Runtime.IsGCUserPeer (result.PeerReference.Handle)) {
				result.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);
			}

			return result;
		}

		/// <summary>
		/// Checks if the Java object's type is assignable from the specified JNI type.
		/// </summary>
		static bool IsJavaTypeAssignableFrom (IntPtr handle, string jniTypeName)
		{
			JniObjectReference typeClass = default;
			JniObjectReference handleClass = default;
			try {
				try {
					typeClass = JniEnvironment.Types.FindClass (jniTypeName);
				} catch (Exception e) {
					throw new ArgumentException (
						$"Could not find Java class `{jniTypeName}`.",
						"targetType",
						e);
				}

				handleClass = JniEnvironment.Types.GetObjectClass (new JniObjectReference (handle));
				if (!JniEnvironment.Types.IsAssignableFrom (handleClass, typeClass)) {
					if (Logger.LogAssembly) {
						var message = $"Handle 0x{handle:x} is of type '{JNIEnv.GetClassNameFromInstance (handle)}' which is not assignable to '{jniTypeName}'";
						Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
					}
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
