using System;
using Android.Runtime;

namespace Android.OS {

	public partial class Handler {

		[global::System.Runtime.Versioning.ObsoletedOSPlatform ("android30.0")]
		public Handler (Action<Message> handler)
			: this (new ActionHandlerCallback (handler))
		{
		}

		public bool Post (Action action)
		{
			return Post (Java.Lang.Thread.RunnableImplementor.GetOrCreate (action));
		}

		public bool PostAtFrontOfQueue (Action action)
		{
			return PostAtFrontOfQueue (Java.Lang.Thread.RunnableImplementor.GetOrCreate (action));
		}

		public bool PostAtTime (Action action, long uptimeMillis)
		{
			return PostAtTime (Java.Lang.Thread.RunnableImplementor.GetOrCreate (action), uptimeMillis);
		}

		public bool PostAtTime (Action action, Java.Lang.Object token, long uptimeMillis)
		{
			return PostAtTime (Java.Lang.Thread.RunnableImplementor.GetOrCreate (action), token, uptimeMillis);
		}

		public bool PostDelayed (Action action, long delayMillis)
		{
			return PostDelayed (Java.Lang.Thread.RunnableImplementor.GetOrCreate (action), delayMillis);
		}

		public void RemoveCallbacks (Action action)
		{
			var runnable = Java.Lang.Thread.RunnableImplementor.Find (action);
			if (runnable == null)
				return;
			RemoveCallbacks (runnable);
		}

		public void RemoveCallbacks (Action action, Java.Lang.Object token)
		{
			var runnable = Java.Lang.Thread.RunnableImplementor.Find (action);
			if (runnable == null)
				return;
			RemoveCallbacks (runnable, token);
		}
	}

	[Register ("mono/android/os/ActionHandlerCallback")]
	internal sealed class ActionHandlerCallback : Java.Lang.Object, Handler.ICallback {
		Action<Message> handler;

		public ActionHandlerCallback (Action<Message> handler)
			: base (
					// SAFETY: The complete ASCII literal supplies stable, NUL-terminated JNI class-name bytes.
					unsafe (JNIEnv.AllocObject ("mono/android/os/ActionHandlerCallback"u8)),
					JniHandleOwnership.TransferLocalRef)
		{
			unsafe {
				// SAFETY: this peer owns the allocated handle; ()V supplies the zero-argument instance-constructor ABI.
				JNIEnv.FinishCreateInstance (Handle, "()V"u8);
			}

			ArgumentNullException.ThrowIfNull (handler);
			this.handler = handler;
		}

		public bool HandleMessage (Message m)
		{
			handler (m);
			return true;
		}
	}
}
