using System;
using System.Diagnostics;
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
	static internal class JNIEnvInit
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
			public bool            jniRemappingInUse;
			public bool            marshalMethodsEnabled;
			public IntPtr          grefGCUserPeerable;
			// OUT
			public IntPtr          propagateUncaughtExceptionFn;
			// OUT
			public IntPtr          getFunctionPointerFn;
		}
#pragma warning restore 0649

		internal static bool jniRemappingInUse;
		internal static bool MarshalMethodsEnabled;
		internal static bool PropagateExceptions;
		internal static BoundExceptionType BoundExceptionType;
		internal static int gref_gc_threshold;
		internal static IntPtr grefIGCUserPeer_class;
		internal static IntPtr grefGCUserPeerable_class;
		internal static IntPtr java_class_loader;

		internal static JniRuntime? androidRuntime;
		internal static ITypeMap? TypeMap;

		[UnmanagedCallersOnly]
		static void PropagateUncaughtException (IntPtr env, IntPtr javaThread, IntPtr javaException)
		{
			JNIEnv.PropagateUncaughtException (env, javaThread, javaException);
		}

		[UnmanagedCallersOnly]
		static unsafe void RegisterJniNatives (IntPtr typeName_ptr, int typeName_len, IntPtr jniClass, IntPtr methods_ptr, int methods_len)
		{
			if (!RuntimeFeature.IsDynamicTypeRegistration) {
				throw new NotSupportedException ("Dynamic native member registration is not supported when TypeMap V3 is enabled. All types should be registered at build time.");
			}

			RegisterJniNativesCore (typeName_ptr, typeName_len, jniClass, methods_ptr, methods_len);
		}

		[RequiresUnreferencedCode ("Dynamic type registration uses Type.GetType() which cannot be statically analyzed.")]
		static unsafe void RegisterJniNativesCore (IntPtr typeName_ptr, int typeName_len, IntPtr jniClass, IntPtr methods_ptr, int methods_len)
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
			androidRuntime!.TypeManager.RegisterNativeMembers (jniType, type, methods);
		}

		// This must be called by NativeAOT before InitializeJniRuntime, as early as possible
		internal static void NativeAotInitializeMaxGrefGet ()
		{
			gref_gc_threshold = RuntimeNativeMethods._monodroid_max_gref_get ();
			if (gref_gc_threshold != int.MaxValue) {
				gref_gc_threshold = checked((gref_gc_threshold * 9) / 10);
			}
		}

		// This is needed to initialize e.g. logging before anything else (useful with e.g. gref
		// logging where runtime creation causes several grefs to be created and logged without
		// stack traces because logging categories on the managed side aren't yet set)
		internal static void InitializeJniRuntimeEarly (JnienvInitializeArgs args)
		{
			Logger.SetLogCategories ((LogCategories)args.logCategories);
		}

		// NOTE: should have different name than `Initialize` to avoid:
		// * Assertion at /__w/1/s/src/mono/mono/metadata/icall.c:6258, condition `!only_unmanaged_callers_only' not met
		internal static void InitializeJniRuntime (JniRuntime runtime, JnienvInitializeArgs args)
		{
			androidRuntime = runtime;
			SetSynchronizationContext ();
		}

		[UnmanagedCallersOnly]
		internal static unsafe void Initialize (JnienvInitializeArgs* args)
		{
			// VERY FIRST log - before anything else
			RuntimeNativeMethods.monodroid_log (LogLevel.Warn, LogCategories.Default, "JNIEnvInit: Initialize ENTRY POINT");

			// Should not be allowed
			if (RuntimeFeature.IsMonoRuntime && RuntimeFeature.IsCoreClrRuntime) {
				throw new NotSupportedException ("Internal error: both RuntimeFeature.IsMonoRuntime and RuntimeFeature.IsCoreClrRuntime are enabled");
			}

			RuntimeNativeMethods.monodroid_log (LogLevel.Warn, LogCategories.Default, "JNIEnvInit: After RuntimeFeature check");

			IntPtr total_timing_sequence = IntPtr.Zero;
			IntPtr partial_timing_sequence = IntPtr.Zero;

			Logger.SetLogCategories ((LogCategories)args->logCategories);

			gref_gc_threshold = args->grefGcThreshold;

			jniRemappingInUse = args->jniRemappingInUse;
			MarshalMethodsEnabled = args->marshalMethodsEnabled;
			java_class_loader = args->grefLoader;

			BoundExceptionType = (BoundExceptionType)args->ioExceptionType;

			try {
				RuntimeNativeMethods.monodroid_log (LogLevel.Info, LogCategories.Default, "JNIEnvInit: About to CreateTypeMap");
				TypeMap = CreateTypeMap ();
				RuntimeNativeMethods.monodroid_log (LogLevel.Info, LogCategories.Default, "JNIEnvInit: CreateTypeMap succeeded");
			} catch (Exception ex) {
				RuntimeNativeMethods.monodroid_log (LogLevel.Error, LogCategories.Default,
					$"JNIEnvInit: CreateTypeMap FAILED: {ex.GetType ().FullName}: {ex.Message}");
				RuntimeNativeMethods.monodroid_log (LogLevel.Error, LogCategories.Default,
					$"JNIEnvInit: Stack: {ex.StackTrace}");
				throw;
			}

			// Create unified managers using the type map
			var typeManager = CreateTypeManager (TypeMap);
			var valueManager = CreateValueManager (TypeMap);

			androidRuntime = new AndroidRuntime (
					args->env,
					args->javaVm,
					args->grefLoader,
					typeManager,
					valueManager,
					args->jniAddNativeMethodRegistrationAttributePresent != 0
			);

			grefIGCUserPeer_class = args->grefIGCUserPeer;
			grefGCUserPeerable_class = args->grefGCUserPeerable;

			PropagateExceptions = args->brokenExceptionTransitions == 0;

			JavaNativeTypeManager.PackageNamingPolicy = (PackageNamingPolicy)args->packageNamingPolicy;

			args->propagateUncaughtExceptionFn = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&PropagateUncaughtException;

			// Provide the GetFunctionPointer callback for Type Mapping API marshal methods
			args->getFunctionPointerFn = (IntPtr)(delegate* unmanaged<byte*, int, int, IntPtr*, void>)&GetFunctionPointer;
			RuntimeNativeMethods.monodroid_log (LogLevel.Info, LogCategories.Default,
				$"JNIEnvInit: Set getFunctionPointerFn to 0x{args->getFunctionPointerFn:x}");

			RunStartupHooksIfNeeded ();
			SetSynchronizationContext ();
		}

		[UnmanagedCallersOnly]
		internal static unsafe void GetFunctionPointer (byte* classNamePtr, int classNameLength, int methodIndex, IntPtr* targetPtr)
		{
			// The class name comes from LLVM IR as UTF-8 bytes
			ReadOnlySpan<byte> utf8Bytes = new ReadOnlySpan<byte>(classNamePtr, classNameLength);
			string className = System.Text.Encoding.UTF8.GetString (utf8Bytes);

			*targetPtr = TypeMap.GetFunctionPointer (className.AsSpan(), methodIndex);
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

			// Pass empty string for diagnosticStartupHooks parameter
			// The method will read STARTUP_HOOKS from AppContext internally
			method.Invoke (null, [ "" ]);
		}

		private static ITypeMap CreateTypeMap ()
		{
			if (RuntimeFeature.IsCoreClrRuntime) {
				// TypeMapping API requires an entry assembly to find TypeMap attributes.
				// Android apps don't have a traditional Main() entry point, so we set it explicitly.
				Assembly.SetEntryAssembly (typeof (Java.Lang.Object).Assembly); // TODO is this really still necessary?
				return new TypeMapAttributeTypeMap ();
			} else if (RuntimeFeature.IsMonoRuntime) {
				// PoC: Use TypeMap V3 for MonoVM too
				return new TypeMapAttributeTypeMap ();
			} else {
				throw new NotSupportedException ("Internal error: unknown runtime not supported");
			}
		}

		private static JniRuntime.JniTypeManager CreateTypeManager (ITypeMap typeMap)
		{
			if (RuntimeFeature.IsCoreClrRuntime) {
				// CoreCLR uses AndroidTypeManager - note that [Export] is NOT supported with TypeMap v3
				return new AndroidTypeManager (typeMap);
			}

			if (RuntimeFeature.IsMonoRuntime) {
				return new AndroidTypeManager (typeMap);
			}

			throw new NotSupportedException ("Internal error: unknown runtime not supported");
		}

		private static JniRuntime.JniValueManager CreateValueManager (ITypeMap typeMap)
		{
			if (RuntimeFeature.IsCoreClrRuntime) {
				// CoreCLR and NativeAOT both use ManagedValueManager with the CLR GC bridge
				return new ManagedValueManager (typeMap);
			}
			
			if (RuntimeFeature.IsMonoRuntime) {
				return new AndroidValueManager (typeMap);
			}
			
			throw new NotSupportedException ("Internal error: unknown runtime not supported");
		}

		static void SetSynchronizationContext () =>
			SynchronizationContext.SetSynchronizationContext (Android.App.Application.SynchronizationContext);
	}
}
