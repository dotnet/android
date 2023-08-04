using System;
using System.Diagnostics;
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
		internal struct JnienvInitializeArgs {
			public IntPtr          javaVm;
			public IntPtr          env;
			public IntPtr          grefLoader;
			public IntPtr          Loader_loadClass;
			public IntPtr          grefClass; // TODO: remove, not needed anymore
			public IntPtr          Class_forName;
			public uint            logCategories;
			public int             version; // TODO: remove, not needed anymore
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

		internal static AndroidValueManager? AndroidValueManager;
		internal static bool AllocObjectSupported;
		internal static bool IsRunningOnDesktop;
		internal static bool jniRemappingInUse;
		internal static bool LocalRefsAreIndirect;
		internal static bool LogAssemblyCategory;
		internal static bool MarshalMethodsEnabled;
		internal static bool PropagateExceptions;
		internal static BoundExceptionType BoundExceptionType;
		internal static int gref_gc_threshold;
		internal static IntPtr grefIGCUserPeer_class;
		internal static IntPtr java_class_loader;
		internal static JniMethodInfo? mid_Class_forName;

		static AndroidRuntime? androidRuntime;

#if NETCOREAPP
		[UnmanagedCallersOnly]
#endif
		static unsafe void RegisterJniNatives (IntPtr typeName_ptr, int typeName_len, IntPtr jniClass, IntPtr methods_ptr, int methods_len)
		{
			string typeName = new string ((char*) typeName_ptr, 0, typeName_len);
			var type = Type.GetType (typeName);
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

#if NETCOREAPP
		[UnmanagedCallersOnly]
#endif
		internal static unsafe void Initialize (JnienvInitializeArgs* args)
		{
			// var report = new System.Text.StringBuilder ();
			// var total = new Stopwatch ();
			// total.Start ();

			// var watch = new Stopwatch ();
			// watch.Start ();

			IntPtr total_timing_sequence = IntPtr.Zero;
			IntPtr partial_timing_sequence = IntPtr.Zero;

			LogAssemblyCategory = (args->logCategories & (uint)LogCategories.Assembly) != 0;

			gref_gc_threshold = args->grefGcThreshold;

			jniRemappingInUse = args->jniRemappingInUse;
#if NETCOREAPP
			MarshalMethodsEnabled = args->marshalMethodsEnabled;
#endif
			java_class_loader = args->grefLoader;

			mid_Class_forName = new JniMethodInfo (args->Class_forName, isStatic: true);

			LocalRefsAreIndirect = args->localRefsAreIndirect == 1;

			// watch.Stop ();
			// report.Append ("Initialize: initialize fields:: ");
			// report.Append (watch.ElapsedTicks);
			// report.AppendLine ();

			// watch.Restart ();
#if MONOANDROID1_0
			Mono.SystemDependencyProvider.Initialize ();
#endif
			// watch.Stop ();
			// report.Append ("Initialize: system deps init:: ");
			// report.Append (watch.ElapsedTicks);
			// report.AppendLine ();

			// watch.Restart ();
			bool androidNewerThan10 = args->androidSdkVersion > 10;
			BoundExceptionType = (BoundExceptionType)args->ioExceptionType;

			androidRuntime = new AndroidRuntime (args->env, args->javaVm, androidNewerThan10, args->grefLoader, args->Loader_loadClass, args->jniAddNativeMethodRegistrationAttributePresent != 0);
			// watch.Stop ();
			// report.Append ("Initialize: AndroidRuntime.ctor:: ");
			// report.Append (watch.ElapsedTicks);
			// report.AppendLine ();

			// watch.Restart ();
			AndroidValueManager = (AndroidValueManager) androidRuntime.ValueManager;

			AllocObjectSupported = androidNewerThan10;
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
			// watch.Stop ();
			// report.Append ("Initialize: the rest:: ");
			// report.Append (watch.ElapsedTicks);
			// report.AppendLine ();

			// total.Stop ();
			// report.Append ("Initialize: total time:: ");
			// report.Append (total.ElapsedTicks);
			// report.AppendLine ();

			// Console.WriteLine ($"Initialize: {report.ToString ()}");
		}

#if !MONOANDROID1_0
		// NOTE: prevents Android.App.Application static ctor from running
		[MethodImpl (MethodImplOptions.NoInlining)]
		static void SetSynchronizationContext () =>
			SynchronizationContext.SetSynchronizationContext (Android.App.Application.SynchronizationContext);
#endif
	}
}
