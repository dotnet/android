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
			public bool            managedMarshalMethodsLookupEnabled;
			public IntPtr          propagateUncaughtExceptionFn;
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

		[UnmanagedCallersOnly]
		static void PropagateUncaughtException (IntPtr env, IntPtr javaThread, IntPtr javaException)
		{
			JNIEnv.PropagateUncaughtException (env, javaThread, javaException);
		}

		[UnmanagedCallersOnly]
		static unsafe void RegisterJniNatives (IntPtr typeName_ptr, int typeName_len, IntPtr jniClass, IntPtr methods_ptr, int methods_len)
		{
			// FIXME: https://github.com/xamarin/xamarin-android/issues/8724
			[UnconditionalSuppressMessage ("Trimming", "IL2057", Justification = "Type should be preserved by the MarkJavaObjects trimmer step.")]
			[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes)]
			static Type TypeGetType (string typeName) =>
				Type.GetType (typeName, throwOnError: false);

			string typeName = new string ((char*) typeName_ptr, 0, typeName_len);
			var type = TypeGetType (typeName);
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
			// Should not be allowed
			if (RuntimeFeature.IsMonoRuntime && RuntimeFeature.IsCoreClrRuntime) {
				throw new NotSupportedException ("Internal error: both RuntimeFeature.IsMonoRuntime and RuntimeFeature.IsCoreClrRuntime are enabled");
			}

			IntPtr total_timing_sequence = IntPtr.Zero;
			IntPtr partial_timing_sequence = IntPtr.Zero;

			Logger.SetLogCategories ((LogCategories)args->logCategories);

			gref_gc_threshold = args->grefGcThreshold;

			jniRemappingInUse = args->jniRemappingInUse;
			MarshalMethodsEnabled = args->marshalMethodsEnabled;
			java_class_loader = args->grefLoader;

			BoundExceptionType = (BoundExceptionType)args->ioExceptionType;
			JniRuntime.JniTypeManager typeManager;
			JniRuntime.JniValueManager valueManager;
			if (RuntimeFeature.ManagedTypeMap) {
				typeManager     = new ManagedTypeManager ();
			} else {
				typeManager     = new AndroidTypeManager (args->jniAddNativeMethodRegistrationAttributePresent != 0);
			}
			if (RuntimeFeature.IsMonoRuntime) {
				valueManager = new AndroidValueManager ();
			} else if (RuntimeFeature.IsCoreClrRuntime) {
				valueManager = ManagedValueManager.GetOrCreateInstance ();
			} else {
				throw new NotSupportedException ("Internal error: unknown runtime not supported");
			}
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

			if (args->managedMarshalMethodsLookupEnabled) {
				delegate* unmanaged <int, int, int, IntPtr*, void> getFunctionPointer = &ManagedMarshalMethodsLookupTable.GetFunctionPointer;
				xamarin_app_init (args->env, getFunctionPointer);
			}

			args->propagateUncaughtExceptionFn = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&PropagateUncaughtException;

			SetSynchronizationContext ();
		}

		[DllImport (RuntimeConstants.InternalDllName, CallingConvention = CallingConvention.Cdecl)]
		static extern unsafe void xamarin_app_init (IntPtr env, delegate* unmanaged <int, int, int, IntPtr*, void> get_function_pointer);

		static void SetSynchronizationContext () =>
			SynchronizationContext.SetSynchronizationContext (Android.App.Application.SynchronizationContext);
	}
}
