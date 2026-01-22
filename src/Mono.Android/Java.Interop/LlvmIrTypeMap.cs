using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Android.Runtime;
using Microsoft.Android.Runtime;

namespace Java.Interop
{
	/// <summary>
	/// Provides type mappings using LLVM IR generated type maps for Mono runtime.
	/// Falls back to native type lookup for managed-to-Java mappings.
	/// </summary>
	[RequiresUnreferencedCode ("Uses runtime code generation for type mapping.")]
	class LlvmIrTypeMap : ITypeMap
	{
		[ThreadStatic]
		static byte[]? mvid_bytes;
		static readonly Type[] XAConstructorSignature = [typeof (IntPtr), typeof (JniHandleOwnership)];
		static readonly Type[] JIConstructorSignature = [typeof (JniObjectReference).MakeByRefType (), typeof (JniObjectReferenceOptions)];

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		static extern Type monodroid_typemap_java_to_managed (string java_type_name);

		static Type monovm_typemap_java_to_managed (string java_type_name)
		{
			return monodroid_typemap_java_to_managed (java_type_name);
		}

		static Type? clr_typemap_java_to_managed (string java_type_name)
		{
			bool result = RuntimeNativeMethods.clr_typemap_java_to_managed (java_type_name, out IntPtr managedAssemblyNamePointer, out uint managedTypeTokenId);
			if (!result || managedAssemblyNamePointer == IntPtr.Zero) {
				return null;
			}

			string? managedAssemblyName = Marshal.PtrToStringAnsi (managedAssemblyNamePointer);
			Assembly assembly = Assembly.Load (managedAssemblyName!);
			Type? ret = null;
			foreach (Module module in assembly.Modules) {
				ret = module.ResolveType ((int)managedTypeTokenId);
				if (ret != null) {
					break;
				}
			}

			if (Logger.LogAssembly) {
				Logger.Log (LogLevel.Info, "monodroid", $"Loaded type: {ret}");
			}

			return ret;
		}

		Type? GetJavaToManagedType (string class_name)
		{
			lock (TypeManagerMapDictionaries.AccessLock) {
				if (TypeManagerMapDictionaries.JniToManaged.TryGetValue (class_name, out Type? type)) {
					return type;
				}

				if (RuntimeFeature.IsMonoRuntime) {
					type = monovm_typemap_java_to_managed (class_name);
				} else if (RuntimeFeature.IsCoreClrRuntime) {
					type = clr_typemap_java_to_managed (class_name);
				} else {
					throw new NotSupportedException ("Internal error: unknown runtime not supported");
				}

				if (type != null) {
					TypeManagerMapDictionaries.JniToManaged.Add (class_name, type);
					return type;
				}

				// Miss message is logged in the native runtime
				if (Logger.LogAssembly)
					JNIEnv.LogTypemapTrace (new System.Diagnostics.StackTrace (true));
				return null;
			}
		}

		/// <inheritdoc/>
		public bool TryGetTypesForJniName (string jniSimpleReference, [NotNullWhen (true)] out IEnumerable<Type>? types)
		{
			var type = GetJavaToManagedType (jniSimpleReference);
			if (type != null) {
				types = [type];
				return true;
			}

			types = null;
			return false;
		}

		/// <inheritdoc/>
		public bool TryGetInvokerType (Type type, [NotNullWhen (true)] out Type? invokerType)
		{
			// Legacy lookup via JavaObjectExtensions
			invokerType = JavaObjectExtensions.GetInvokerType (type);
			return invokerType != null;
		}

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		static extern unsafe IntPtr monodroid_typemap_managed_to_java (Type type, byte* mvidptr);

		static unsafe IntPtr monovm_typemap_managed_to_java (Type type, byte* mvidptr)
		{
			return monodroid_typemap_managed_to_java (type, mvidptr);
		}

		/// <inheritdoc/>
		public unsafe bool TryGetJniNameForType (Type type, [NotNullWhen (true)] out string? jniName)
		{
			if (mvid_bytes == null)
				mvid_bytes = new byte[16];

			var mvid = new Span<byte> (mvid_bytes);
			byte[]? mvid_data = null;
			if (!type.Module.ModuleVersionId.TryWriteBytes (mvid)) {
				RuntimeNativeMethods.monodroid_log (LogLevel.Warn, LogCategories.Default, $"Failed to obtain module MVID using the fast method, falling back to the slow one");
				mvid_data = type.Module.ModuleVersionId.ToByteArray ();
			} else {
				mvid_data = mvid_bytes;
			}

			IntPtr ret;
			fixed (byte* mvidptr = mvid_data) {
				if (RuntimeFeature.IsMonoRuntime) {
					ret = monovm_typemap_managed_to_java (type, mvidptr);
				} else if (RuntimeFeature.IsCoreClrRuntime) {
					ret = RuntimeNativeMethods.clr_typemap_managed_to_java (type.FullName, (IntPtr) mvidptr);
				} else {
					throw new NotSupportedException ("Internal error: unknown runtime not supported");
				}
			}

			if (ret == IntPtr.Zero) {
				if (Logger.LogAssembly) {
					RuntimeNativeMethods.monodroid_log (LogLevel.Warn, LogCategories.Default, $"typemap: failed to map managed type to Java type: {type.AssemblyQualifiedName} (Module ID: {type.Module.ModuleVersionId}; Type token: {type.MetadataToken})");
					JNIEnv.LogTypemapTrace (new System.Diagnostics.StackTrace (true));
				}

				jniName = null;
				return false;
			}

			jniName = Marshal.PtrToStringAnsi (ret);
			return jniName != null;
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
		public IntPtr GetFunctionPointer (string className, int methodIndex)
		{
			// LlvmIrTypeMap doesn't use the attribute-based GetFunctionPointer mechanism.
			// This is only implemented in TypeMapAttributeTypeMap for CoreCLR.
			throw new NotSupportedException ("LlvmIrTypeMap does not support this GetFunctionPointer shape.");
		}

		/// <inheritdoc/>
		public IJavaPeerable? CreatePeer (
			IntPtr handle,
			JniHandleOwnership transfer,
			Type? targetType)
		{
			Type? type = null;
			IntPtr class_ptr = JNIEnv.GetObjectClass (handle);
			string? class_name = TypeManager.GetClassName (class_ptr);

			lock (TypeManagerMapDictionaries.AccessLock) {
				// TODO I suppose we want to use cache if we're locking?
				while (class_ptr != IntPtr.Zero) {
					type = GetJavaToManagedType (class_name!);
					if (type != null) {
						// TODO: I suppose we need to add it to the cache now that we found it?
						break;
					}

					IntPtr super_class_ptr = JNIEnv.GetSuperclass (class_ptr);
					JNIEnv.DeleteLocalRef (class_ptr);
					class_name = null;
					class_ptr = super_class_ptr;
					if (class_ptr != IntPtr.Zero) {
						class_name = TypeManager.GetClassName (class_ptr);
					}
				}
			}

			if (class_ptr != IntPtr.Zero) {
				JNIEnv.DeleteLocalRef (class_ptr);
				class_ptr = IntPtr.Zero;
			}

			if (targetType != null &&
					(type == null ||
					 !targetType.IsAssignableFrom (type))) {
				type = targetType;
			}

			if (type == null) {
				class_name = JNIEnv.GetClassNameFromInstance (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (
						FormattableString.Invariant ($"Internal error finding wrapper class for '{class_name}'. (Where is the Java.Lang.Object wrapper?!)"),
						TypeManager.CreateJavaLocationException ());
			}

			if (type.IsInterface || type.IsAbstract) {
				var invokerType = JavaObjectExtensions.GetInvokerType (type);
				if (invokerType == null)
					throw new NotSupportedException ("Unable to find Invoker for type '" + type.FullName + "'. Was it linked away?",
							TypeManager.CreateJavaLocationException ());
				type = invokerType;
			}

			var typeSig = JNIEnvInit.androidRuntime?.TypeManager.GetTypeSignature (type) ?? default;
			if (!typeSig.IsValid || typeSig.SimpleReference == null) {
				throw new ArgumentException ($"Could not determine Java type corresponding to `{type.AssemblyQualifiedName}`.", nameof (targetType));
			}

			JniObjectReference typeClass = default;
			JniObjectReference handleClass = default;
			try {
				try {
					typeClass = JniEnvironment.Types.FindClass (typeSig.SimpleReference);
				} catch (Exception e) {
					throw new ArgumentException ($"Could not find Java class `{typeSig.SimpleReference}`.",
							nameof (targetType),
							e);
				}

				handleClass = JniEnvironment.Types.GetObjectClass (new JniObjectReference (handle));
				if (!JniEnvironment.Types.IsAssignableFrom (handleClass, typeClass)) {
					if (Logger.LogAssembly) {
						var message = $"Handle 0x{handle:x} is of type '{JNIEnv.GetClassNameFromInstance (handle)}' which is not assignable to '{typeSig.SimpleReference}'";
						Logger.Log (LogLevel.Debug, "monodroid-assembly", message);
					}
					if (RuntimeFeature.IsAssignableFromCheck) {
						return null;
					}
				}
			} finally {
				JniObjectReference.Dispose (ref handleClass);
				JniObjectReference.Dispose (ref typeClass);
			}

			IJavaPeerable? result = null;

			try {
				result = CreateProxy (type, handle, transfer);
				if (Runtime.IsGCUserPeer (result.PeerReference.Handle)) {
					result.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);
				}
			} catch (MissingMethodException e) {
				var key_handle = JNIEnv.IdentityHash (handle);
				JNIEnv.DeleteRef (handle, transfer);
				throw new NotSupportedException (FormattableString.Invariant (
					$"Unable to activate instance of type {type} from native handle 0x{handle:x} (key_handle 0x{key_handle:x})."), e);
			}
			return result;
		}

		static IJavaPeerable CreateProxy (
				Type type,
				IntPtr handle,
				JniHandleOwnership transfer)
		{
			// Skip Activator.CreateInstance() as that requires public constructors,
			// and we want to hide some constructors for sanity reasons.
			var peer = GetUninitializedObject (type);
			BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			var c = type.GetConstructor (flags, null, XAConstructorSignature, null);
			if (c != null) {
				c.Invoke (peer, [handle, transfer]);
				return peer;
			}
			c = type.GetConstructor (flags, null, JIConstructorSignature, null);
			if (c != null) {
				JniObjectReference r = new JniObjectReference (handle);
				JniObjectReferenceOptions o = JniObjectReferenceOptions.Copy;
				c.Invoke (peer, [r, o]);
				JNIEnv.DeleteRef (handle, transfer);
				return peer;
			}
			GC.SuppressFinalize (peer);
			throw new MissingMethodException (
					"No constructor found for " + type.FullName + "::.ctor(System.IntPtr, Android.Runtime.JniHandleOwnership)",
					TypeManager.CreateJavaLocationException ());

			static IJavaPeerable GetUninitializedObject (Type type)
			{
				var v = (IJavaPeerable) RuntimeHelpers.GetUninitializedObject (type);
				v.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);
				return v;
			}
		}
	}
}
