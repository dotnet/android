using Android.Runtime;
using Java.Interop;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Android.Runtime;

static partial class JavaInteropRuntime
{
	static JniRuntime? runtime;
	static TypeMapAttributeTypeMap? typeMap;

	// Delegate type for typemap_get_function_pointer callback
	unsafe delegate void GetFunctionPointerCallback (byte* classNamePtr, int classNameLength, int methodIndex, IntPtr* outputPtr);

	// Keep delegate alive to prevent GC
	static GetFunctionPointerCallback? _getFunctionPointerDelegate;
	static IntPtr _getFunctionPointerPtr;

	[DllImport("xa-internal-api")]
	static extern int XA_Host_NativeAOT_JNI_OnLoad (IntPtr vm, IntPtr reserved);

	// Import dlsym and dlopen for looking up the global symbol
	const int RTLD_DEFAULT = 0;
	[DllImport("dl")]
	static extern IntPtr dlsym (IntPtr handle, string symbol);

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

	[DllImport("xa-internal-api")]
	static extern void XA_Host_NativeAOT_OnInit (IntPtr language, IntPtr filesDir, IntPtr cacheDir, ref JNIEnvInit.JnienvInitializeArgs initArgs);

	// symbol name from `$(IntermediateOutputPath)obj/Release/osx-arm64/h-classes/net_dot_jni_hello_JavaInteropRuntime.h`
	[UnmanagedCallersOnly (EntryPoint="Java_net_dot_jni_nativeaot_JavaInteropRuntime_init")]
	static void init (IntPtr jnienv, IntPtr klass, IntPtr classLoader, IntPtr language, IntPtr filesDir, IntPtr cacheDir)
	{
		JniTransition   transition  = default;
		try {
			var initArgs = new JNIEnvInit.JnienvInitializeArgs ();

			// This needs to be called first, since it sets up locations, environment variables, logging etc
			XA_Host_NativeAOT_OnInit (language, filesDir, cacheDir, ref initArgs);
			JNIEnvInit.InitializeJniRuntimeEarly (initArgs);

			var settings    = new DiagnosticSettings ();
			settings.AddDebugDotnetLog ();

			typeMap = new TypeMapAttributeTypeMap ();

			// Initialize the typemap_get_function_pointer callback for LLVM IR marshal methods
			InitializeTypemapGetFunctionPointer ();

			var options = new NativeAotRuntimeOptions {
				EnvironmentPointer          = jnienv,
				ClassLoader                 = new JniObjectReference (classLoader, JniObjectReferenceType.Global),
				TypeManager                 = new AndroidTypeManager (typeMap),
				ValueManager                = new ManagedValueManager (typeMap),
				UseMarshalMemberBuilder     = false,
				JniGlobalReferenceLogWriter = settings.GrefLog,
				JniLocalReferenceLogWriter  = settings.LrefLog,
			};
			runtime = options.CreateJreVM ();

			// Entry point into Mono.Android.dll. Log categories are initialized in JNI_OnLoad.
			JNIEnvInit.InitializeJniRuntime (runtime, initArgs);

			transition  = new JniTransition (jnienv);

			// TODO: UncaughtExceptionMarshaler currently fails with TypeMap V3 due to
			// Java.Interop.JavaProxyThrowable trying to register native members dynamically.
			// Skipping this for now to get the basic app running.
			// var handler = Java.Lang.Thread.DefaultUncaughtExceptionHandler;
			// Java.Lang.Thread.DefaultUncaughtExceptionHandler = new UncaughtExceptionMarshaler (handler);
		}
		catch (Exception e) {
			AndroidLog.Print (AndroidLogLevel.Error, "JavaInteropRuntime", $"JavaInteropRuntime.init: error: {e}");
			transition.SetPendingException (e);
		}
		transition.Dispose ();
	}

	/// <summary>
	/// Initialize the typemap_get_function_pointer global callback that LLVM IR marshal methods use
	/// to get managed method pointers.
	/// </summary>
	static unsafe void InitializeTypemapGetFunctionPointer ()
	{
		// Create the callback delegate and get its function pointer
		_getFunctionPointerDelegate = new GetFunctionPointerCallback (TypemapGetFunctionPointerImpl);
		_getFunctionPointerPtr = Marshal.GetFunctionPointerForDelegate (_getFunctionPointerDelegate);

		// Use dlsym to find the global variable address
		// RTLD_DEFAULT (0) searches all loaded shared libraries
		IntPtr globalAddr = dlsym (IntPtr.Zero, "typemap_get_function_pointer");
		if (globalAddr == IntPtr.Zero) {
			AndroidLog.Print (AndroidLogLevel.Error, "JavaInteropRuntime", "Failed to find typemap_get_function_pointer symbol");
			return;
		}

		// The address points to the global variable (which holds a pointer)
		// We need to write our function pointer to this location
		*(IntPtr*)globalAddr = _getFunctionPointerPtr;
		AndroidLog.Print (AndroidLogLevel.Info, "JavaInteropRuntime", $"Initialized typemap_get_function_pointer callback at 0x{globalAddr:X}");
	}

	/// <summary>
	/// Callback implementation that the LLVM IR marshal methods call to get managed method pointers.
	/// </summary>
	static unsafe void TypemapGetFunctionPointerImpl (byte* classNamePtr, int classNameLength, int methodIndex, IntPtr* outputPtr)
	{
		try {
			// Convert the class name from bytes
			string javaClassName = Encoding.UTF8.GetString (classNamePtr, classNameLength);

			// Look up the proxy type and get its function pointer
			IntPtr fnPtr = typeMap?.GetFunctionPointer (javaClassName, methodIndex) ?? IntPtr.Zero;
			*outputPtr = fnPtr;

			if (fnPtr == IntPtr.Zero) {
				AndroidLog.Print (AndroidLogLevel.Error, "JavaInteropRuntime",
					$"TypemapGetFunctionPointer: no function pointer for {javaClassName}[{methodIndex}]");
			}
		}
		catch (Exception e) {
			AndroidLog.Print (AndroidLogLevel.Error, "JavaInteropRuntime",
				$"TypemapGetFunctionPointer failed: {e}");
			*outputPtr = IntPtr.Zero;
		}
	}
}
