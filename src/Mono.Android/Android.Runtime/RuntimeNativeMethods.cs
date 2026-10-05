#if INSIDE_MONO_ANDROID_RUNTIME
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Java;

namespace Android.Runtime
{
	internal unsafe static partial class RuntimeNativeMethods
	{
		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial void monodroid_log (LogLevel level, LogCategories category, string message);

		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial void monodroid_free (IntPtr ptr);

		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial void _monodroid_detect_cpu_and_architecture (ref ushort built_for_cpu, ref ushort running_on_cpu, ref byte is64bit);

		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial IntPtr monodroid_TypeManager_get_java_class_name (IntPtr klass);

		[LibraryImport (RuntimeConstants.InternalDllName, EntryPoint = "clr_initialize_gc_bridge")]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial delegate* unmanaged<MarkCrossReferencesArgs*, void> clr_initialize_gc_bridge (
			delegate* unmanaged<MarkCrossReferencesArgs*, void> bridgeProcessingCallback);

	}
}
#endif // INSIDE_MONO_ANDROID_RUNTIME
