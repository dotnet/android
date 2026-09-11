using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
			var candidates = new List<RunnableImplementor> ();

			RunnableImplementor.Remove (action, candidates.Add);
			Assert.AreEqual (1, candidates.Count);
			Assert.AreSame (pending, candidates [0]);
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

			var candidates = new List<RunnableImplementor> ();
			RunnableImplementor.Remove (action, candidates.Add);
			Assert.AreEqual (1, candidates.Count);
			Assert.AreSame (second, candidates [0]);
			Assert.AreEqual (IntPtr.Zero, first.Handle);
			Assert.AreNotEqual (IntPtr.Zero, second.Handle);
		}

		[Test]
		public void RemovalUsesSnapshotWithoutHoldingCacheLock ()
		{
			Action action = () => {};
			using var first = new RunnableImplementor (action, removable: true);
			RunnableImplementor second = null;
			int removals = 0;
			try {
				RunnableImplementor.Remove (action, runnable => {
					Assert.AreSame (first, runnable);
					removals++;
					var post = Task.Run (() => second = new RunnableImplementor (action, removable: true));
					Assert.IsTrue (post.Wait (TimeSpan.FromSeconds (10)), "Removal must not hold the cache lock.");
				});
				Assert.AreEqual (1, removals, "A reentrant post must not be added to an in-progress removal.");
				var candidates = new List<RunnableImplementor> ();
				RunnableImplementor.Remove (action, candidates.Add);
				Assert.AreEqual (2, candidates.Count);
				Assert.AreSame (first, candidates [0]);
				Assert.AreSame (second, candidates [1]);
			} finally {
				second?.Dispose ();
			}
		}

		[Test]
		public void CompletionDoesNotDisposeDuringRemoval ()
		{
			using var running = new ManualResetEventSlim ();
			Action action = () => running.Set ();
			using var runnable = new RunnableImplementor (action, removable: true);
			Task execution = null;
			try {
				RunnableImplementor.Remove (action, candidate => {
					execution = Task.Run (() => runnable.Run ());
					Assert.IsTrue (running.Wait (TimeSpan.FromSeconds (10)));
					Assert.IsFalse (execution.Wait (TimeSpan.FromMilliseconds (100)),
						"Completion must wait until the native removal finishes using the peer.");
					Assert.AreNotEqual (IntPtr.Zero, candidate.Handle);
				});
			} finally {
				if (execution != null)
					Assert.IsTrue (execution.Wait (TimeSpan.FromSeconds (10)));
			}
			Assert.AreEqual (IntPtr.Zero, runnable.Handle);
		}
	}
}
