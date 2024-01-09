#if INSIDE_MONO_ANDROID_RUNTIME
using System;
using System.Reflection;

namespace Android.Runtime
{
	public static class AndroidRuntimeInternal
	{
		internal static readonly Action<Exception> mono_unhandled_exception = RuntimeNativeMethods.monodroid_debugger_unhandled_exception;

#pragma warning disable CS0649 // Field is never assigned to.  This field is assigned from monodroid-glue.cc.
		internal static volatile bool BridgeProcessing; // = false
#pragma warning restore CS0649 // Field is never assigned to.

		public static void WaitForBridgeProcessing ()
		{
			if (!BridgeProcessing)
				return;
			RuntimeNativeMethods._monodroid_gc_wait_for_bridge_processing ();
		}
	}
}
#endif // INSIDE_MONO_ANDROID_RUNTIME
