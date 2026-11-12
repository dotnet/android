using Android.Runtime;
using Java.Interop;
using System.Globalization;
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
	private static unsafe partial int NativeAOT_JNI_OnLoad (IntPtr vm, IntPtr reserved, ref JNIEnvInit.JnienvInitializeArgs initArgs);

	/// <safety>
	/// <paramref name="vm"/> must be the live JavaVM supplied by JNI_OnLoad and remain live for the cryptography
	/// library lifetime. <paramref name="reserved"/> must obey the JNI_OnLoad callback contract.
	/// </safety>
	[LibraryImport ("System.Security.Cryptography.Native.Android", EntryPoint = "AndroidCryptoNative_InitLibraryOnLoad")]
	[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
	private static unsafe partial int AndroidCryptoNative_InitLibraryOnLoad (IntPtr vm, IntPtr reserved);

	/// <safety>
	/// The VM must invoke this callback with its live JavaVM on an attached thread and the JNI_OnLoad
	/// reserved argument. The VM must remain live for the lifetime of the loaded library.
	/// </safety>
	[UnmanagedCallersOnly (EntryPoint="JNI_OnLoad")]
	static unsafe int JNI_OnLoad (IntPtr vm, IntPtr reserved)
	{
		try {
			AndroidLog.Print (AndroidLogLevel.Info, "JavaInteropRuntime", "JNI_OnLoad()");
			var initArgs = new JNIEnvInit.JnienvInitializeArgs ();
			// SAFETY: the VM invokes JNI_OnLoad with its live JavaVM; the stub pins the matching descriptor.
			int hostVersion = unsafe (NativeAOT_JNI_OnLoad (vm, reserved, ref initArgs));
			if (hostVersion < 0) {
				return hostVersion;
			}
			// The crypto initializer caches Java classes using JNI_OnLoad's application class loader.
			// SAFETY: the same VM-owned JavaVM remains live while the library caches its JNI classes.
			int cryptoVersion = unsafe (AndroidCryptoNative_InitLibraryOnLoad (vm, reserved));
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
	/// <paramref name="jnienv"/> must be the current thread's live JNI environment.
	/// <paramref name="language"/>, <paramref name="filesDir"/> and <paramref name="cacheDir"/> must be null
	/// or live JNI string references on the current thread for the call. <paramref name="initArgs"/> must match
	/// the native descriptor layout; its native data and global references have runtime/process lifetimes.
	/// </safety>
	[LibraryImport ("xa-internal-api")]
	[UnmanagedCallConv (CallConvs = new[] { typeof (CallConvCdecl) })]
	private static unsafe partial void NativeAOT_OnInit (IntPtr jnienv, IntPtr language, IntPtr filesDir, IntPtr cacheDir, ref JNIEnvInit.JnienvInitializeArgs initArgs);

	// symbol name from `$(IntermediateOutputPath)obj/Release/osx-arm64/h-classes/net_dot_jni_hello_JavaInteropRuntime.h`
	/// <safety>
	/// JNI must supply the current thread's live environment and live borrowed class and class-loader references
	/// for this callback. String references must be null or live for the callback. The VM must outlive the runtime created here.
	/// </safety>
	[UnmanagedCallersOnly (EntryPoint="Java_net_dot_jni_nativeaot_JavaInteropRuntime_init")]
	static unsafe void init (IntPtr jnienv, IntPtr klass, IntPtr classLoader, IntPtr language, IntPtr filesDir, IntPtr cacheDir, int localDateTimeOffset)
	{
		JniTransition   transition  = default;
		try {
			var initArgs = new JNIEnvInit.JnienvInitializeArgs ();

			// This needs to be called first, since it sets up locations, environment variables, logging etc
			unsafe {
				// SAFETY: JNI supplies borrowed strings for this callback; native code duplicates them before ownership transfer.
				NativeAOT_OnInit (jnienv, language, filesDir, cacheDir, ref initArgs);
			}
			// Seed DateTimeOffset.Now's fast path before managed runtime initialization or application code.
			AppContext.SetData ("System.TimeZoneInfo.LocalDateTimeOffset", localDateTimeOffset.ToString (CultureInfo.InvariantCulture));
			unsafe {
				// SAFETY: the host returned process-lifetime remapping storage and runtime-lifetime global references.
				JNIEnvInit.InitializeBeforeRuntimeCreation (initArgs);
			}

			var options = new NativeAotRuntimeOptions {
				EnvironmentPointer = jnienv,
				ClassLoader        = new JniObjectReference (classLoader, JniObjectReferenceType.Global),
				ObjectReferenceManager = new ManagedObjectReferenceManager (),
				TypeManager                 = new TrimmableTypeMapTypeManager (),
				ValueManager                = new TrimmableTypeMapValueManager (),
			};
			// SAFETY: JNI retains this thread's environment and borrowed loader through the callback; creation duplicates the loader and the VM outlives the runtime.
			runtime = unsafe (options.CreateJreVM ());

			// Entry point into Mono.Android.dll for NativeAOT-specific JNI runtime initialization.
			unsafe {
				// SAFETY: the host retains the descriptor's bridge initializer and global references for the process/runtime lifetime.
				JNIEnvInit.InitializeNativeAotRuntime (runtime, initArgs);
			}

			unsafe {
				// SAFETY: JNI keeps the borrowed filesDir string live until this callback returns.
				SetAppContextBaseDirectory (filesDir);
			}

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
	/// <safety>
	/// <paramref name="filesDir"/> must be null or a live JNI string reference on the current thread for the call.
	/// </safety>
	static unsafe void SetAppContextBaseDirectory (IntPtr filesDir)
	{
		// SAFETY: the caller keeps the JNI string live while ToString copies its contents.
		string? baseDirectory = unsafe (JniEnvironment.Strings.ToString (filesDir));
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
