using Android.Runtime;
using Java.Interop;
using System.Runtime.InteropServices;

namespace Microsoft.Android.Runtime;

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
	static void init (IntPtr jnienv, IntPtr klass, IntPtr classLoader)
	{
		JniTransition   transition  = default;
		try {
			var settings    = new DiagnosticSettings ();
			settings.AddDebugDotnetLog ();

			var options = new NativeAotRuntimeOptions {
				EnvironmentPointer          = jnienv,
				ClassLoader                 = new JniObjectReference (classLoader),
				TypeManager                 = new ManagedTypeManager (),
				ValueManager                = ManagedValueManager.Instance, // TODO this will likely blow up in AOT at runtime currently
				UseMarshalMemberBuilder     = false,
				JniGlobalReferenceLogWriter = settings.GrefLog,
				JniLocalReferenceLogWriter  = settings.LrefLog,
			};
			runtime = options.CreateJreVM ();

			// Entry point into Mono.Android.dll
			JNIEnvInit.InitializeJniRuntime (runtime);

			transition  = new JniTransition (jnienv);

			var handler = Java.Lang.Thread.DefaultUncaughtExceptionHandler;
			Java.Lang.Thread.DefaultUncaughtExceptionHandler = new UncaughtExceptionMarshaler (handler);
		}
		catch (Exception e) {
			AndroidLog.Print (AndroidLogLevel.Error, "JavaInteropRuntime", $"JavaInteropRuntime.init: error: {e}");
			transition.SetPendingException (e);
		}
		transition.Dispose ();
	}
}