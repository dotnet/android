using Android.Runtime;
using Java.Interop;
using System.Runtime.InteropServices;

namespace Microsoft.Android.Runtime;

static partial class JavaInteropRuntime
{
	static JniRuntime? runtime;

	[DllImport("xa-internal-api")]
	static extern int XA_Host_NativeAOT_JNI_OnLoad (IntPtr vm, IntPtr reserved);

	[UnmanagedCallersOnly (EntryPoint="JNI_OnLoad")]
	static int JNI_OnLoad (IntPtr vm, IntPtr reserved)
	{
		try {
			AndroidLog.Print (AndroidLogLevel.Info, "JavaInteropRuntime", "JNI_OnLoad()");
			XA_Host_NativeAOT_JNI_OnLoad (vm, reserved);
			// This must be called before anything else, otherwise we'll see several spurious GC invocations and log messages
			// similar to:
			//
			//  09-15 14:51:01.311 11071 11071 D monodroid-gc: 1 outstanding GREFs. Performing a full GC!
			//
			JNIEnvInit.NativeAotInitializeMaxGrefGet ();

			return (int) JniVersion.v1_6;
		}
		catch (Exception e) {
			AndroidLog.Print (AndroidLogLevel.Error, "JavaInteropRuntime", $"JNI_OnLoad() failed: {e}");
			return 0;
		}
	}

	[UnmanagedCallersOnly(EntryPoint = "JNI_OnUnload")]
	static void JNI_OnUnload (IntPtr vm, IntPtr reserved)
	{
		AndroidLog.Print(AndroidLogLevel.Info, "JavaInteropRuntime", "JNI_OnUnload");
		runtime?.Dispose ();
	}

	[DllImport("xa-internal-api")]
	static extern void XA_Host_NativeAOT_OnInit ();

	// symbol name from `$(IntermediateOutputPath)obj/Release/osx-arm64/h-classes/net_dot_jni_hello_JavaInteropRuntime.h`
	[UnmanagedCallersOnly (EntryPoint="Java_net_dot_jni_nativeaot_JavaInteropRuntime_init")]
	static void init (IntPtr jnienv, IntPtr klass, IntPtr classLoader)
	{
		JniTransition   transition  = default;
		try {
			var options = new NativeAotRuntimeOptions {
				EnvironmentPointer          = jnienv,
				ClassLoader                 = new JniObjectReference (classLoader, JniObjectReferenceType.Global),
				TypeManager                 = new ManagedTypeManager (),
				ValueManager                = ManagedValueManager.GetOrCreateInstance (),
				UseMarshalMemberBuilder     = false,
			};
			runtime = options.CreateJreVM ();

			// Entry point into Mono.Android.dll
			JNIEnvInit.InitializeJniRuntime (runtime);
			XA_Host_NativeAOT_OnInit ();

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
