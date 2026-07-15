#nullable enable

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Java.Net;

using NUnit.Framework;

using Xamarin.Android.Net;

namespace Xamarin.Android.NetTests
{
	/// <summary>
	/// Deterministic (no network) coverage of the "drain, then close" behavior in
	/// <c>AndroidMessageHandler.CancellationAwareResponseStream</c>. These construct the wrapper directly
	/// over a controllable blocking stream and a fake <c>HttpURLConnection</c> so the dispose-during-read
	/// ordering is exercised without depending on real socket/okhttp timing.
	/// </summary>
	[TestFixture]
	[Category ("AndroidMessageHandlerCancellation")]
	[Category ("CancellationAwareResponseStream")]
	public class CancellationAwareResponseStreamTests
	{
		const int PromptCompletionTimeoutMilliseconds = 3000;

		[Test]
		public Task DisposeDuringReadDefersInnerStreamClose () => AssertDisposeDefersClose (cancelReadFirst: false);

		[Test]
		public Task DisposeDuringCanceledReadDefersInnerStreamClose () => AssertDisposeDefersClose (cancelReadFirst: true);

		/// <summary>
		/// The core invariant: disposing while a read is parked must (a) not block, (b) abort the read via
		/// Disconnect, (c) NOT close the inner stream while the read is in flight, and (d) close it exactly
		/// once the read has unwound -- so the stream is neither corrupted nor leaked.
		/// </summary>
		static async Task AssertDisposeDefersClose (bool cancelReadFirst)
		{
			using var url = new URL ("http://localhost");
			var connection = new RecordingHttpURLConnection (url);
			var innerStream = new BlockingReadStream ();
			var responseStream = new AndroidMessageHandler.CancellationAwareResponseStream (innerStream, connection);
			using var readCts = new CancellationTokenSource ();
			var buffer = new byte [1];

			Task<int> readTask = responseStream.ReadAsync (buffer, 0, buffer.Length, cancelReadFirst ? readCts.Token : CancellationToken.None);
			await AssertCompletesPromptly (innerStream.ReadStartedTask, innerStream.ReleaseRead,
				"The response stream did not start reading.").ConfigureAwait (false);

			if (cancelReadFirst)
				readCts.Cancel ();

			// (a) Dispose must not block on the parked read.
			Task disposeTask = Task.Run (responseStream.Dispose);
			await AssertCompletesPromptly (disposeTask, innerStream.ReleaseRead,
				"Disposing the stream blocked while a read was in flight.").ConfigureAwait (false);

			// (b) Dispose must abort the parked read via Disconnect.
			await AssertCompletesPromptly (connection.DisconnectStartedTask, innerStream.ReleaseRead,
				"Disposing the stream did not disconnect to abort the in-flight read.").ConfigureAwait (false);

			// (c) The inner stream must NOT be closed while the read is still parked.
			Assert.IsFalse (innerStream.IsDisposed, "The inner stream was closed while a read was in flight.");

			// (d) Once the read unwinds, the reader (last one out) must close the inner stream exactly once.
			innerStream.ReleaseRead ();
			await AssertReadFinished (readTask).ConfigureAwait (false);
			await AssertCompletesPromptly (innerStream.DisposedTask, innerStream.ReleaseRead,
				"The inner stream was not closed after the read unwound (leak).").ConfigureAwait (false);

			Assert.IsFalse (innerStream.WasDisposedDuringRead, "The inner stream was closed while its read was active.");
			Assert.GreaterOrEqual (connection.DisconnectCallCount, 1, "The connection was never disconnected.");
		}

		[Test]
		public void DisposeWithNoActiveReadClosesImmediately ()
		{
			using var url = new URL ("http://localhost");
			var connection = new RecordingHttpURLConnection (url);
			var innerStream = new BlockingReadStream ();
			var responseStream = new AndroidMessageHandler.CancellationAwareResponseStream (innerStream, connection);

			responseStream.Dispose ();

			Assert.IsTrue (innerStream.IsDisposed, "The inner stream was not closed on dispose.");
			Assert.GreaterOrEqual (connection.DisconnectCallCount, 1, "The connection was not disconnected on dispose.");
		}

		[Test]
		public void ReadAfterDisposeThrowsObjectDisposedException ()
		{
			using var url = new URL ("http://localhost");
			var connection = new RecordingHttpURLConnection (url);
			var innerStream = new BlockingReadStream ();
			var responseStream = new AndroidMessageHandler.CancellationAwareResponseStream (innerStream, connection);
			responseStream.Dispose ();

			var buffer = new byte [1];
			// These calls are expected to throw before returning any data, so the "inexact read" analyzer
			// (CA2022) does not apply.
#pragma warning disable CA2022
			Assert.Throws<ObjectDisposedException> (() => responseStream.Read (buffer, 0, buffer.Length));
			Assert.ThrowsAsync<ObjectDisposedException> (async () => await responseStream.ReadAsync (buffer, 0, buffer.Length));
#pragma warning restore CA2022
		}

		static async Task AssertReadFinished (Task<int> readTask)
		{
			var completed = await Task.WhenAny (readTask, Task.Delay (PromptCompletionTimeoutMilliseconds)).ConfigureAwait (false);
			if (completed != readTask)
				Assert.Fail ("The read did not finish after being released.");

			try {
				await readTask.ConfigureAwait (false);
			} catch (Exception ex) when (ex is OperationCanceledException or System.IO.IOException or Java.IO.IOException or InvalidDataException or ObjectDisposedException or WebException) {
				// Any abort-driven outcome is acceptable; the read must simply have finished.
			}
		}

		static async Task AssertCompletesPromptly (Task task, Action unblock, string failureMessage)
		{
			var completed = await Task.WhenAny (task, Task.Delay (PromptCompletionTimeoutMilliseconds)).ConfigureAwait (false);
			if (completed != task) {
				unblock ();
				Assert.Fail (failureMessage);
			}

			await task.ConfigureAwait (false);
		}

		sealed class RecordingHttpURLConnection : HttpURLConnection
		{
			readonly TaskCompletionSource<bool> disconnectStarted = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			int disconnectCallCount;

			public RecordingHttpURLConnection (URL url) : base (url) { }

			public int DisconnectCallCount => Volatile.Read (ref disconnectCallCount);
			public Task DisconnectStartedTask => disconnectStarted.Task;

			public override void Connect () { }

			public override void Disconnect ()
			{
				Interlocked.Increment (ref disconnectCallCount);
				disconnectStarted.TrySetResult (true);
			}

			public override bool UsingProxy () => false;
		}

		sealed class BlockingReadStream : Stream
		{
			readonly TaskCompletionSource<bool> readStarted = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			readonly TaskCompletionSource<bool> releaseRead = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			readonly TaskCompletionSource<bool> disposedTcs = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			int activeReadCount;
			int disposed;
			int disposedDuringRead;

			public Task ReadStartedTask => readStarted.Task;
			public Task DisposedTask => disposedTcs.Task;
			public bool IsDisposed => Volatile.Read (ref disposed) != 0;
			public bool WasDisposedDuringRead => Volatile.Read (ref disposedDuringRead) != 0;

			public override bool CanRead => true;
			public override bool CanSeek => false;
			public override bool CanWrite => false;
			public override long Length => throw new NotSupportedException ();
			public override long Position { get => throw new NotSupportedException (); set => throw new NotSupportedException (); }

			public void ReleaseRead () => releaseRead.TrySetResult (true);

			public override async ValueTask<int> ReadAsync (Memory<byte> buffer, CancellationToken cancellationToken = default)
			{
				Interlocked.Increment (ref activeReadCount);
				readStarted.TrySetResult (true);
				try {
					// Simulates a parked native read: the Java stream does not observe managed cancellation,
					// so it only returns once the test releases it (mirroring Disconnect aborting the socket).
					await releaseRead.Task.ConfigureAwait (false);
					return 0;
				} finally {
					Interlocked.Decrement (ref activeReadCount);
				}
			}

			// Real body streams (BufferedStream, GZipStream, ...) implement the byte[] overload; mirror that
			// by delegating to the Memory overload so the wrapper's byte[] Read/ReadAsync exercise this fake.
			public override Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
				ReadAsync (buffer.AsMemory (offset, count), cancellationToken).AsTask ();

			protected override void Dispose (bool disposing)
			{
				if (disposing) {
					if (Volatile.Read (ref activeReadCount) != 0)
						Interlocked.Exchange (ref disposedDuringRead, 1);
					Interlocked.Exchange (ref disposed, 1);
					disposedTcs.TrySetResult (true);
				}
				base.Dispose (disposing);
			}

			public override void Flush () { }
			public override int Read (byte[] buffer, int offset, int count) => throw new NotSupportedException ();
			public override long Seek (long offset, SeekOrigin origin) => throw new NotSupportedException ();
			public override void SetLength (long value) => throw new NotSupportedException ();
			public override void Write (byte[] buffer, int offset, int count) => throw new NotSupportedException ();
		}
	}
}
