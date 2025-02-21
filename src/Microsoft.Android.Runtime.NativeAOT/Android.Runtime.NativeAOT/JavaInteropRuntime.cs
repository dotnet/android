using Android.Runtime;
using Java.Interop;
using System.Runtime.InteropServices;

namespace Microsoft.Android.Runtime;

[Flags]
enum DebugMonoLog {
	None,
	Gref = 1 << 0,
	Lref = 1 << 1,
	All  = Gref | Lref,
}

static partial class JavaInteropRuntime
{
	static JniRuntime? runtime;

	[UnmanagedCallersOnly (EntryPoint="JNI_OnLoad")]
	static int JNI_OnLoad (IntPtr vm, IntPtr reserved)
	{
		try {
			AndroidLog.Print (AndroidLogLevel.Info, "JavaInteropRuntime", "JNI_OnLoad()");
			LogcatTextWriter.Init ();
			return (int) JniVersion.v1_6;
		}
		catch (Exception e) {
			AndroidLog.Print (AndroidLogLevel.Error, "JavaInteropRuntime", $"JNI_OnLoad() failed: {e}");
			return 0;
		}
	}

	[UnmanagedCallersOnly (EntryPoint="JNI_OnUnload")]
	static void JNI_OnUnload (IntPtr vm, IntPtr reserved)
	{
		AndroidLog.Print(AndroidLogLevel.Info, "JavaInteropRuntime", "JNI_OnUnload");
		runtime?.Dispose ();
	}

	// symbol name from `$(IntermediateOutputPath)obj/Release/osx-arm64/h-classes/net_dot_jni_hello_JavaInteropRuntime.h`
	[UnmanagedCallersOnly (EntryPoint="Java_net_dot_jni_nativeaot_JavaInteropRuntime_init")]
	static void init (IntPtr jnienv, IntPtr klass)
	{
		try {
			var log = GetDebugMonoLog ();
			var typeManager = new NativeAotTypeManager ();
			var options = new NativeAotRuntimeOptions {
				EnvironmentPointer          = jnienv,
				TypeManager                 = typeManager,
				ValueManager                = new NativeAotValueManager (typeManager),
				UseMarshalMemberBuilder     = false,
				JniGlobalReferenceLogWriter = log.HasFlag (DebugMonoLog.Gref) ? new LogcatTextWriter (AndroidLogLevel.Debug, "NativeAot:GREF") : null,
				JniLocalReferenceLogWriter  = log.HasFlag (DebugMonoLog.Lref) ? new LogcatTextWriter (AndroidLogLevel.Debug, "NativeAot:LREF") : null,
			};
			runtime = options.CreateJreVM ();

			// Entry point into Mono.Android.dll
			JNIEnvInit.InitializeJniRuntime (runtime);
		}
		catch (Exception e) {
			AndroidLog.Print (AndroidLogLevel.Error, "JavaInteropRuntime", $"JavaInteropRuntime.init: error: {e}");
		}
	}

	[LibraryImport ("c", EntryPoint="__system_property_get")]
	static private partial int GetSystemProperty (ReadOnlySpan<byte> name, Span<byte> value);

	static DebugMonoLog GetDebugMonoLog ()
	{
		Span<byte>  buffer  = stackalloc byte [PROP_VALUE_MAX];
		int len     = GetSystemProperty ("debug.mono.log"u8, buffer);
		if (len <= 0) {
			return DebugMonoLog.None;
		}

		ReadOnlySpan<byte>  value   = buffer;
		value = value.Slice (0, len);

		return Parse (value);
	}

	const int PROP_VALUE_MAX = 92;

	static DebugMonoLog Parse (ReadOnlySpan<byte> value)
	{
		DebugMonoLog log = default;

		// warning CS9087: This returns a parameter by reference 'value' but it is not a ref parameter
		// Use of `ref` is for my purposes, and shouldn't be visible to callers.
#pragma warning disable CS9087
		ReadOnlySpan<byte> v;
		while (value.Length > 0 && (v = GetNextValue (ref value)).Length > 0) {
			if (v.SequenceEqual ("lref"u8))
				log |= DebugMonoLog.Lref;
			else if (v.SequenceEqual ("gref"u8))
				log |= DebugMonoLog.Gref;
			else if (v.SequenceEqual ("all"u8))
				log |= DebugMonoLog.All;
		}
#pragma warning restore CS9087
		return log;

		ReadOnlySpan<byte> GetNextValue (ref ReadOnlySpan<byte> value)
		{
			int c = value.IndexOf ((byte) ',');
			if (c >= 0) {
				var n = value.Slice (0, c);
				value = value.Slice (c + 1);
				return n;
			}
			else {
				var n = value;
				value = default;
				return n;
			}
		}
	}
}