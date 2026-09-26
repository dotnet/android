using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
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
			public IntPtr          Loader_loadClass;
			public IntPtr          grefClass; // TODO: remove, not needed anymore
			public uint            logCategories;
			public int             version; // TODO: remove, not needed anymore
			public int             grefGcThreshold;
			public IntPtr          grefIGCUserPeer;
			public byte            brokenExceptionTransitions;
			public int             packageNamingPolicy;
			public byte            ioExceptionType;
			public int             jniAddNativeMethodRegistrationAttributePresent;
			public IntPtr          jniRemappingData;
			public bool            marshalMethodsEnabled;
			public IntPtr          grefGCUserPeerable;
			public IntPtr          propagateUncaughtExceptionFn;
			public IntPtr          registerJniNativesFn;
			public IntPtr          grefLogPath;
			public IntPtr          lrefLogPath;
			public IntPtr          referenceLogDirectory;
			public byte            lightGref;
			public byte            lightLref;
			public byte            grefToLogcat;
			public byte            lrefToLogcat;
			public int             maxGrefCount;
		}
#pragma warning restore 0649

		internal static bool MarshalMethodsEnabled;
		internal static bool PropagateExceptions;
		internal static BoundExceptionType BoundExceptionType;
		internal static int gref_gc_threshold;
		internal static int max_gref_count;
		internal static IntPtr grefIGCUserPeer_class;
		internal static IntPtr grefGCUserPeerable_class;
		internal static IntPtr java_class_loader;
		internal static ReferenceLoggingConfiguration ReferenceLoggingConfiguration;

		internal static JniRuntime? androidRuntime;

		[UnmanagedCallersOnly]
		static void PropagateUncaughtException (IntPtr env, IntPtr javaThread, IntPtr javaException)
		{
			JNIEnv.PropagateUncaughtException (env, javaThread, javaException);
		}

		[UnmanagedCallersOnly]
		[RequiresUnreferencedCode ("Uses reflection to access System.StartupHookProvider.")]
		static unsafe void RegisterJniNatives (IntPtr typeName_ptr, int typeName_len, IntPtr jniClass, IntPtr methods_ptr, int methods_len)
		{
			string typeName = new string ((char*) typeName_ptr, 0, typeName_len);
			var type = Type.GetType (typeName, throwOnError: false);
			if (type == null) {
				RuntimeNativeMethods.monodroid_log (LogLevel.Error,
				               LogCategories.Default,
				               $"Could not load type '{typeName}'. Skipping JNI registration of type '{Java.Interop.TypeManager.GetClassName (jniClass)}'.");
				return;
			}

			var className = Java.Interop.TypeManager.GetClassName (jniClass);
			Java.Interop.TypeManager.RegisterType (className, type);

			JniType? jniType = null;
			JniType.GetCachedJniType (ref jniType, className);

			ReadOnlySpan<char> methods = new ReadOnlySpan<char> ((void*) methods_ptr, methods_len);
			if (androidRuntime is null) {
				throw new InvalidOperationException ("androidRuntime has not been initialized");
			}
			androidRuntime.TypeManager.RegisterNativeMembers (jniType, type, methods);
		}

		internal static void InitializeBeforeRuntimeCreation (JnienvInitializeArgs args)
		{
			InitializeCommonState (args);
			InitializeTrimmableTypeMapDataIfNeeded ();
		}

		internal static void InitializeMaxGrefCounts (JnienvInitializeArgs args)
		{
			gref_gc_threshold = args.grefGcThreshold;
			max_gref_count = args.maxGrefCount;
		}

		// NOTE: should have different name than `Initialize` to avoid:
		// * Assertion at /__w/1/s/src/mono/mono/metadata/icall.c:6258, condition `!only_unmanaged_callers_only' not met
		// Only used for NativeAOT after the runtime has been created. CoreCLR uses Initialize().
		internal static void InitializeNativeAotRuntime (JniRuntime runtime, JnienvInitializeArgs args)
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
			RegisterTrimmableTypeMapNativeMethodsIfNeeded ();
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

			IntPtr total_timing_sequence = IntPtr.Zero;
			IntPtr partial_timing_sequence = IntPtr.Zero;

			InitializeBeforeRuntimeCreation (*args);

			JniRuntime.JniTypeManager typeManager = CreateTypeManager (*args);
			JniRuntime.JniValueManager valueManager = CreateValueManager ();
			androidRuntime = new AndroidRuntime (
					args->env,
					args->javaVm,
					args->grefLoader,
					typeManager,
					valueManager,
					args->jniAddNativeMethodRegistrationAttributePresent != 0
			);
			JniRuntime.SetCurrent (androidRuntime);
			RegisterTrimmableTypeMapNativeMethodsIfNeeded ();

			args->propagateUncaughtExceptionFn = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&PropagateUncaughtException;

			if (!RuntimeFeature.TrimmableTypeMap) {
				args->registerJniNativesFn = GetRegisterJniNativesFnPtr ();
			}
			RunStartupHooksIfNeeded ();
			SetSynchronizationContext ();

			[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "This method is never used with the trimmable type map.")]
			IntPtr GetRegisterJniNativesFnPtr () =>
				(IntPtr)(delegate* unmanaged<IntPtr, int, IntPtr, IntPtr, int, void>)&RegisterJniNatives;
		}

		[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "The AndroidTypeManager branch is only reached when RuntimeFeature.TrimmableTypeMap is false; the linker substitutes the feature switch and trims this branch in trimmable apps.")]
		internal static JniRuntime.JniTypeManager CreateTypeManager (JnienvInitializeArgs args)
		{
			if (RuntimeFeature.TrimmableTypeMap) {
				return new TrimmableTypeMapTypeManager ();
			}

			return CreateAndroidTypeManager (args);

			[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "This type manager won't be used in Native AOT builds.")]
			[UnconditionalSuppressMessage ("Trimming", "IL3050", Justification = "This type manager won't be used in Native AOT builds.")]
			static JniRuntime.JniTypeManager CreateAndroidTypeManager (JnienvInitializeArgs args) => new AndroidTypeManager (args.jniAddNativeMethodRegistrationAttributePresent != 0);
		}

		internal static JniRuntime.JniValueManager CreateValueManager ()
		{
			if (RuntimeFeature.TrimmableTypeMap) {
				return new TrimmableTypeMapValueManager ();
			}

			return CreateJavaMarshalValueManager ();

			[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "CoreCLR value manager is preserved by the MarkJavaObjects trimmer step.")]
			[UnconditionalSuppressMessage ("Trimming", "IL3050", Justification = "This value manager won't be used in Native AOT builds in the future.")]
			JniRuntime.JniValueManager CreateJavaMarshalValueManager () => new JavaMarshalValueManager ();
		}

		static void InitializeCommonState (JnienvInitializeArgs args)
		{
			Logger.SetLogCategories ((LogCategories)args.logCategories);

			InitializeMaxGrefCounts (args);
			JniRemappingLookup.Initialize (args.jniRemappingData);
			MarshalMethodsEnabled = args.marshalMethodsEnabled;
			java_class_loader = args.grefLoader;

			BoundExceptionType = (BoundExceptionType)args.ioExceptionType;
			grefIGCUserPeer_class = args.grefIGCUserPeer;
			grefGCUserPeerable_class = args.grefGCUserPeerable;
			PropagateExceptions = args.brokenExceptionTransitions == 0;
			ReferenceLoggingConfiguration = new ReferenceLoggingConfiguration (
				Marshal.PtrToStringUTF8 (args.grefLogPath),
				Marshal.PtrToStringUTF8 (args.lrefLogPath),
				Marshal.PtrToStringUTF8 (args.referenceLogDirectory),
				args.lightGref != 0,
				args.lightLref != 0,
				args.grefToLogcat != 0,
				args.lrefToLogcat != 0);

			JavaNativeTypeManager.PackageNamingPolicy = (PackageNamingPolicy)args.packageNamingPolicy;
		}

		static void InitializeTrimmableTypeMapDataIfNeeded ()
		{
			if (RuntimeFeature.TrimmableTypeMap) {
				InitializeTrimmableTypeMapData ();
			}
		}

		static void RegisterTrimmableTypeMapNativeMethodsIfNeeded ()
		{
			if (RuntimeFeature.TrimmableTypeMap) {
				// TypeMapLoader.Initialize() only loads managed typemap data. Registering
				// mono.android.Runtime natives requires JniRuntime.Current and its ClassLoader.
				TrimmableTypeMap.RegisterNativeMethods ();
			}
		}

		// Separate method so the JIT doesn't try to resolve TypeMapLoader (from _Microsoft.Android.TypeMaps.dll)
		// when compiling JNIEnvInit.Initialize() in non-trimmable builds where that assembly isn't present.
		[MethodImpl (MethodImplOptions.NoInlining)]
		static void InitializeTrimmableTypeMapData ()
		{
			TypeMapLoader.Initialize ();
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
