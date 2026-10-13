using Android.Runtime;
using Java.Interop;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Android.Runtime;

static partial class JavaInteropRuntime
{
	static JniRuntime? runtime;

	/// <safety>
	/// <paramref name="vm"/> must be the live JavaVM supplied by JNI_OnLoad; <paramref name="reserved"/>
	/// must obey that callback's contract. <paramref name="initArgs"/> must match the native initialization layout.
	/// </safety>
	[LibraryImport ("xa-internal-api")]
	[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
	private static unsafe partial int XA_Host_NativeAOT_JNI_OnLoad (IntPtr vm, IntPtr reserved, ref JNIEnvInit.JnienvInitializeArgs initArgs);

	/// <safety>
	/// <paramref name="vm"/> must be the live JavaVM supplied by JNI_OnLoad and remain live for the cryptography
	/// library lifetime. <paramref name="reserved"/> must obey the JNI_OnLoad callback contract.
	/// </safety>
	[LibraryImport ("System.Security.Cryptography.Native.Android", EntryPoint = "AndroidCryptoNative_InitLibraryOnLoad")]
	[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
	private static unsafe partial int AndroidCryptoNative_InitLibraryOnLoad (IntPtr vm, IntPtr reserved);

	[UnmanagedCallersOnly (EntryPoint="JNI_OnLoad")]
	static int JNI_OnLoad (IntPtr vm, IntPtr reserved)
	{
		try {
			AndroidLog.Print (AndroidLogLevel.Info, "JavaInteropRuntime", "JNI_OnLoad()");
			var initArgs = new JNIEnvInit.JnienvInitializeArgs ();
			int hostVersion;
			unsafe {
				// SAFETY: the VM invokes JNI_OnLoad with its live JavaVM; the stub pins the matching descriptor.
				hostVersion = XA_Host_NativeAOT_JNI_OnLoad (vm, reserved, ref initArgs);
			}
			if (hostVersion < 0) {
				return hostVersion;
			}
			// The crypto initializer caches Java classes using JNI_OnLoad's application class loader.
			int cryptoVersion;
			unsafe {
				// SAFETY: the same VM-owned JavaVM remains live while the library caches its JNI classes.
				cryptoVersion = AndroidCryptoNative_InitLibraryOnLoad (vm, reserved);
			}
			if (cryptoVersion < 0) {
				AndroidLog.Print (AndroidLogLevel.Error, "JavaInteropRuntime", "Cryptography JNI initialization failed");
				return cryptoVersion;
			}
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

	/// <safety>
	/// <paramref name="language"/>, <paramref name="filesDir"/> and <paramref name="cacheDir"/> must be null
	/// or live JNI string references on the current thread for the call. <paramref name="initArgs"/> must match
	/// the native descriptor layout; its native data and global references have runtime/process lifetimes.
	/// </safety>
	[LibraryImport ("xa-internal-api")]
	[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
	private static unsafe partial void XA_Host_NativeAOT_OnInit (IntPtr language, IntPtr filesDir, IntPtr cacheDir, ref JNIEnvInit.JnienvInitializeArgs initArgs);

	// symbol name from `$(IntermediateOutputPath)obj/Release/osx-arm64/h-classes/net_dot_jni_hello_JavaInteropRuntime.h`
	[UnmanagedCallersOnly (EntryPoint="Java_net_dot_jni_nativeaot_JavaInteropRuntime_init")]
	static void init (IntPtr jnienv, IntPtr klass, IntPtr classLoader, IntPtr language, IntPtr filesDir, IntPtr cacheDir)
	{
		JniTransition   transition  = default;
		try {
			var initArgs = new JNIEnvInit.JnienvInitializeArgs ();

			// This needs to be called first, since it sets up locations, environment variables, logging etc
			unsafe {
				// SAFETY: JNI supplies borrowed strings for this callback; native code duplicates them before ownership transfer.
				XA_Host_NativeAOT_OnInit (language, filesDir, cacheDir, ref initArgs);
			}
			unsafe {
				// SAFETY: the host returned process-lifetime remapping storage and runtime-lifetime global references.
				JNIEnvInit.InitializeBeforeRuntimeCreation (initArgs);
			}

			var options = new NativeAotRuntimeOptions {
				EnvironmentPointer = jnienv,
				ClassLoader        = new JniObjectReference (classLoader, JniObjectReferenceType.Global),
				TypeManager                 = new TrimmableTypeMapTypeManager (),
				ValueManager                = new TrimmableTypeMapValueManager (),
			};
			unsafe {
				// SAFETY: JNI retains this thread's environment and borrowed loader through the callback; creation duplicates the loader and the VM outlives the runtime.
				runtime = options.CreateJreVM ();
			}

			// Entry point into Mono.Android.dll for NativeAOT-specific JNI runtime initialization.
			JNIEnvInit.InitializeNativeAotRuntime (runtime);

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
