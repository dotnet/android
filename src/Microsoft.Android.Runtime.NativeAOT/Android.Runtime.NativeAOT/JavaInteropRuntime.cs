using Android.Runtime;
using Java.Interop;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Android.Runtime;

static partial class JavaInteropRuntime
{
	static JniRuntime? runtime;

	[LibraryImport ("xa-internal-api")]
	[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
	private static partial int XA_Host_NativeAOT_JNI_OnLoad (IntPtr vm, IntPtr reserved, ref JNIEnvInit.JnienvInitializeArgs initArgs);

	[UnmanagedCallersOnly (EntryPoint="JNI_OnLoad")]
	static int JNI_OnLoad (IntPtr vm, IntPtr reserved)
	{
		try {
			AndroidLog.Print (AndroidLogLevel.Info, "JavaInteropRuntime", "JNI_OnLoad()");
			var initArgs = new JNIEnvInit.JnienvInitializeArgs ();
			XA_Host_NativeAOT_JNI_OnLoad (vm, reserved, ref initArgs);
			JNIEnvInit.InitializeMaxGrefCounts (initArgs);
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

	[LibraryImport ("xa-internal-api")]
	[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
	private static partial void XA_Host_NativeAOT_OnInit (IntPtr language, IntPtr filesDir, IntPtr cacheDir, ref JNIEnvInit.JnienvInitializeArgs initArgs);

	// symbol name from `$(IntermediateOutputPath)obj/Release/osx-arm64/h-classes/net_dot_jni_hello_JavaInteropRuntime.h`
	[UnmanagedCallersOnly (EntryPoint="Java_net_dot_jni_nativeaot_JavaInteropRuntime_init")]
	static void init (IntPtr jnienv, IntPtr klass, IntPtr classLoader, IntPtr language, IntPtr filesDir, IntPtr cacheDir)
	{
		JniTransition   transition  = default;
		try {
			var initArgs = new JNIEnvInit.JnienvInitializeArgs ();

			// This needs to be called first, since it sets up locations, environment variables, logging etc
			XA_Host_NativeAOT_OnInit (language, filesDir, cacheDir, ref initArgs);
			JNIEnvInit.InitializeBeforeRuntimeCreation (initArgs);

			var options = new NativeAotRuntimeOptions {
				EnvironmentPointer = jnienv,
				ClassLoader        = new JniObjectReference (classLoader, JniObjectReferenceType.Global),
				TypeManager        = JNIEnvInit.CreateTypeManager (),
				ValueManager       = JNIEnvInit.CreateValueManager (),
			};
			runtime = options.CreateJreVM ();

			// Entry point into Mono.Android.dll for NativeAOT-specific JNI runtime initialization.
			JNIEnvInit.InitializeNativeAotRuntime (runtime, initArgs);

			SetAppContextBaseDirectory (filesDir);

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

	// MonoVM and CoreCLR hand `APP_CONTEXT_BASE_DIRECTORY` to the runtime as a host property, but
	// NativeAOT has no such property bag: without this, `AppContext.BaseDirectory` falls back to the
	// directory of `Environment.ProcessPath`, which is `/system/bin/` (where `app_process64` lives).
	static void SetAppContextBaseDirectory (IntPtr filesDir)
	{
		string? baseDirectory = JniEnvironment.Strings.ToString (filesDir);
		if (string.IsNullOrEmpty (baseDirectory)) {
			return;
		}

		// .NET always terminates `AppContext.BaseDirectory` with a directory separator.
		if (!baseDirectory.EndsWith ('/')) {
			baseDirectory += "/";
		}
		AppContext.SetData ("APP_CONTEXT_BASE_DIRECTORY", baseDirectory);
	}
}
