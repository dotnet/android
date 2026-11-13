using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Android.OS;
using Java.Interop;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	public class HandlerTest {

		[Test]
		public void RemoveDisposedInstance ()
		{
      using (var t = new HandlerThread ("RemoveDisposedInstance")) {
        t.Start ();
        using (var h = new Handler (t.Looper)) {
    			var e = new ManualResetEvent (false);
          Java.Lang.Runnable r = null;
          r = new Java.Lang.Runnable (() => {
            e.Set ();
            r.Dispose ();
          });
          h.Post (r.Run);
          e.WaitOne ();
        }
        
        t.QuitSafely ();
      }
		}

		[Test]
		public void RemoveCallbacksCancelsEveryPendingPost ()
		{
			using var t = new HandlerThread ("RemoveCallbacksCancelsEveryPendingPost");
			t.Start ();
			using var h = new Handler (t.Looper);
			using var other = new Handler (t.Looper);
			using var token = new Java.Lang.String ("token");
			using var otherToken = new Java.Lang.String ("other");
			using var blocked = new ManualResetEventSlim ();
			using var release = new ManualResetEventSlim ();
			using var done = new ManualResetEventSlim ();
			int calls = 0;
			Action action = () => Interlocked.Increment (ref calls);

			// Keep the looper busy so nothing runs before it is canceled.
			Assert.IsTrue (h.Post (() => { blocked.Set (); release.Wait (); }));
			Assert.IsTrue (blocked.Wait (TimeSpan.FromSeconds (10)));
			for (int i = 0; i < 50; i++) {
				Assert.IsTrue (h.Post (action));
				Assert.IsTrue (h.PostDelayed (action, 0));
				h.RemoveCallbacks (action);
			}
			Assert.IsTrue (other.Post (action));                       // runs: different handler
			Assert.IsTrue (h.Post (action));
			h.RemoveCallbacks (action);
			long now = SystemClock.UptimeMillis ();
			Assert.IsTrue (h.PostAtTime (action, token, now));
			Assert.IsTrue (h.PostAtTime (action, token, now));
			Assert.IsTrue (h.PostAtTime (action, otherToken, now));   // runs: different token
			h.RemoveCallbacks (action, token);
			Assert.IsTrue (h.Post (action));                           // runs: posted after cancellation
			Assert.IsTrue (h.Post (action));                           // runs: same runnable queued twice
			Assert.IsTrue (h.Post (done.Set));
			release.Set ();
			Assert.IsTrue (done.Wait (TimeSpan.FromSeconds (10)));
			Assert.AreEqual (4, calls);
			t.QuitSafely ();
		}

		[Test]
		public void ExecutedActionCallbacksAreCollectible ()
		{
			using var t = new HandlerThread ("ExecutedActionCallbacksAreCollectible");
			t.Start ();
			using var h = new Handler (t.Looper);
			var actions = PostAndRun (h, 100);
			for (int i = 0; i < 50 && actions.Exists (a => a.IsAlive); i++) {
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
				Java.Lang.Runtime.GetRuntime ().Gc ();
				JniEnvironment.Runtime.ValueManager.CollectPeers ();
				Thread.Sleep (100);
			}
			Assert.IsFalse (actions.Exists (a => a.IsAlive), "Executed callbacks must not keep their Action alive.");
			t.QuitSafely ();
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static List<WeakReference> PostAndRun (Handler h, int count)
		{
			var actions = new List<WeakReference> ();
			using var done = new CountdownEvent (count);
			for (int i = 0; i < count; i++) {
				var payload = new object ();
				Action action = () => { GC.KeepAlive (payload); done.Signal (); };
				actions.Add (new WeakReference (action));
				Assert.IsTrue (h.Post (action));
			}
			Assert.IsTrue (done.Wait (TimeSpan.FromSeconds (10)));
			return actions;
		}
	}
}
