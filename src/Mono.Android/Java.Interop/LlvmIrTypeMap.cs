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
	/// </summary>
	[RequiresUnreferencedCode ("Uses runtime code generation for type mapping.")]
	class LlvmIrTypeMap : ITypeMap
	{
		[ThreadStatic]
		static byte[]? mvid_bytes;

		static readonly Type[] XAConstructorSignature = [typeof (IntPtr), typeof (JniHandleOwnership)];
		static readonly Type[] JIConstructorSignature = [typeof (JniObjectReference).MakeByRefType (), typeof (JniObjectReferenceOptions)];

		/// <inheritdoc/>
		public Type? TryGetExactTypeMapping (string class_name)
		{
			lock (TypeManagerMapDictionaries.AccessLock) {
				if (TypeManagerMapDictionaries.JniToManaged.TryGetValue (class_name, out Type? type)) {
					return type;
				}

				type = LookupTypeFromNative (class_name);
				if (type != null) {
					TypeManagerMapDictionaries.JniToManaged.Add (class_name, type);
				}

				return type;
			}
		}

		static Type? LookupTypeFromNative (string class_name)
		{
			if (RuntimeFeature.IsMonoRuntime) {
				return TypeManager.monodroid_typemap_java_to_managed (class_name);
			}

			if (RuntimeFeature.IsCoreClrRuntime) {
				bool result = RuntimeNativeMethods.clr_typemap_java_to_managed (class_name, out IntPtr managedAssemblyNamePointer, out uint managedTypeTokenId);
				if (!result || managedAssemblyNamePointer == IntPtr.Zero) {
					return null;
				}

				string? managedAssemblyName = Marshal.PtrToStringAnsi (managedAssemblyNamePointer);
				Assembly assembly = Assembly.Load (managedAssemblyName!);
				foreach (Module module in assembly.Modules) {
					var ret = module.ResolveType ((int) managedTypeTokenId);
					if (ret != null) {
						return ret;
					}
				}
				return null;
			}

			throw new NotSupportedException ("Unknown runtime");
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
			=> JNIEnv.monovm_typemap_managed_to_java (type, mvidptr);

		/// <inheritdoc/>
		public unsafe bool TryGetJniNameForType (Type type, [NotNullWhen (true)] out string? jniName)
		{
			mvid_bytes ??= new byte[16];

			var mvid = new Span<byte> (mvid_bytes);
			byte[]? mvid_data = type.Module.ModuleVersionId.TryWriteBytes (mvid)
				? mvid_bytes
				: type.Module.ModuleVersionId.ToByteArray ();

			IntPtr ret;
			fixed (byte* mvidptr = mvid_data) {
				if (RuntimeFeature.IsMonoRuntime) {
					ret = monovm_typemap_managed_to_java (type, mvidptr);
				} else if (RuntimeFeature.IsCoreClrRuntime) {
					ret = RuntimeNativeMethods.clr_typemap_managed_to_java (type.FullName, (IntPtr) mvidptr);
				} else {
					throw new NotSupportedException ("Unknown runtime");
				}
			}

			jniName = ret != IntPtr.Zero ? Marshal.PtrToStringAnsi (ret) : null;
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
		public JavaPeerProxy? GetProxyForManagedType (Type managedType) => null;

		/// <inheritdoc/>
		public bool TryGetInvokerType (Type type, [NotNullWhen (true)] out Type? invokerType)
		{
			invokerType = JavaObjectExtensions.GetInvokerType (type);
			return invokerType != null;
		}

		/// <inheritdoc/>
		public IntPtr GetFunctionPointer (ReadOnlySpan<char> className, int methodIndex)
			=> throw new NotSupportedException ("LlvmIrTypeMap does not support GetFunctionPointer.");

		/// <inheritdoc/>
		[RequiresUnreferencedCode ("Uses Array.CreateInstance which is not AOT-safe.")]
		public Array CreateArray (Type elementType, int length, int rank)
		{
			if (rank < 1 || rank > 2) {
				throw new ArgumentOutOfRangeException (nameof (rank), rank, "Rank must be 1 or 2");
			}

			while (elementType.IsArray) {
				elementType = elementType.GetElementType ()!;
				rank++;
			}

			if (rank > 2) {
				throw new ArgumentOutOfRangeException (nameof (rank), rank, "Rank must be 1 or 2");
			}

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
