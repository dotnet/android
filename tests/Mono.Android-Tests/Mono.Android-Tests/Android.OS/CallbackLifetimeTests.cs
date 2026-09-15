using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;

using Android.OS;
using Android.Runtime;
using Java.Interop;

using NUnit.Framework;

using RunnableImplementor = Java.Lang.Thread.RunnableImplementor;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	[Category ("CallbackLifetime")]
	public class CallbackLifetimeTests {
		[TestCase (false)]
		[TestCase (true)]
		public void NativeCancellationReleasesRunnableWithRootedAction (bool removeAll)
		{
			using var queue = new CallbackQueue ();
			using var token = new Java.Lang.String ("token");
			int calls = 0;
			Action action = () => Interlocked.Increment (ref calls);
			WeakReference<RunnableImplementor> weak = null;
			JniObjectReference javaWeak = default;

			try {
				OnFreshThread (() => {
					var runnable = new RunnableImplementor (action, removable: true);
					weak = new WeakReference<RunnableImplementor> (runnable, trackResurrection: true);
					javaWeak = runnable.PeerReference.NewWeakGlobalRef ();
					Assert.IsTrue (queue.Handler.PostAtTime (runnable, token, SystemClock.UptimeMillis ()));
				});

				CollectPeers ();
				Assert.IsTrue (IsAlive (weak), "The Java queue must retain the original managed callback.");
				Assert.IsFalse (JNIEnv.IsSameObject (javaWeak.Handle, IntPtr.Zero));

				// This is the generated native binding, not the Action-specific removal helper.
				queue.Handler.RemoveCallbacksAndMessages (removeAll ? null : token);
				WaitForCollection (() => !IsAlive (weak) && JNIEnv.IsSameObject (javaWeak.Handle, IntPtr.Zero));
				queue.Drain ();
				Assert.AreEqual (0, calls);
			} finally {
				JniObjectReference.Dispose (ref javaWeak);
				GC.KeepAlive (action);
			}
		}

		[Test]
		public void JavaQueueKeepsCallbackCallableAcrossCollection ()
		{
			using var queue = new CallbackQueue ();
			int calls = 0;
			WeakReference<Action> weakAction = null;
			OnFreshThread (() => {
				Action action = () => Interlocked.Increment (ref calls);
				weakAction = new WeakReference<Action> (action);
				Assert.IsTrue (queue.Handler.Post (action));
			});

			CollectPeers ();
			Assert.IsTrue (IsAlive (weakAction), "The queue, not a managed Action root, owns the callback.");
			queue.Drain ();
			Assert.AreEqual (1, calls);
		}

		[Test]
		public void CacheDoesNotRootActionOrUnqueuedRunnable ()
		{
			WeakReference<Action> weakAction = null;
			WeakReference<RunnableImplementor> weakRunnable = null;
			OnFreshThread (() => {
				var target = new object ();
				Action action = () => GC.KeepAlive (target);
				var runnable = new RunnableImplementor (action, removable: true);
				weakAction = new WeakReference<Action> (action);
				weakRunnable = new WeakReference<RunnableImplementor> (runnable, trackResurrection: true);
			});

			WaitForCollection (() => !IsAlive (weakAction) && !IsAlive (weakRunnable));
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RemoveCallbacksRemovesEveryPost (bool useToken)
		{
			using var queue = new CallbackQueue ();
			using var token = new Java.Lang.String ("token");
			int calls = 0;
			int otherCalls = 0;
			Action action = () => calls++;
			for (int i = 0; i < 3; i++)
				Assert.IsTrue (queue.Handler.PostAtTime (action, token, SystemClock.UptimeMillis ()));
			Assert.IsTrue (queue.Handler.Post (() => otherCalls++));

			if (useToken)
				queue.Handler.RemoveCallbacks (action, token);
			else
				queue.Handler.RemoveCallbacks (action);
			queue.Drain ();

			Assert.AreEqual (0, calls);
			Assert.AreEqual (1, otherCalls);
		}

		[Test]
		public void WrongTokenDoesNotDisposeOrForgetQueuedCallback ()
		{
			using var queue = new CallbackQueue ();
			using var token = new Java.Lang.String ("token");
			using var otherToken = new Java.Lang.String ("other token");
			int calls = 0;
			Action action = () => calls++;
			using var runnable = new RunnableImplementor (action, removable: true);
			Assert.IsTrue (queue.Handler.PostAtTime (runnable, token, SystemClock.UptimeMillis ()));

			queue.Handler.RemoveCallbacks (action, otherToken);
			Assert.AreNotEqual (IntPtr.Zero, runnable.Handle, "A token mismatch must not dispose queued work.");
			queue.Handler.RemoveCallbacks (action, token);
			queue.Drain ();
			Assert.AreEqual (0, calls, "A token mismatch must leave the callback removable.");
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RemovingOneTokenPreservesOtherTokenCallbacks (bool removeOther)
		{
			using var queue = new CallbackQueue ();
			using var token = new Java.Lang.String ("token");
			using var otherToken = new Java.Lang.String ("other token");
			int calls = 0;
			Action action = () => calls++;
			Assert.IsTrue (queue.Handler.PostAtTime (action, token, SystemClock.UptimeMillis ()));
			Assert.IsTrue (queue.Handler.PostAtTime (action, otherToken, SystemClock.UptimeMillis ()));

			queue.Handler.RemoveCallbacks (action, token);
			if (removeOther)
				queue.Handler.RemoveCallbacks (action, otherToken);
			queue.Drain ();
			Assert.AreEqual (removeOther ? 0 : 1, calls);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RemovingFromOneHandlerPreservesOtherHandlerCallbacks (bool removeOther)
		{
			using var queue = new CallbackQueue ();
			using var other = new Handler (queue.Handler.Looper);
			int calls = 0;
			Action action = () => calls++;
			Assert.IsTrue (queue.Handler.Post (action));
			Assert.IsTrue (other.Post (action));

			queue.Handler.RemoveCallbacks (action);
			if (removeOther)
				other.RemoveCallbacks (action);
			queue.Drain ();
			Assert.AreEqual (removeOther ? 0 : 1, calls);
		}

		[Test]
		public void OlderCompletionDoesNotForgetNewerPost ()
		{
			using var queue = new CallbackQueue ();
			int calls = 0;
			Action action = () => calls++;
			using var remove = new Java.Lang.Runnable (() => queue.Handler.RemoveCallbacks (action));
			Assert.IsTrue (queue.Handler.Post (action));
			Assert.IsTrue (queue.Handler.Post (remove));
			Assert.IsTrue (queue.Handler.Post (action));

			queue.Drain ();
			Assert.AreEqual (1, calls);
		}

		[Test]
		public void SelfRepostingCallbackRemainsRemovable ()
		{
			using var queue = new CallbackQueue ();
			int calls = 0;
			Action action = null;
			using var remove = new Java.Lang.Runnable (() => queue.Handler.RemoveCallbacks (action));
			action = () => {
				if (++calls == 1) {
					queue.Handler.Post (remove);
					queue.Handler.Post (action);
				}
			};
			Assert.IsTrue (queue.Handler.Post (action));

			queue.Drain ();
			// The first callback queues its removal and repost after the first drain marker.
			queue.Drain ();
			Assert.AreEqual (1, calls);
		}

		static void CollectPeers ()
		{
			int generation = JNIEnv.BridgeProcessingGeneration;
			var timeout = Stopwatch.StartNew ();
			do {
				GC.Collect ();
				GC.WaitForPendingFinalizers ();
				JNIEnv.WaitForBridgeProcessing ();
				Java.Lang.JavaSystem.Gc ();
				JniEnvironment.Runtime.ValueManager.CollectPeers ();
				JNIEnv.WaitForBridgeProcessing ();
				if (Microsoft.Android.Runtime.RuntimeFeature.IsMonoRuntime ||
						JNIEnv.BridgeProcessingGeneration != generation)
					return;
				Thread.Sleep (10);
			} while (timeout.ElapsedMilliseconds < 5000);
			Assert.Fail ("No JNI bridge-processing cycle completed.");
		}

		static void WaitForCollection (Func<bool> collected)
		{
			var timeout = Stopwatch.StartNew ();
			do {
				CollectPeers ();
				if (collected ())
					return;
				Thread.Sleep (10);
			} while (timeout.ElapsedMilliseconds < 10000);
			Assert.Fail ("The callback ownership chain was not released.");
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static bool IsAlive<T> (WeakReference<T> weak) where T : class
		{
			bool alive = false;
			OnFreshThread (() => alive = weak.TryGetTarget (out _));
			return alive;
		}

		static void OnFreshThread (Action action)
		{
			// Do not leave callback references on a conservatively scanned Mono test stack.
			Exception error = null;
			var thread = new Thread (() => {
				try {
					action ();
				} catch (Exception e) {
					error = e;
				}
			});
			thread.Start ();
			Assert.IsTrue (thread.Join (TimeSpan.FromSeconds (10)), "The callback operation did not finish.");
			if (error != null)
				ExceptionDispatchInfo.Capture (error).Throw ();
		}

		sealed class CallbackQueue : IDisposable {
			readonly HandlerThread thread = new HandlerThread ("CallbackLifetimeTests");
			readonly ManualResetEventSlim release = new ManualResetEventSlim ();
			readonly Java.Lang.Runnable blocker;

			public Handler Handler { get; }

			public CallbackQueue ()
			{
				thread.Start ();
				Handler = new Handler (thread.Looper);
				blocker = new Java.Lang.Runnable (() => release.Wait ());
				Assert.IsTrue (Handler.Post (blocker));
			}

			public void Drain ()
			{
				using var done = new ManualResetEventSlim ();
				using var marker = new Java.Lang.Runnable (() => done.Set ());
				Assert.IsTrue (Handler.Post (marker));
				release.Set ();
				Assert.IsTrue (done.Wait (TimeSpan.FromSeconds (10)), "The callback queue did not drain.");
			}

			public void Dispose ()
			{
				Handler.RemoveCallbacksAndMessages (null);
				release.Set ();
				thread.Quit ();
				thread.Join ();
				blocker.Dispose ();
				Handler.Dispose ();
				thread.Dispose ();
				release.Dispose ();
			}
		}
	}
}
