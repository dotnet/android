using System;
using System.Reflection;

namespace Android.Runtime
{
	public static class AndroidRuntimeInternal
	{
		internal static MethodInfo? mono_unhandled_exception_method = null;
#if NETCOREAPP
		internal static Action<Exception> mono_unhandled_exception = RuntimeNativeMethods.monodroid_debugger_unhandled_exception;
#else
		internal static Action<Exception>? mono_unhandled_exception = null;
#endif

#pragma warning disable CS0649 // Field is never assigned to.  This field is assigned from monodroid-glue.cc.
		internal static volatile bool BridgeProcessing; // = false
#pragma warning restore CS0649 // Field is never assigned to.

		internal static void InitializeUnhandledExceptionMethod ()
		{
			if (mono_unhandled_exception == null) {
				mono_unhandled_exception_method = typeof (System.Diagnostics.Debugger)
					.GetMethod ("Mono_UnhandledException", BindingFlags.NonPublic | BindingFlags.Static);
				if (mono_unhandled_exception_method != null)
					mono_unhandled_exception = (Action<Exception>) Delegate.CreateDelegate (typeof(Action<Exception>), mono_unhandled_exception_method);
			}
			if (mono_unhandled_exception_method == null && mono_unhandled_exception != null) {
				mono_unhandled_exception_method = mono_unhandled_exception.Method;
			}
		}

		public static void WaitForBridgeProcessing ()
		{
			if (!BridgeProcessing)
				return;
			RuntimeNativeMethods._monodroid_gc_wait_for_bridge_processing ();
		}
	}
}
