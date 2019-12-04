using System;
using System.Threading;

using Android.OS;
using Android.Runtime;

using LogLevel = Android.Runtime.LogLevel;

namespace Android.App {

	internal class SyncContext : SynchronizationContext {

		public override SynchronizationContext CreateCopy ()
		{
			return new SyncContext ();
		}

		static bool EnsureLooper (Looper looper, SendOrPostCallback d)
		{
			if (looper == null) {
				var message = $"No Android message loop is available. Skipping invocation of `{d.Method.DeclaringType.FullName}.{d.Method.Name}`!";
				if (JNIEnv.IsRunningOnDesktop)
					message += " Using `await` when running on the Android Designer is not currently supported. Please use the `View.IsInEditMode` property.";
				Logger.Log (LogLevel.Error, "monodroid-synccontext", message);
				return false;
			}

			return true;
		}

		public override void Post (SendOrPostCallback d, object state)
		{
			var looper = Application.Context?.MainLooper;
			if (!EnsureLooper (looper, d))
				return;
			using (var h = new Handler (looper)) {
				h.Post (() => d (state));
			}
		}

		public override void Send (SendOrPostCallback d, object state)
		{
			var looper = Looper.MainLooper;
			if (!EnsureLooper (looper, d))
				return;
			if (Looper.MyLooper() == looper) {
				d (state);
				return;
			}
			var m = new ManualResetEvent (false);
			using (var h = new Handler (looper)) {
				h.Post (() => {
					d (state);
					m.Set ();
				});
			}
			m.WaitOne ();
		}
	}
}
