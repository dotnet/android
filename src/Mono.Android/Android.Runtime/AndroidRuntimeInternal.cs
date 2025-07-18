#if INSIDE_MONO_ANDROID_RUNTIME
using System;
using System.Reflection;
using Microsoft.Android.Runtime;

namespace Android.Runtime
{
	public static class AndroidRuntimeInternal
	{
		internal static readonly Action<Exception> mono_unhandled_exception;

#pragma warning disable CS0649 // Field is never assigned to.  This field is assigned from monodroid-glue.cc.
		internal static volatile bool BridgeProcessing; // = false
#pragma warning restore CS0649 // Field is never assigned to.

		static AndroidRuntimeInternal ()
		{
			if (RuntimeFeature.IsMonoRuntime) {
				mono_unhandled_exception = MonoUnhandledException;
			} else if (RuntimeFeature.IsCoreClrRuntime) {
				mono_unhandled_exception = CoreClrUnhandledException;
			} else {
				throw new NotSupportedException ("Internal error: unknown runtime not supported");
			}
		}

		static void CoreClrUnhandledException (Exception ex)
		{
			// TODO: Is this even needed on CoreCLR?
		}

		// Needed when running under CoreCLR, which doesn't allow icalls/ecalls.  Any method which contains any reference to
		// an unregistered icall/ecall method will fail to JIT (even if the method isn't actually called).  In this instance
		// it affected the static constructor which tried to assign `RuntimeNativeMethods.monodroid_debugger_unhandled_exception`
		// to `mono_unhandled_exception` at the top of the class.
		static void MonoUnhandledException (Exception ex)
		{
			RuntimeNativeMethods.monodroid_debugger_unhandled_exception (ex);
		}

		public static void WaitForBridgeProcessing ()
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
		}
	}
}
#endif // INSIDE_MONO_ANDROID_RUNTIME
