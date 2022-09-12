using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;

namespace Android.Runtime
{
	static class JNIEnvInit
	{
#pragma warning disable 0649
		internal struct JnienvInitializeArgs {
			public IntPtr          javaVm;
			public IntPtr          env;
			public IntPtr          grefLoader;
			public IntPtr          Loader_loadClass;
			public IntPtr          grefClass;
			public IntPtr          Class_forName;
			public uint            logCategories;
			public int             version;
			public int             androidSdkVersion;
			public int             localRefsAreIndirect;
			public int             grefGcThreshold;
			public IntPtr          grefIGCUserPeer;
			public int             isRunningOnDesktop;
			public byte            brokenExceptionTransitions;
			public int             packageNamingPolicy;
			public byte            ioExceptionType;
			public int             jniAddNativeMethodRegistrationAttributePresent;
			public bool            jniRemappingInUse;
			public bool            marshalMethodsEnabled;
		}
#pragma warning restore 0649

		[DllImport (AndroidRuntime.InternalDllName, CallingConvention = CallingConvention.Cdecl)]
		extern static IntPtr _monodroid_get_identity_hash_code (IntPtr env, IntPtr value);

		internal static AndroidValueManager? AndroidValueManager;
		internal static bool AllocObjectSupported;
		internal static bool IsRunningOnDesktop;
		internal static bool jniRemappingInUse;
		internal static bool LogAssemblyCategory;
		internal static bool MarshalMethodsEnabled;
		internal static bool PropagateExceptions;
		internal static BoundExceptionType BoundExceptionType;
		internal static Func<IntPtr, IntPtr>? IdentityHash;
		internal static int gref_gc_threshold;
		internal static IntPtr grefIGCUserPeer_class;
		internal static IntPtr java_class_loader;
		internal static JniMethodInfo? mid_Class_forName;

		static int androidSdkVersion; // TODO: doesn't need to be a field
		static int version; // TODO: not needed?
		static IntPtr gref_class; // TODO: not needed?
		static IntPtr java_vm; // TODO: not needed?
		static IntPtr load_class_id; // TODO: not needed?

		static AndroidRuntime? androidRuntime;

#pragma warning disable CS0649 // Field is never assigned to.  This field is assigned from monodroid-glue.cc.
		internal static volatile bool BridgeProcessing; // = false
#pragma warning restore CS0649 // Field is never assigned to.

		internal static IntPtr Handle => JniEnvironment.EnvironmentPointer;

#if NETCOREAPP
		[UnmanagedCallersOnly]
#endif
		static unsafe void RegisterJniNatives (IntPtr typeName_ptr, int typeName_len, IntPtr jniClass, IntPtr methods_ptr, int methods_len)
		{
			string typeName = new string ((char*) typeName_ptr, 0, typeName_len);
			var type = Type.GetType (typeName);
			if (type == null) {
				JNIEnv.monodroid_log (LogLevel.Error,
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

#if NETCOREAPP
		[UnmanagedCallersOnly]
#endif
		internal static unsafe void Initialize (JnienvInitializeArgs* args)
		{
			IntPtr total_timing_sequence = IntPtr.Zero;
			IntPtr partial_timing_sequence = IntPtr.Zero;

			LogAssemblyCategory = (args->logCategories & (uint)LogCategories.Assembly) != 0;

			gref_gc_threshold = args->grefGcThreshold;

			jniRemappingInUse = args->jniRemappingInUse;
#if NETCOREAPP
			MarshalMethodsEnabled = args->marshalMethodsEnabled;
#endif
			java_vm = args->javaVm;

			version = args->version;

			androidSdkVersion = args->androidSdkVersion;

			java_class_loader = args->grefLoader;
			load_class_id     = args->Loader_loadClass;
			gref_class        = args->grefClass;
			mid_Class_forName = new JniMethodInfo (args->Class_forName, isStatic: true);

			if (args->localRefsAreIndirect == 1)
				IdentityHash = v => _monodroid_get_identity_hash_code (Handle, v);
			else
				IdentityHash = v => v;

#if MONOANDROID1_0
			Mono.SystemDependencyProvider.Initialize ();
#endif

			BoundExceptionType = (BoundExceptionType)args->ioExceptionType;
			androidRuntime = new AndroidRuntime (args->env, args->javaVm, androidSdkVersion > 10, args->grefLoader, args->Loader_loadClass, args->jniAddNativeMethodRegistrationAttributePresent != 0);
			AndroidValueManager = (AndroidValueManager) androidRuntime.ValueManager;

			AllocObjectSupported = androidSdkVersion > 10;
			IsRunningOnDesktop = args->isRunningOnDesktop == 1;

			grefIGCUserPeer_class = args->grefIGCUserPeer;

			PropagateExceptions = args->brokenExceptionTransitions == 0;

			JavaNativeTypeManager.PackageNamingPolicy = (PackageNamingPolicy)args->packageNamingPolicy;
			if (IsRunningOnDesktop) {
				var packageNamingPolicy = Environment.GetEnvironmentVariable ("__XA_PACKAGE_NAMING_POLICY__");
				if (Enum.TryParse (packageNamingPolicy, out PackageNamingPolicy pnp)) {
					JavaNativeTypeManager.PackageNamingPolicy = pnp;
				}
			}

#if !MONOANDROID1_0
			SetSynchronizationContext ();
#endif
		}

#if !MONOANDROID1_0
		// NOTE: prevents Android.App.Application static ctor from running
		[MethodImpl (MethodImplOptions.NoInlining)]
		static void SetSynchronizationContext () =>
			SynchronizationContext.SetSynchronizationContext (Android.App.Application.SynchronizationContext);
#endif
	}
}
