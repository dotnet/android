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
			var runnable    = new Java.Lang.Thread.RunnableImplementor (action, removable: true);
			if (Post (runnable)) {
				return true;
			}
			runnable.Dispose ();
			return false;
		}

		public bool PostAtFrontOfQueue (Action action)
		{
			var runnable    = new Java.Lang.Thread.RunnableImplementor (action, removable: true);
			if (PostAtFrontOfQueue (runnable)) {
				return true;
			}
			runnable.Dispose ();
			return false;
		}

		public bool PostAtTime (Action action, long uptimeMillis)
		{
			var runnable    = new Java.Lang.Thread.RunnableImplementor (action, removable: true);
			if (PostAtTime (runnable, uptimeMillis)) {
				return true;
			}
			runnable.Dispose ();
			return false;
		}

		public bool PostAtTime (Action action, Java.Lang.Object token, long uptimeMillis)
		{
			var runnable    = new Java.Lang.Thread.RunnableImplementor (action, removable: true);
			if (PostAtTime (runnable, token, uptimeMillis)) {
				return true;
			}
			runnable.Dispose ();
			return false;
		}

		public bool PostDelayed (Action action, long delayMillis)
		{
			var runnable    = new Java.Lang.Thread.RunnableImplementor (action, removable: true);
			if (PostDelayed (runnable, delayMillis)) {
				return true;
			}
			runnable.Dispose ();
			return false;
		}

		public void RemoveCallbacks (Action action)
		{
			var runnable = Java.Lang.Thread.RunnableImplementor.Remove (action);
			if (runnable == null)
				return;
			RemoveCallbacks (runnable);
			runnable.Dispose ();
		}

		public void RemoveCallbacks (Action action, Java.Lang.Object token)
		{
			var runnable = Java.Lang.Thread.RunnableImplementor.Remove (action);
			if (runnable == null)
				return;
			RemoveCallbacks (runnable, token);
			runnable.Dispose ();
		}
	}

	[Register ("mono/android/os/ActionHandlerCallback")]
	internal sealed class ActionHandlerCallback : Java.Lang.Object, Handler.ICallback {
		Action<Message> handler;

		public ActionHandlerCallback (Action<Message> handler)
			: base (
					JNIEnv.StartCreateInstance ("mono/android/os/ActionHandlerCallback", "()V"),
					JniHandleOwnership.TransferLocalRef)
		{
			JNIEnv.FinishCreateInstance (Handle, "()V");

			if (handler == null)
				throw new ArgumentNullException ("handler");
			this.handler = handler;
		}

		public bool HandleMessage (Message m)
		{
			handler (m);
			return true;
		}
	}
}

