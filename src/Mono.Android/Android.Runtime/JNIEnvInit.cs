using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;

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
			public int             isRunningOnDesktop;
			public byte            brokenExceptionTransitions;
			public int             packageNamingPolicy;
			public byte            ioExceptionType;
			public int             jniAddNativeMethodRegistrationAttributePresent;
			public bool            jniRemappingInUse;
			public bool            marshalMethodsEnabled;
			public IntPtr          grefGCUserPeerable;
			public uint            runtimeType;
			public bool            managedMarshalMethodsLookupEnabled;
		}
#pragma warning restore 0649

		internal static JniRuntime.JniValueManager? ValueManager;
		internal static bool IsRunningOnDesktop;
		internal static bool jniRemappingInUse;
		internal static bool MarshalMethodsEnabled;
		internal static bool PropagateExceptions;
		internal static BoundExceptionType BoundExceptionType;
		internal static int gref_gc_threshold;
		internal static IntPtr grefIGCUserPeer_class;
		internal static IntPtr grefGCUserPeerable_class;
		internal static IntPtr java_class_loader;

		internal static JniRuntime? androidRuntime;

		public static DotNetRuntimeType RuntimeType { get; private set; } = DotNetRuntimeType.Unknown;

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
			((AndroidTypeManager)androidRuntime!.TypeManager).RegisterNativeMembers (jniType, type, methods);
		}

		// NOTE: should have different name than `Initialize` to avoid:
		// * Assertion at /__w/1/s/src/mono/mono/metadata/icall.c:6258, condition `!only_unmanaged_callers_only' not met
		internal static void InitializeJniRuntime (JniRuntime runtime)
		{
			androidRuntime = runtime;
			ValueManager = runtime.ValueManager;
		}

		[UnmanagedCallersOnly]
		internal static unsafe void Initialize (JnienvInitializeArgs* args)
		{
			// This looks weird, see comments in RuntimeTypeInternal.cs
			RuntimeType = DotNetRuntimeTypeConverter.Convert (args->runtimeType);
			InternalRuntimeTypeHolder.SetRuntimeType (args->runtimeType);

			IntPtr total_timing_sequence = IntPtr.Zero;
			IntPtr partial_timing_sequence = IntPtr.Zero;

			Logger.SetLogCategories ((LogCategories)args->logCategories);

			gref_gc_threshold = args->grefGcThreshold;

			jniRemappingInUse = args->jniRemappingInUse;
			MarshalMethodsEnabled = args->marshalMethodsEnabled;
			java_class_loader = args->grefLoader;

			BoundExceptionType = (BoundExceptionType)args->ioExceptionType;
			androidRuntime = new AndroidRuntime (args->env, args->javaVm, args->grefLoader, args->Loader_loadClass, args->jniAddNativeMethodRegistrationAttributePresent != 0);
			ValueManager = androidRuntime.ValueManager;

			IsRunningOnDesktop = args->isRunningOnDesktop == 1;

			grefIGCUserPeer_class = args->grefIGCUserPeer;
			grefGCUserPeerable_class = args->grefGCUserPeerable;

			PropagateExceptions = args->brokenExceptionTransitions == 0;

			JavaNativeTypeManager.PackageNamingPolicy = (PackageNamingPolicy)args->packageNamingPolicy;
			if (IsRunningOnDesktop) {
				var packageNamingPolicy = Environment.GetEnvironmentVariable ("__XA_PACKAGE_NAMING_POLICY__");
				if (Enum.TryParse (packageNamingPolicy, out PackageNamingPolicy pnp)) {
					JavaNativeTypeManager.PackageNamingPolicy = pnp;
				}
			}

			if (args->managedMarshalMethodsLookupEnabled) {
				delegate* unmanaged <int, int, int, IntPtr*, void> getFunctionPointer = &ManagedMarshalMethodsLookupTable.GetFunctionPointer;
				xamarin_app_init (args->env, getFunctionPointer);
			}

			SetSynchronizationContext ();
		}

		[DllImport ("xamarin-app")]
		static extern unsafe void xamarin_app_init (IntPtr env, delegate* unmanaged <int, int, int, IntPtr*, void> get_function_pointer);

		// NOTE: prevents Android.App.Application static ctor from running
		[MethodImpl (MethodImplOptions.NoInlining)]
		static void SetSynchronizationContext () =>
			SynchronizationContext.SetSynchronizationContext (Android.App.Application.SynchronizationContext);
	}
}
