using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Android.OS;
using Android.Runtime;
using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.RuntimeTests {

	[TestClass]
	public class HandlerTest {

		[TestMethod]
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

		[TestMethod]
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

		[TestMethod]
		public void ConcurrentPostsShareRunnable ()
		{
			using var t = new HandlerThread ("ConcurrentPostsShareRunnable");
			t.Start ();
			using var h = new CapturingHandler (t.Looper);
			using var start = new ManualResetEventSlim ();
			var threads = new Thread [8];
			int failedPosts = 0;
			Action action = () => {};
			long later = SystemClock.UptimeMillis () + 3_600_000;

			try {
				for (int i = 0; i < threads.Length; i++) {
					threads [i] = new Thread (() => {
						start.Wait ();
						for (int post = 0; post < 50; post++) {
							if (!h.PostAtTime (action, later))
								Interlocked.Increment (ref failedPosts);
						}
					});
					threads [i].Start ();
				}
				start.Set ();
				foreach (var thread in threads)
					Assert.IsTrue (thread.Join (TimeSpan.FromSeconds (10)));
				Assert.AreEqual (0, failedPosts);
				Assert.AreEqual (threads.Length * 50, h.Runnables.Count);
				var runnable = h.Runnables [0].Target;
				Assert.IsNotNull (runnable);
				foreach (var reference in h.Runnables)
					Assert.AreSame (runnable, reference.Target, "Concurrent posts for one Action must share a runnable.");
				h.RemoveCallbacks (action);
			} finally {
				start.Set ();
				foreach (var thread in threads)
					thread?.Join (TimeSpan.FromSeconds (10));
				Quit (t);
			}
		}

		[TestMethod]
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

		[TestMethod]
		public void ExecutedActionCallbacksAreCollectible ()
		{
			using var t = new HandlerThread ("ExecutedActionCallbacksAreCollectible");
			t.Start ();
			using var h = new Handler (t.Looper);
			try {
				var actions = PostAndRun (h, 100);
				AssertActionsCollectible (actions, "Executed callbacks must not keep their Action alive.");
			} finally {
				Quit (t);
			}
		}

		[TestMethod]
		public void CancelledActionCallbacksAreCollectible ()
		{
			using var t = new HandlerThread ("CancelledActionCallbacksAreCollectible");
			t.Start ();
			using var h = new Handler (t.Looper);
			try {
				var actions = PostAndCancel (h, 100);
				AssertActionsCollectible (actions, "Canceled callbacks must not keep their Action alive.");
			} finally {
				GC.KeepAlive (h);
				Quit (t);
			}
		}

		static void AssertActionsCollectible (List<WeakReference> actions, string message)
		{
			for (int i = 0; i < 100 && actions.Exists (a => a.IsAlive); i++) {
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
				Java.Lang.Runtime.GetRuntime ().Gc ();
				JniEnvironment.Runtime.ValueManager.CollectPeers ();
				Thread.Sleep (100);
			}
			Assert.IsFalse (actions.Exists (a => a.IsAlive), message);
		}

		[TestMethod]
		public void CancelledActionCallbacksAreCollectibleWhileActionsAreRooted ()
		{
			using var t = new HandlerThread ("CancelledActionCallbacksAreCollectibleWhileActionsAreRooted");
			t.Start ();
			using var h = new CapturingHandler (t.Looper);
			try {
				var actions = PostAndCancel (h, 100);
				Assert.AreEqual (200, h.Runnables.Count);
				h.Runnable = null;

				for (int round = 0; round < 50 && h.Runnables.Exists (r => r.IsAlive); round++) {
					GC.Collect ();
					GC.WaitForPendingFinalizers ();
					Java.Lang.Runtime.GetRuntime ().Gc ();
					JniEnvironment.Runtime.ValueManager.CollectPeers ();
					Thread.Sleep (100);
				}
				Assert.IsFalse (h.Runnables.Exists (r => r.IsAlive), "Canceled runnables must not be retained while their Actions remain rooted.");
				Assert.AreEqual (100, actions.Count);
				GC.KeepAlive (actions);
			} finally {
				Quit (t);
			}
		}

		static int sweepCalls;
		static readonly Action rootedAction = () => Interlocked.Increment (ref sweepCalls);

		[TestMethod]
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

				// Transient owners exercise the registry while canceled runnables should become
				// collectible even though the Action stays rooted.
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
			public List<WeakReference> Runnables { get; } = new ();

			public CapturingHandler (Looper looper) : base (looper) {}

			public override bool SendMessageAtTime (Message msg, long uptimeMillis)
			{
				lock (Runnables) {
					Runnable = msg.Callback;
					if (msg.Callback != null)
						Runnables.Add (new WeakReference (msg.Callback));
				}
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

		[MethodImpl (MethodImplOptions.NoInlining)]
		static List<WeakReference> PostAndCancel (Handler h, int count)
		{
			var actions = new List<WeakReference> ();
			for (int i = 0; i < count; i++) {
				var payload = new object ();
				Action action = () => GC.KeepAlive (payload);
				actions.Add (new WeakReference (action));
				Assert.IsTrue (h.PostDelayed (action, 3_600_000));
				Assert.IsTrue (h.PostDelayed (action, 3_600_000));
				h.RemoveCallbacks (action);
			}
			return actions;
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static List<Action> PostAndCancel (CapturingHandler h, int count)
		{
			var actions = new List<Action> (count);
			long later = SystemClock.UptimeMillis () + 3_600_000;
			for (int i = 0; i < count; i++) {
				var payload = new object ();
				Action action = () => GC.KeepAlive (payload);
				actions.Add (action);
				Assert.IsTrue (h.PostAtTime (action, later));
				Assert.IsTrue (h.PostAtTime (action, later));
				h.RemoveCallbacks (action);
			}
			return actions;
		}
	}
}
