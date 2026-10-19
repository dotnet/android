using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Java;
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
			public IntPtr          propagateUncaughtExceptionFn;
			public int             maxGrefCount;
			public delegate* unmanaged<delegate* unmanaged<MarkCrossReferencesArgs*, void>, delegate* unmanaged<MarkCrossReferencesArgs*, void>> initializeGCBridge;
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

		/// <safety>
		/// <paramref name="args"/> must come from the native host with the matching descriptor layout,
		/// process-lifetime remapping data, and runtime-lifetime global references.
		/// </safety>
		internal static unsafe void InitializeBeforeRuntimeCreation (JnienvInitializeArgs args)
		{
			unsafe {
				// SAFETY: the native host retains the remapping body and global references beyond initialization.
				InitializeCommonState (args);
			}
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
		/// <safety>
		/// <paramref name="args"/> must retain the native host's matching descriptor and process-lifetime
		/// GC-bridge initializer. Its VM and global references must remain live for the runtime lifetime.
		/// </safety>
		internal static unsafe void InitializeNativeAotRuntime (JniRuntime runtime, JnienvInitializeArgs args)
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
			unsafe {
				// SAFETY: the caller retains the native host's process-lifetime initializer and matching runtime descriptor.
				JavaMarshalGCBridge.Initialize (args);
			}
			TrimmableTypeMap.RegisterNativeMethods ();
			SetSynchronizationContext ();
		}

		// Only used for CoreCLR. NativeAOT uses InitializeNativeAotRuntime().
		/// <safety>
		/// <paramref name="args"/> must address the native host's readable and writable initialization descriptor
		/// for the call. Its VM must outlive the runtime, environment must belong to the current thread,
		/// and global references/remapping data must retain their documented runtime/process lifetimes.
		/// </safety>
		[UnmanagedCallersOnly]
		internal static unsafe void Initialize (JnienvInitializeArgs* args)
		{
			ArgumentNullException.ThrowIfNull (args);
			unsafe {
				// SAFETY: the native host calls this entry point with its matching live descriptor, current-thread environment and runtime-lifetime VM/global references.
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
				JavaMarshalGCBridge.Initialize (*args);
				TrimmableTypeMap.RegisterNativeMethods ();

				args->propagateUncaughtExceptionFn = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&PropagateUncaughtException;

				RunStartupHooksIfNeeded ();
				SetSynchronizationContext ();
			}
		}

		/// <safety>
		/// <paramref name="args"/> must retain its native remapping body for the process lifetime
		/// and its JNI global references for the runtime lifetime.
		/// </safety>
		static unsafe void InitializeCommonState (JnienvInitializeArgs args)
		{
			Logger.SetLogCategories ((LogCategories)args.logCategories);

			InitializeMaxGrefCounts (args);
			if (RuntimeFeature.JniRemapping) {
				unsafe {
					// SAFETY: the host supplies the process-lifetime descriptor and remapping body.
					JniRemappingLookup.Initialize (args.jniRemappingData);
				}
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
				Logger.Log (LogLevel.Warn, "monodroid",
					$"Could not load type '{typeName}'. Skipping startup hooks.");
				return;
			}

			var method = type.GetMethod (methodName, 
				BindingFlags.NonPublic | BindingFlags.Static, null, [ typeof(string) ], null);
			if (method is null) {
				Logger.Log (LogLevel.Warn, "monodroid",
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
