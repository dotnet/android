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

		static Type monovm_typemap_java_to_managed (string java_type_name)
		{
			return TypeManager.monodroid_typemap_java_to_managed (java_type_name);
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

		/// <inheritdoc/>
		public Type? TryGetExactTypeMapping (string class_name)
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
			var type = TryGetExactTypeMapping (jniSimpleReference);
			if (type != null) {
				types = [type];
				return true;
			}

			types = null;
			return false;
		}

		static unsafe IntPtr monovm_typemap_managed_to_java (Type type, byte* mvidptr)
		{
			return JNIEnv.monovm_typemap_managed_to_java (type, mvidptr);
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
		public JavaPeerProxy? GetProxyForManagedType (Type managedType)
		{
			// LlvmIrTypeMap uses reflection-based activation, not proxy types
			return null;
		}

		/// <inheritdoc/>
		public bool TryGetInvokerType (Type type, [NotNullWhen (true)] out Type? invokerType)
		{
			// For LlvmIrTypeMap, we look up the invoker type from the [Register] attribute's Invoker property
			invokerType = JavaObjectExtensions.GetInvokerType (type);
			return invokerType != null;
		}

		/// <inheritdoc/>
		public IntPtr GetFunctionPointer (ReadOnlySpan<char> className, int methodIndex)
		{
			// LlvmIrTypeMap doesn't use the attribute-based GetFunctionPointer mechanism.
			// This is only implemented in TypeMapAttributeTypeMap for CoreCLR.
			throw new NotSupportedException ("LlvmIrTypeMap does not support this GetFunctionPointer shape.");
		}

		/// <inheritdoc/>
		[RequiresUnreferencedCode ("Uses Array.CreateInstance which is not AOT-safe.")]
		public Array CreateArray (Type elementType, int length, int rank)
		{
			if (rank < 1 || rank > 2) {
				throw new ArgumentOutOfRangeException (nameof (rank), rank, "Rank must be 1 or 2");
			}

			// Unwrap nested array types
			while (elementType.IsArray) {
				elementType = elementType.GetElementType ()!;
				rank++;
			}

			if (rank > 2) {
				throw new ArgumentOutOfRangeException (nameof (rank), rank, "Rank must be 1 or 2");
			}

			// Legacy MonoVM path - reflection is OK here
			var arrayType = rank == 1 ? elementType : elementType.MakeArrayType ();
			return Array.CreateInstance (arrayType, length);
		}

		/// <inheritdoc/>
		public IJavaPeerable? CreatePeer (
			IntPtr handle,
			JniHandleOwnership transfer,
			Type? targetType)
		{
			return Android.Runtime.PeerCreationHelper.CreatePeer (
				handle,
				transfer,
				targetType,
				typeMap: this,
				typeResolver: (class_ptr, class_name) => JavaHierarchyWalker.WalkHierarchy (class_ptr, class_name, this),
				instanceCreator: CreateProxy,
				resolveInvokerTypes: true);
		}

		static IJavaPeerable? CreateProxy (
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
			// Return null - PeerCreationHelper will throw NotSupportedException with proper context
			return null;

			static IJavaPeerable GetUninitializedObject (Type type)
			{
				var v = (IJavaPeerable) RuntimeHelpers.GetUninitializedObject (type);
				v.SetJniManagedPeerState (JniManagedPeerStates.Replaceable | JniManagedPeerStates.Activatable);
				return v;
			}
		}
	}
}
