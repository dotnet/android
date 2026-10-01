#if INSIDE_MONO_ANDROID_RUNTIME
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Java;

namespace Android.Runtime
{
	// NOTE: Keep this in sync with the native side in src/native/common/include/managed-interface.hh
	[Flags]
	enum TraceKind : uint
	{
		Java    = 0x01,
		Managed = 0x02,
		Native  = 0x04,
		Signals = 0x08,

		All     = Java | Managed | Native | Signals,
	}

	internal unsafe static partial class RuntimeNativeMethods
	{
		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial void monodroid_log (LogLevel level, LogCategories category, string message);

		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial void monodroid_free (IntPtr ptr);

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial IntPtr _monodroid_lookup_replacement_type (string jniSimpleReference);

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial IntPtr _monodroid_lookup_replacement_method_info (string jniSourceType, string jniMethodName, string jniMethodSignature);

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial IntPtr _monodroid_lookup_replacement_method_info (string jniSourceType, byte* jniMethodName, byte* jniMethodSignature);

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
