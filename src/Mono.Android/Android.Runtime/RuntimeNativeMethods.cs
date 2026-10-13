#if INSIDE_MONO_ANDROID_RUNTIME
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Java;

namespace Android.Runtime
{
	internal static partial class RuntimeNativeMethods
	{
		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		// The stub owns UTF-8 storage and the native implementation always logs it as data, not a format.
		internal static safe partial void monodroid_log (LogLevel level, LogCategories category, string message);

		/// <safety>
		/// <paramref name="ptr"/> must be null or an unfreed allocation from the native allocator paired with this free.
		/// The caller must own the allocation and prevent any subsequent access through aliases.
		/// </safety>
		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static unsafe partial void monodroid_free (IntPtr ptr);

		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		// Native code writes exactly the three scalar outputs pinned by the generated stub.
		internal static safe partial void _monodroid_detect_cpu_and_architecture (ref ushort built_for_cpu, ref ushort running_on_cpu, ref byte is64bit);

		/// <safety>
		/// <paramref name="klass"/> must be a live JNI class reference usable on the current thread.
		/// The returned native string is owned by the caller and must be released with <see cref="monodroid_free"/>.
		/// </safety>
		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static unsafe partial IntPtr monodroid_TypeManager_get_java_class_name (IntPtr klass);

		/// <safety>
		/// <paramref name="bridgeProcessingCallback"/> must have the declared unmanaged ABI and remain callable
		/// for the process lifetime. Its argument graphs belong to the CLR bridge and remain live for each callback.
		/// Initialization is process-global and must occur only once, after the JNI runtime is published.
		/// </safety>
		[LibraryImport (RuntimeConstants.InternalDllName, EntryPoint = "clr_initialize_gc_bridge")]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static unsafe partial delegate* unmanaged<MarkCrossReferencesArgs*, void> clr_initialize_gc_bridge (
			delegate* unmanaged<MarkCrossReferencesArgs*, void> bridgeProcessingCallback);

	}
}
#endif // INSIDE_MONO_ANDROID_RUNTIME
