using System;

using Android.Runtime;

namespace Java.Lang {

	[Register ("mono/java/lang/Runnable")]
	public sealed class Runnable : Java.Lang.Object, Java.Lang.IRunnable {

		Action handler;

		public Runnable (Action handler)
			: base (
					JNIEnv.StartCreateInstance ("mono/java/lang/Runnable", "()V"),
					JniHandleOwnership.TransferLocalRef)
		{
			JNIEnv.FinishCreateInstance (Handle, "()V");
			if (handler == null) {
				base.Dispose ();
				throw new ArgumentNullException ("handler");
			}
			this.handler = handler;
		}

		public void Run ()
		{
			handler ();
		}
	}
}

