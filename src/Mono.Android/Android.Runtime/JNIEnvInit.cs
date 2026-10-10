using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;

using Microsoft.Android.Runtime;
using RuntimeFeature = Microsoft.Android.Runtime.RuntimeFeature;

namespace Android.Runtime
{
	static internal partial class JNIEnvInit
	{
#pragma warning disable 0649
		// NOTE: Keep this in sync with the native side in src/native/common/include/managed-interface.hh
		internal struct JnienvInitializeArgs {
			public IntPtr          javaVm;
			public IntPtr          env;
			public IntPtr          grefLoader;
			public uint            logCategories;
			public int             grefGcThreshold;
			public IntPtr          grefIGCUserPeer;
			public byte            brokenExceptionTransitions;
			public int             packageNamingPolicy;
			public byte            ioExceptionType;
			public IntPtr          jniRemappingData;
			public IntPtr          grefGCUserPeerable;
			public IntPtr          propagateUncaughtExceptionFn;
			public int             maxGrefCount;
		}
#pragma warning restore 0649

		internal static bool PropagateExceptions;
		internal static BoundExceptionType BoundExceptionType;
		internal static int gref_gc_threshold;
		internal static int max_gref_count;
		internal static IntPtr grefIGCUserPeer_class;

		internal static JniRuntime? androidRuntime;

		[UnmanagedCallersOnly]
		static void PropagateUncaughtException (IntPtr env, IntPtr javaThread, IntPtr javaException)
		{
			JNIEnv.PropagateUncaughtException (env, javaThread, javaException);
		}

		internal static void InitializeBeforeRuntimeCreation (JnienvInitializeArgs args)
		{
			InitializeCommonState (args);
			TypeMapLoader.Initialize ();
		}

		internal static void InitializeMaxGrefCounts (JnienvInitializeArgs args)
		{
			gref_gc_threshold = args.grefGcThreshold;
			max_gref_count = args.maxGrefCount;
		}

		// NOTE: should have different name than `Initialize` to avoid:
		// * Assertion at /__w/1/s/src/mono/mono/metadata/icall.c:6258, condition `!only_unmanaged_callers_only' not met
		// Only used for NativeAOT after the runtime has been created. CoreCLR uses Initialize().
		internal static void InitializeNativeAotRuntime (JniRuntime runtime)
		{
			if (!RuntimeFeature.IsNativeAotRuntime) {
				throw new NotSupportedException ("JNIEnvInit.InitializeNativeAotRuntime can only be used to initialize NativeAOT.");
			}
			if (RuntimeFeature.IsCoreClrRuntime) {
				throw new NotSupportedException ("Internal error: NativeAOT cannot be enabled with CoreCLR.");
			}

			if (RuntimeFeature.StartupNoGCRegion) {
				StartupNoGCRegion.Start ();
			}
			androidRuntime = runtime;
			JniRuntime.SetCurrent (runtime);
			JavaMarshalRegisteredPeers.InitializeIfNeeded ();
			TrimmableTypeMap.RegisterNativeMethods ();
			SetSynchronizationContext ();
		}

		// Only used for CoreCLR. NativeAOT uses InitializeNativeAotRuntime().
		[UnmanagedCallersOnly]
		internal static unsafe void Initialize (JnienvInitializeArgs* args)
		{
			if (RuntimeFeature.IsNativeAotRuntime) {
				throw new NotSupportedException ("JNIEnvInit.Initialize cannot be used to initialize NativeAOT.");
			}
			if (!RuntimeFeature.IsCoreClrRuntime) {
				throw new NotSupportedException ("Internal error: CoreCLR must be enabled.");
			}

			if (RuntimeFeature.StartupNoGCRegion) {
				StartupNoGCRegion.Start ();
			}

			InitializeBeforeRuntimeCreation (*args);

			androidRuntime = new AndroidRuntime (
					args->env,
					args->javaVm,
					args->grefLoader,
					new TrimmableTypeMapTypeManager (),
					new TrimmableTypeMapValueManager ()
			);
			JniRuntime.SetCurrent (androidRuntime);
			JavaMarshalRegisteredPeers.InitializeIfNeeded ();
			TrimmableTypeMap.RegisterNativeMethods ();

			args->propagateUncaughtExceptionFn = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&PropagateUncaughtException;

			RunStartupHooksIfNeeded ();
			SetSynchronizationContext ();
		}

		static void InitializeCommonState (JnienvInitializeArgs args)
		{
			Logger.SetLogCategories ((LogCategories)args.logCategories);

			InitializeMaxGrefCounts (args);
			if (RuntimeFeature.JniRemapping) {
				JniRemappingLookup.Initialize (args.jniRemappingData);
			}
			BoundExceptionType = (BoundExceptionType)args.ioExceptionType;
			grefIGCUserPeer_class = args.grefIGCUserPeer;
			PropagateExceptions = args.brokenExceptionTransitions == 0;
			JavaNativeTypeManager.PackageNamingPolicy = (PackageNamingPolicy)args.packageNamingPolicy;
		}

		static void RunStartupHooksIfNeeded ()
		{
			// Return if startup hooks are disabled or not CoreCLR
			if (!RuntimeFeature.IsCoreClrRuntime)
				return;
			if (!RuntimeFeature.StartupHookSupport)
				return;

			RunStartupHooks ();
		}

		[RequiresUnreferencedCode ("Uses reflection to access System.StartupHookProvider.")]
		static void RunStartupHooks ()
		{
			const string typeName = "System.StartupHookProvider";
			const string methodName = "ProcessStartupHooks";

			var type = typeof(object).Assembly.GetType (typeName, throwOnError: false);
			if (type is null) {
				RuntimeNativeMethods.monodroid_log (LogLevel.Warn, LogCategories.Default,
					$"Could not load type '{typeName}'. Skipping startup hooks.");
				return;
			}

			var method = type.GetMethod (methodName, 
				BindingFlags.NonPublic | BindingFlags.Static, null, [ typeof(string) ], null);
			if (method is null) {
				RuntimeNativeMethods.monodroid_log (LogLevel.Warn, LogCategories.Default,
					$"Could not load method '{typeName}.{methodName}'. Skipping startup hooks.");
				return;
			}

			// ProcessStartupHooks accepts startup hooks directly via parameter.
			// It will also read STARTUP_HOOKS from AppContext internally.
			// Pass DOTNET_STARTUP_HOOKS env var value so it works without needing AppContext setup.
			string? startupHooks = Environment.GetEnvironmentVariable ("DOTNET_STARTUP_HOOKS");
			method.Invoke (null, [ startupHooks ?? "" ]);
		}

		static void SetSynchronizationContext () =>
			SynchronizationContext.SetSynchronizationContext (Android.App.Application.SynchronizationContext);
	}
}
