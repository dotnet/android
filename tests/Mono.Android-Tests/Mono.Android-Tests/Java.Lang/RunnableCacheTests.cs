using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Android.Runtime;
using Java.Interop;

using NUnit.Framework;

using RunnableImplementor = Java.Lang.Thread.RunnableImplementor;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	[Category ("CallbackLifetime")]
	public class RunnableCacheTests {
		[TestCase (false)]
		[TestCase (true)]
		public void HandledExceptionStillDisposesRunnable (bool removable)
		{
			var expected = new InvalidOperationException ("callback failure");
			Action action = () => throw expected;
			using var runnable = new RunnableImplementor (action, removable);

			Assert.AreSame (expected, Assert.Throws<InvalidOperationException> (() => runnable.Run ()));
			Assert.AreEqual (IntPtr.Zero, runnable.Handle, "Run must clean up even when its exception is handled.");
			GC.KeepAlive (action);
		}

		[Test]
		public void DisposedCallbacksAreNotRemovalCandidates ()
		{
			Action action = () => {};
			using var disposed = new RunnableImplementor (action, removable: true);
			disposed.Dispose ();
			using var pending = new RunnableImplementor (action, removable: true);
			int candidates = 0;
			bool foundPending = false;

			RunnableImplementor.Remove (action, reference => {
				candidates++;
				foundPending = JNIEnv.IsSameObject (pending.Handle, reference.Handle);
			});
			Assert.AreEqual (1, candidates);
			Assert.IsTrue (foundPending);
			Assert.AreNotEqual (IntPtr.Zero, pending.Handle);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void TerminalCleanupOnlyRemovesItsOwnInstance (bool throws)
		{
			Action action = () => {
				if (throws)
					throw new InvalidOperationException ();
			};
			using var first = new RunnableImplementor (action, removable: true);
			using var second = new RunnableImplementor (action, removable: true);
			if (throws)
				Assert.Throws<InvalidOperationException> (() => first.Run ());
			else
				first.Run ();

			int candidates = 0;
			bool foundSecond = false;
			RunnableImplementor.Remove (action, reference => {
				candidates++;
				foundSecond = JNIEnv.IsSameObject (second.Handle, reference.Handle);
			});
			Assert.AreEqual (1, candidates);
			Assert.IsTrue (foundSecond);
			Assert.AreEqual (IntPtr.Zero, first.Handle);
			Assert.AreNotEqual (IntPtr.Zero, second.Handle);
		}

		[Test]
		public void RemovalAggregatesCallbackResults ()
		{
			Action action = () => {};
			using var first = new RunnableImplementor (action, removable: true);
			using var second = new RunnableImplementor (action, removable: true);
			var callbacks = new List<bool> ();

			bool result = RunnableImplementor.Remove (
				action,
				callbacks,
				static (items, reference) => {
					items.Add (reference.IsValid);
					return items.Count == 2;
				});

			Assert.IsTrue (result);
			Assert.AreEqual (2, callbacks.Count);
			Assert.IsTrue (callbacks.TrueForAll (valid => valid));
		}

		[Test]
		public void RemovalUsesSnapshotWithoutHoldingCacheLock ()
		{
			Action action = () => {};
			using var first = new RunnableImplementor (action, removable: true);
			RunnableImplementor second = null;
			int removals = 0;
			try {
				RunnableImplementor.Remove (action, reference => {
					Assert.IsTrue (JNIEnv.IsSameObject (first.Handle, reference.Handle));
					removals++;
					var post = Task.Run (() => second = new RunnableImplementor (action, removable: true));
					Assert.IsTrue (post.Wait (TimeSpan.FromSeconds (10)), "Removal must not hold the cache lock.");
				});
				Assert.AreEqual (1, removals, "A reentrant post must not be added to an in-progress removal.");
				int candidates = 0;
				bool foundFirst = false;
				bool foundSecond = false;
				RunnableImplementor.Remove (action, reference => {
					candidates++;
					foundFirst |= JNIEnv.IsSameObject (first.Handle, reference.Handle);
					foundSecond |= JNIEnv.IsSameObject (second.Handle, reference.Handle);
				});
				Assert.AreEqual (2, candidates);
				Assert.IsTrue (foundFirst);
				Assert.IsTrue (foundSecond);
			} finally {
				second?.Dispose ();
			}
		}

		[Test]
		public void CompletionKeepsRemovalReferenceAlive ()
		{
			using var running = new ManualResetEventSlim ();
			Action action = () => running.Set ();
			using var runnable = new RunnableImplementor (action, removable: true);
			Task execution = null;
			JniObjectReference javaWeak = runnable.PeerReference.NewWeakGlobalRef ();
			try {
				RunnableImplementor.Remove (action, reference => {
					execution = Task.Run (() => runnable.Run ());
					Assert.IsTrue (running.Wait (TimeSpan.FromSeconds (10)));
					Assert.IsTrue (execution.Wait (TimeSpan.FromSeconds (10)));
					Assert.AreEqual (IntPtr.Zero, runnable.Handle);
					Assert.IsTrue (reference.IsValid);
					Assert.IsTrue (JNIEnv.IsSameObject (javaWeak.Handle, reference.Handle));
				});
			} finally {
				JniObjectReference.Dispose (ref javaWeak);
				if (execution != null)
					Assert.IsTrue (execution.Wait (TimeSpan.FromSeconds (10)));
			}
			Assert.AreEqual (IntPtr.Zero, runnable.Handle);
		}
	}
}
