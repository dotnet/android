#if INSIDE_MONO_ANDROID_RUNTIME
using System;
using System.Threading;

namespace Android.Runtime
{
	public static class AndroidRuntimeInternal
	{
		internal static readonly Action<Exception> mono_unhandled_exception = CoreClrUnhandledException;
		static int bridgeProcessingGeneration;

		internal static int BridgeProcessingGeneration => Volatile.Read (ref bridgeProcessingGeneration);

#pragma warning disable CS0649 // Field is never assigned to.  This field is assigned from monodroid-glue.cc.
		internal static volatile bool BridgeProcessing; // = false
#pragma warning restore CS0649 // Field is never assigned to.

		static void CoreClrUnhandledException (Exception ex)
		{
			// TODO: Is this even needed on CoreCLR?
		}

		internal static void NotifyBridgeProcessingFinished ()
		{
			Interlocked.Increment (ref bridgeProcessingGeneration);
		}

		public static void WaitForBridgeProcessing ()
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
		}
	}
}
#endif // INSIDE_MONO_ANDROID_RUNTIME
