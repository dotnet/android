using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Android.OS;
using Android.Runtime;
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

			try {
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
			} finally {
				release.Set ();
				Quit (t);
			}
		}

		[Test]
		public void RemoveCallbacksThroughReplacementWrapper ()
		{
			using var t = new HandlerThread ("RemoveCallbacksThroughReplacementWrapper");
			t.Start ();
			using var blocked = new ManualResetEventSlim ();
			using var release = new ManualResetEventSlim ();
			using var done = new ManualResetEventSlim ();
			int calls = 0;
			Action action = () => Interlocked.Increment (ref calls);
			var h = new Handler (t.Looper);
			var handle = h.PeerReference.NewGlobalRef ();

			try {
				Assert.IsTrue (h.Post (() => { blocked.Set (); release.Wait (); }));
				Assert.IsTrue (blocked.Wait (TimeSpan.FromSeconds (10)));
				Assert.IsTrue (h.Post (action));
				Assert.IsTrue (h.PostDelayed (action, 0));
				h.Dispose ();

				using var replacement = Java.Lang.Object.GetObject<Handler> (handle.Handle, JniHandleOwnership.DoNotTransfer);
				Assert.IsNotNull (replacement);
				Assert.AreNotSame (h, replacement);
				replacement.RemoveCallbacks (action);
				Assert.IsTrue (replacement.Post (done.Set));
				release.Set ();
				Assert.IsTrue (done.Wait (TimeSpan.FromSeconds (10)));
				Assert.AreEqual (0, calls, "Posts made through a disposed wrapper must be cancelable through a new wrapper.");
			} finally {
				release.Set ();
				h.Dispose ();
				JniObjectReference.Dispose (ref handle);
				Quit (t);
			}
		}

		[Test]
		public void ExecutedActionCallbacksAreCollectible ()
		{
			using var t = new HandlerThread ("ExecutedActionCallbacksAreCollectible");
			t.Start ();
			using var h = new Handler (t.Looper);
			try {
				var actions = PostAndRun (h, 100);
				for (int i = 0; i < 50 && actions.Exists (a => a.IsAlive); i++) {
					GC.Collect ();
					GC.WaitForPendingFinalizers ();
					Java.Lang.Runtime.GetRuntime ().Gc ();
					JniEnvironment.Runtime.ValueManager.CollectPeers ();
					Thread.Sleep (100);
				}
				Assert.IsFalse (actions.Exists (a => a.IsAlive), "Executed callbacks must not keep their Action alive.");
			} finally {
				Quit (t);
			}
		}

		static int sweepCalls;
		static readonly Action rootedAction = () => Interlocked.Increment (ref sweepCalls);

		[Test]
		public void CollectedOwnersReleaseRunnablesOfRootedActions ()
		{
			using var t = new HandlerThread ("CollectedOwnersReleaseRunnablesOfRootedActions");
			t.Start ();
			using var survivor = new Handler (t.Looper);
			using var blocked = new ManualResetEventSlim ();
			using var release = new ManualResetEventSlim ();
			using var done = new ManualResetEventSlim ();
			sweepCalls = 0;

			try {
				Assert.IsTrue (survivor.Post (() => { blocked.Set (); release.Wait (); }));
				Assert.IsTrue (blocked.Wait (TimeSpan.FromSeconds (10)));
				Assert.IsTrue (survivor.Post (rootedAction));
				Assert.IsTrue (survivor.PostDelayed (rootedAction, 0));

				// Each transient Handler caches a runnable for the rooted Action; once the Handler's Java
				// object is collected, sweeping its owner entry must release that runnable.
				var runnables = new List<WeakReference> ();
				for (int round = 0; round < 40 && (runnables.Count == 0 || runnables.Exists (r => r.IsAlive)); round++) {
					CreateTransientOwners (t.Looper, 100, round == 0 ? runnables : null);
					GC.Collect ();
					GC.WaitForPendingFinalizers ();
					Java.Lang.Runtime.GetRuntime ().Gc ();
					JniEnvironment.Runtime.ValueManager.CollectPeers ();
					Thread.Sleep (50);
				}
				Assert.AreEqual (100, runnables.Count);
				Assert.IsFalse (runnables.Exists (r => r.IsAlive), "Runnables of collected owners must be collectible while their Action is rooted.");

				survivor.RemoveCallbacks (rootedAction);
				Assert.IsTrue (survivor.Post (done.Set));
				release.Set ();
				Assert.IsTrue (done.Wait (TimeSpan.FromSeconds (10)));
				Assert.AreEqual (0, sweepCalls, "A surviving owner's pending posts must stay cancelable after a sweep.");
			} finally {
				release.Set ();
				Quit (t);
			}
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static void CreateTransientOwners (Looper looper, int count, List<WeakReference> runnables)
		{
			long later = SystemClock.UptimeMillis () + 3_600_000;
			for (int i = 0; i < count; i++) {
				using var h = new CapturingHandler (looper);
				Assert.IsTrue (h.PostAtTime (rootedAction, later));
				h.RemoveCallbacks (rootedAction);
				if (runnables != null) {
					Assert.IsNotNull (h.Runnable);
					runnables.Add (new WeakReference (h.Runnable));
				}
			}
		}

		sealed class CapturingHandler : Handler {
			public Java.Lang.IRunnable Runnable;

			public CapturingHandler (Looper looper) : base (looper) {}

			public override bool SendMessageAtTime (Message msg, long uptimeMillis)
			{
				Runnable = msg.Callback;
				return base.SendMessageAtTime (msg, uptimeMillis);
			}
		}

		static void Quit (HandlerThread t)
		{
			t.QuitSafely ();
			t.Join (10_000);
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
