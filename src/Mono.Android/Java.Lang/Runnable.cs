using System;

using Android.Runtime;

namespace Java.Lang {

	[Register ("mono/java/lang/Runnable")]
	public sealed class Runnable : Java.Lang.Object, Java.Lang.IRunnable {

		Action handler;

		public Runnable (Action handler)
			: base (
					// SAFETY: The complete ASCII literal supplies stable, NUL-terminated JNI class-name bytes.
					unsafe (JNIEnv.AllocObject ("mono/java/lang/Runnable"u8)),
					JniHandleOwnership.TransferLocalRef)
		{
			unsafe {
				// SAFETY: this peer owns the allocated handle; ()V supplies the zero-argument instance-constructor ABI.
				JNIEnv.FinishCreateInstance (Handle, "()V"u8);
			}
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
