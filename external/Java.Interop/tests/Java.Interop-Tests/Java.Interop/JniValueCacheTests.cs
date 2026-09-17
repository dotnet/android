#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	public class JniValueCacheTests {

		[Test]
		public void DisposalRemovesValuePublishedAfterSnapshot ()
		{
			var value = new DisposableValue ();
			using var key = new BlockingKey ();
			using var cache = new JniPeerMembers.JniValueCache<BlockingKey, DisposableValue> (
				1,
				1,
				static value => value.Dispose ());
			var publication = Task.Run (() => cache.GetOrAdd (key, _ => value));
			try {
				Assert.IsTrue (key.GetOrAddStarted.Wait (TimeSpan.FromSeconds (30)), "Cache publication did not start.");
				cache.Dispose ();
				key.ReleaseGetOrAdd.Set ();

				Assert.Throws<ObjectDisposedException> (() => publication.GetAwaiter ().GetResult ());
				Assert.AreEqual (1, value.DisposeCount);
				Assert.AreEqual (0, cache.Count);
			} finally {
				key.ReleaseGetOrAdd.Set ();
			}
		}

		sealed class BlockingKey : IDisposable {

			int hashCalls;

			public ManualResetEventSlim GetOrAddStarted {get;} = new ManualResetEventSlim ();
			public ManualResetEventSlim ReleaseGetOrAdd {get;} = new ManualResetEventSlim ();

			public override bool Equals (object? obj) => ReferenceEquals (this, obj);

			public override int GetHashCode ()
			{
				if (Interlocked.Increment (ref hashCalls) == 2) {
					GetOrAddStarted.Set ();
					if (!ReleaseGetOrAdd.Wait (TimeSpan.FromSeconds (30)))
						throw new TimeoutException ("Cache publication was not released.");
				}
				return 1;
			}

			public void Dispose ()
			{
				GetOrAddStarted.Dispose ();
				ReleaseGetOrAdd.Dispose ();
			}
		}

		sealed class DisposableValue {

			int disposeCount;

			public int DisposeCount => Volatile.Read (ref disposeCount);

			public void Dispose ()
			{
				Interlocked.Increment (ref disposeCount);
			}
		}
	}
}
