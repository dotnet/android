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

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial IntPtr monodroid_timing_start (string? message);

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial void monodroid_timing_stop (IntPtr sequence, string? message);

		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial void monodroid_free (IntPtr ptr);

		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial int _monodroid_gref_get ();

		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial int _monodroid_weak_gref_get ();

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial IntPtr _monodroid_lookup_replacement_type (string jniSimpleReference);

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial IntPtr _monodroid_lookup_replacement_method_info (string jniSourceType, string jniMethodName, string jniMethodSignature);


		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial void _monodroid_detect_cpu_and_architecture (ref ushort built_for_cpu, ref ushort running_on_cpu, ref byte is64bit);

		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial void _monodroid_gc_wait_for_bridge_processing ();

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial int _monodroid_gref_log (string message);

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial int _monodroid_gref_log_new (IntPtr curHandle, byte curType, IntPtr newHandle, byte newType, string? threadName, int threadId, string? from, int from_writable);

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial void _monodroid_gref_log_delete (IntPtr handle, byte type, string? threadName, int threadId, string? from, int from_writable);

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial void _monodroid_weak_gref_new (IntPtr curHandle, byte curType, IntPtr newHandle, byte newType, string? threadName, int threadId, string? from, int from_writable);

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial void _monodroid_weak_gref_delete (IntPtr handle, byte type, string? threadName, int threadId, string? from, int from_writable);

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial int _monodroid_lref_log_new (int lrefc, IntPtr handle, byte type, string? threadName, int threadId, string from, int from_writable);

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial void _monodroid_lref_log_delete (int lrefc, IntPtr handle, byte type, string? threadName, int threadId, string from, int from_writable);

		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial IntPtr monodroid_TypeManager_get_java_class_name (IntPtr klass);

		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial int _monodroid_max_gref_get ();

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial IntPtr clr_typemap_managed_to_java (string fullName, IntPtr mvid);

		[LibraryImport (RuntimeConstants.InternalDllName, StringMarshalling = StringMarshalling.Utf8)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		[return: MarshalAs (UnmanagedType.U1)]
		internal static partial bool clr_typemap_java_to_managed (string java_type_name, out IntPtr managed_assembly_name, out uint managed_type_token_id);

		[LibraryImport (RuntimeConstants.InternalDllName)]
		[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
		internal static partial delegate* unmanaged<MarkCrossReferencesArgs*, void> clr_initialize_gc_bridge (
			delegate* unmanaged<MarkCrossReferencesArgs*, void> bridge_processing_started_callback,
			delegate* unmanaged<MarkCrossReferencesArgs*, void> bridge_processing_finished_callback);

		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		internal static extern void monodroid_unhandled_exception (Exception javaException);

		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		internal static extern unsafe void monodroid_debugger_unhandled_exception (Exception e);
	}
}
#endif // INSIDE_MONO_ANDROID_RUNTIME
