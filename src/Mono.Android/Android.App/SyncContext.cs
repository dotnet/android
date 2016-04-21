using System;
using System.Threading;

using Android.OS;

namespace Android.App {

	internal class SyncContext : SynchronizationContext {

		public override SynchronizationContext CreateCopy ()
		{
			return new SyncContext ();
		}

		public override void Post (SendOrPostCallback d, object state)
		{
			using (Handler h = new Handler(Application.Context.MainLooper)) {
				h.Post (() => d (state));
			}
		}

		public override void Send (SendOrPostCallback d, object state)
		{
			var looper = Looper.MainLooper;
			if (Looper.MyLooper() == looper) {
				d (state);
				return;
			}
			var m = new ManualResetEvent (false);
			using (Handler h = new Handler (looper)) {
				h.Post (() => {
					d (state);
					m.Set ();
				});
			}
			m.WaitOne ();
		}
	}
}
