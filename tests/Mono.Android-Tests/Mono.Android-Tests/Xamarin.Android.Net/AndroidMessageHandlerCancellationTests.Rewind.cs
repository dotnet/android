#nullable enable

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xamarin.Android.Net;

using NUnit.Framework;

namespace Xamarin.Android.NetTests
{
	public partial class AndroidMessageHandlerCancellationTests
	{
		[Test]
		public async Task HttpContentStreamIsRewoundAfterCancellation ()
		{
			const int requestContentLength = 1_000_000;
			const int requestTimeoutMilliseconds = 10_000;

			int testPort = GetAvailablePort ();
			using var listener = new HttpListener ();
			listener.Prefixes.Add ($"http://+:{testPort}/");
			listener.Start ();

			var cancellationObserved = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			var firstServerBodyRead = new TaskCompletionSource<int> (TaskCreationOptions.RunContinuationsAsynchronously);
			var firstServerTask = HandleCancelledRequest ();

			using var cancellationTokenSource = new CancellationTokenSource ();
			using var retryCancellationTokenSource = new CancellationTokenSource ();
			using var client = new HttpClient (new AndroidMessageHandler ());
			var requestBody = new byte [requestContentLength];
			for (int i = 0; i < requestBody.Length; i++)
				requestBody [i] = (byte) (i % 251);
			var contentStream = new ControlledSeekableStream (requestBody);
			using var content = new StreamContent (contentStream);
			using var request = new HttpRequestMessage (HttpMethod.Post, $"http://localhost:{testPort}/") { Content = content };
			Task firstRequestTask = Task.CompletedTask;
			Task retryServerTask = Task.CompletedTask;
			Task retryRequestTask = Task.CompletedTask;

			try {
				var stream = await content.ReadAsStreamAsync ();
				Assert.AreEqual (0, stream.Position, "Stream position should be 0 before first request");

				var firstResponseTask = client.SendAsync (request, cancellationTokenSource.Token);
				firstRequestTask = firstResponseTask;
				await WaitForTask (contentStream.FirstWriteCompletedTask, requestTimeoutMilliseconds, "The first request body did not start uploading.").ConfigureAwait (false);
				await WaitForTask (firstServerBodyRead.Task, requestTimeoutMilliseconds, "The first server handler did not receive the request body prefix.").ConfigureAwait (false);
				Assert.IsTrue (contentStream.IsFirstCopyBlocked, "The first content copy was not blocked after its initial destination write.");
				Assert.AreEqual (1, contentStream.CopyCount, "The first request should start exactly one content copy.");

				cancellationTokenSource.Cancel ();
				try {
					await WaitForTask (firstResponseTask, requestTimeoutMilliseconds, "The first request did not observe cancellation.").ConfigureAwait (false);
					using var firstResponse = await firstResponseTask.ConfigureAwait (false);
					Assert.Fail ("The first request completed successfully instead of observing cancellation.");
				} catch (OperationCanceledException) {
				}

				cancellationObserved.TrySetResult (true);
				await WaitForTask (firstServerTask, requestTimeoutMilliseconds, "The first server handler did not finish after cancellation.").ConfigureAwait (false);

				var streamAfterCancellation = await content.ReadAsStreamAsync ();
				Assert.AreEqual (0, streamAfterCancellation.Position, "Stream position should be 0 after cancellation (stream should be rewound)");
				Assert.AreEqual (1, contentStream.CopyCount, "Cancellation should finish the first content copy before retry.");

				retryServerTask = HandleRetryRequest ();
				using var retryRequest = new HttpRequestMessage (HttpMethod.Post, $"http://localhost:{testPort}/") { Content = content };
				var retryResponseTask = client.SendAsync (retryRequest, retryCancellationTokenSource.Token);
				retryRequestTask = retryResponseTask;
				var retryTasks = Task.WhenAll (retryResponseTask, retryServerTask);
				await WaitForTask (retryTasks, requestTimeoutMilliseconds, "The retry request and server handler did not finish.").ConfigureAwait (false);

				using var retryResponse = await retryResponseTask.ConfigureAwait (false);
				Assert.True (retryResponse.IsSuccessStatusCode, "Second request should succeed with reused content");
				Assert.AreEqual (2, contentStream.CopyCount, "The retry should perform a second, ungated content copy.");

				var streamAfterRetry = await content.ReadAsStreamAsync ();
				Assert.AreEqual (0, streamAfterRetry.Position, "Stream position should be 0 after successful request");
			} finally {
				bool firstWriteCompletedBeforeCleanup = contentStream.FirstWriteCompletedTask.IsCompleted;
				bool firstServerBodyReadBeforeCleanup = firstServerBodyRead.Task.IsCompleted;
				bool firstRequestCompletedBeforeCleanup = firstRequestTask.IsCompleted;
				bool firstServerCompletedBeforeCleanup = firstServerTask.IsCompleted;
				bool retryRequestCompletedBeforeCleanup = retryRequestTask.IsCompleted;
				bool retryServerCompletedBeforeCleanup = retryServerTask.IsCompleted;
				bool firstRequestCancellationExpected = cancellationTokenSource.IsCancellationRequested || !firstRequestCompletedBeforeCleanup;

				cancellationObserved.TrySetResult (true);
				cancellationTokenSource.Cancel ();
				retryCancellationTokenSource.Cancel ();
				contentStream.ReleaseFirstCopy ();
				listener.Abort ();

				await Task.WhenAll (
					ObserveTaskAfterCleanup (contentStream.FirstWriteCompletedTask, "first destination write signal", firstWriteCompletedBeforeCleanup, cancellationExpected: !firstWriteCompletedBeforeCleanup, listenerAbortExpected: false),
					ObserveTaskAfterCleanup (firstServerBodyRead.Task, "first server body read signal", firstServerBodyReadBeforeCleanup, cancellationExpected: false, listenerAbortExpected: true),
					ObserveTaskAfterCleanup (firstRequestTask, "first request", firstRequestCompletedBeforeCleanup, cancellationExpected: firstRequestCancellationExpected, listenerAbortExpected: false),
					ObserveTaskAfterCleanup (firstServerTask, "first server handler", firstServerCompletedBeforeCleanup, cancellationExpected: false, listenerAbortExpected: true),
					ObserveTaskAfterCleanup (retryRequestTask, "retry request", retryRequestCompletedBeforeCleanup, cancellationExpected: !retryRequestCompletedBeforeCleanup, listenerAbortExpected: false),
					ObserveTaskAfterCleanup (retryServerTask, "retry server handler", retryServerCompletedBeforeCleanup, cancellationExpected: false, listenerAbortExpected: true)
				).ConfigureAwait (false);
			}

			async Task HandleCancelledRequest ()
			{
				try {
					var context = await listener.GetContextAsync ().ConfigureAwait (false);
					using var response = context.Response;
					Assert.AreEqual (requestBody.Length, context.Request.ContentLength64, "The first request declared an unexpected content length.");

					var buffer = new byte [4096];
					int bytesRead = await context.Request.InputStream.ReadAsync (buffer, 0, buffer.Length).ConfigureAwait (false);
					Assert.Greater (bytesRead, 0, "The first request ended before the server received its body prefix.");
					Assert.Less (bytesRead, context.Request.ContentLength64, "The first server read unexpectedly consumed the complete request body.");
					for (int i = 0; i < bytesRead; i++) {
						if (buffer [i] != requestBody [i])
							Assert.Fail ($"The first request body differed at offset {i}.");
					}

					firstServerBodyRead.TrySetResult (bytesRead);
					await cancellationObserved.Task.ConfigureAwait (false);
					response.Abort ();
				} catch (Exception ex) {
					firstServerBodyRead.TrySetException (ex);
					throw;
				}
			}

			async Task HandleRetryRequest ()
			{
				var context = await listener.GetContextAsync ().ConfigureAwait (false);
				using var response = context.Response;
				Assert.AreEqual (requestBody.Length, context.Request.ContentLength64, "The retry request declared an unexpected content length.");
				var buffer = new byte [4096];
				int totalBytesRead = 0;
				while (totalBytesRead < requestBody.Length) {
					int bytesToRead = Math.Min (buffer.Length, requestBody.Length - totalBytesRead);
					int bytesRead = await context.Request.InputStream.ReadAsync (buffer, 0, bytesToRead).ConfigureAwait (false);
					Assert.Greater (bytesRead, 0, "The retry request ended before the complete body was received.");
					for (int i = 0; i < bytesRead; i++) {
						if (buffer [i] != requestBody [totalBytesRead + i])
							Assert.Fail ($"The retry request body differed at offset {totalBytesRead + i}.");
					}
					totalBytesRead += bytesRead;
				}

				Assert.AreEqual (requestBody.Length, totalBytesRead, "The retry request did not contain the complete rewound body.");

				response.StatusCode = 200;
				response.ContentLength64 = 0;
				response.Close ();
			}

			async Task ObserveTaskAfterCleanup (Task task, string taskName, bool completedBeforeCleanup, bool cancellationExpected, bool listenerAbortExpected)
			{
				try {
					await WaitForTask (task, requestTimeoutMilliseconds, $"The {taskName} did not finish during cleanup.").ConfigureAwait (false);
				} catch (OperationCanceledException) when (cancellationExpected) {
				} catch (HttpListenerException) when (listenerAbortExpected && !completedBeforeCleanup) {
				} catch (ObjectDisposedException) when (listenerAbortExpected && !completedBeforeCleanup) {
				} catch (IOException) when (listenerAbortExpected && !completedBeforeCleanup) {
				}
			}
		}

		[Test]
		public async Task ControlledSeekableStreamGatesOnlyFirstCopy ()
		{
			const int copyTimeoutMilliseconds = 10_000;
			var content = new byte [32];
			for (int i = 0; i < content.Length; i++)
				content [i] = (byte) i;

			using var stream = new ControlledSeekableStream (content);
			using var firstDestination = new BufferedDestinationStream ();
			using var cancellationTokenSource = new CancellationTokenSource ();
			Task firstCopyTask = Task.CompletedTask;

			try {
				firstCopyTask = stream.CopyToAsync (firstDestination, 8, cancellationTokenSource.Token);
				await WaitForTask (stream.FirstWriteCompletedTask, copyTimeoutMilliseconds, "The controlled stream did not complete its first destination write.").ConfigureAwait (false);

				Assert.IsTrue (stream.IsFirstCopyBlocked, "The first copy should remain blocked after its initial destination write.");
				Assert.IsFalse (firstCopyTask.IsCompleted, "The first copy completed before cancellation.");
				Assert.AreEqual (8, stream.FirstWriteLength, "The first destination write should use the requested copy buffer size.");
				Assert.AreEqual (8, stream.Position, "The controlled stream should advance only by the bytes written before its gate.");
				Assert.AreEqual (1, firstDestination.FlushCount, "The first destination write should be flushed before the progress gate is signaled.");
				CollectionAssert.AreEqual (new byte [] { 0, 1, 2, 3, 4, 5, 6, 7 }, firstDestination.ToArray (),
					"The flushed destination should expose the complete first write.");

				cancellationTokenSource.Cancel ();
				try {
					await firstCopyTask.ConfigureAwait (false);
					Assert.Fail ("The first controlled copy completed instead of observing cancellation.");
				} catch (OperationCanceledException) {
				}

				stream.Seek (0, SeekOrigin.Begin);
				using var retryDestination = new MemoryStream ();
				await stream.CopyToAsync (retryDestination, 8, CancellationToken.None).ConfigureAwait (false);

				Assert.AreEqual (2, stream.CopyCount, "The retry should perform a second content copy.");
				CollectionAssert.AreEqual (content, retryDestination.ToArray (), "The ungated retry should copy the complete stream.");
			} finally {
				cancellationTokenSource.Cancel ();
				stream.ReleaseFirstCopy ();

				try {
					await WaitForTask (firstCopyTask, copyTimeoutMilliseconds, "The first controlled copy did not finish during cleanup.").ConfigureAwait (false);
				} catch (OperationCanceledException) {
				}
			}
		}

		[Test]
		public async Task HttpListenerAbortCompletesPendingRequestBodyRead ()
		{
			const int contentLength = 1024;
			const int requestTimeoutMilliseconds = 10_000;
			byte [] bodyPrefix = { 0, 1, 2, 3, 4, 5, 6, 7 };

			int testPort = GetAvailablePort ();
			using var listener = new HttpListener ();
			listener.Prefixes.Add ($"http://127.0.0.1:{testPort}/");
			listener.Start ();
			var contextTask = listener.GetContextAsync ();

			using var client = new TcpClient ();
			await client.ConnectAsync (IPAddress.Loopback, testPort).ConfigureAwait (false);
			using NetworkStream clientStream = client.GetStream ();
			byte [] requestHeaders = Encoding.ASCII.GetBytes (
				$"POST / HTTP/1.1\r\nHost: 127.0.0.1:{testPort}\r\nContent-Length: {contentLength}\r\nConnection: keep-alive\r\n\r\n"
			);
			await clientStream.WriteAsync (requestHeaders, 0, requestHeaders.Length).ConfigureAwait (false);
			await clientStream.WriteAsync (bodyPrefix, 0, bodyPrefix.Length).ConfigureAwait (false);
			await clientStream.FlushAsync ().ConfigureAwait (false);

			await WaitForTask (contextTask, requestTimeoutMilliseconds, "The listener did not accept the partial fixed-length request.").ConfigureAwait (false);
			var context = await contextTask.ConfigureAwait (false);
			using var response = context.Response;
			Task<int> pendingReadTask = Task.FromResult (0);
			try {
				Assert.AreEqual (contentLength, context.Request.ContentLength64, "The listener observed an unexpected content length.");

				var receivedPrefix = new byte [bodyPrefix.Length];
				int totalBytesRead = 0;
				while (totalBytesRead < receivedPrefix.Length) {
					int bytesRead = await context.Request.InputStream.ReadAsync (
						receivedPrefix,
						totalBytesRead,
						receivedPrefix.Length - totalBytesRead
					).ConfigureAwait (false);
					Assert.Greater (bytesRead, 0, "The partial request ended before the body prefix was received.");
					totalBytesRead += bytesRead;
				}
				CollectionAssert.AreEqual (bodyPrefix, receivedPrefix, "The listener received an unexpected request body prefix.");

				var pendingReadBuffer = new byte [1];
				pendingReadTask = context.Request.InputStream.ReadAsync (pendingReadBuffer, 0, pendingReadBuffer.Length);
				var prematureCompletion = await Task.WhenAny (pendingReadTask, Task.Delay (250)).ConfigureAwait (false);
				Assert.AreNotSame (pendingReadTask, prematureCompletion, "The request body read should remain pending while the client keeps the incomplete request open.");

				response.Abort ();
				try {
					await WaitForTask (pendingReadTask, requestTimeoutMilliseconds, "Aborting the response did not terminate the pending request body read.").ConfigureAwait (false);
					int bytesRead = await pendingReadTask.ConfigureAwait (false);
					Assert.AreEqual (0, bytesRead, "The aborted request body read should not produce additional bytes.");
				} catch (IOException) {
				} catch (HttpListenerException) {
				} catch (ObjectDisposedException) {
				}
			} finally {
				response.Abort ();
				listener.Abort ();
				try {
					await WaitForTask (pendingReadTask, requestTimeoutMilliseconds, "The pending request body read did not finish during cleanup.").ConfigureAwait (false);
				} catch (IOException) {
				} catch (HttpListenerException) {
				} catch (ObjectDisposedException) {
				}
			}
		}

		static async Task WaitForTask (Task task, int timeoutMilliseconds, string failureMessage)
		{
			try {
				await task.WaitAsync (TimeSpan.FromMilliseconds (timeoutMilliseconds)).ConfigureAwait (false);
			} catch (TimeoutException) {
				if (task.IsFaulted)
					await task.ConfigureAwait (false);
				Assert.Fail ($"{failureMessage} Timeout: {timeoutMilliseconds}ms.");
			}
		}

		sealed class ControlledSeekableStream : MemoryStream
		{
			readonly TaskCompletionSource<bool> firstWriteCompleted = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			readonly TaskCompletionSource<bool> releaseFirstCopy = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			int copyCount;
			int firstWriteLength;

			public ControlledSeekableStream (byte [] content)
				: base (content, writable: false)
			{
			}

			public int CopyCount => Volatile.Read (ref copyCount);

			public int FirstWriteLength => Volatile.Read (ref firstWriteLength);

			public Task FirstWriteCompletedTask => firstWriteCompleted.Task;

			public bool IsFirstCopyBlocked => firstWriteCompleted.Task.Status == TaskStatus.RanToCompletion && !releaseFirstCopy.Task.IsCompleted;

			public void ReleaseFirstCopy ()
			{
				releaseFirstCopy.TrySetResult (true);
				firstWriteCompleted.TrySetCanceled ();
			}

			public override Task CopyToAsync (Stream destination, int bufferSize, CancellationToken cancellationToken)
			{
				ArgumentNullException.ThrowIfNull (destination);
				if (bufferSize <= 0)
					throw new ArgumentOutOfRangeException (nameof (bufferSize));

				cancellationToken.ThrowIfCancellationRequested ();
				bool gateFirstCopy = Interlocked.Increment (ref copyCount) == 1;
				return CopyToAsyncCore (destination, bufferSize, cancellationToken, gateFirstCopy);
			}

			async Task CopyToAsyncCore (Stream destination, int bufferSize, CancellationToken cancellationToken, bool gateFirstCopy)
			{
				try {
					var buffer = new byte [bufferSize];
					int bytesRead;
					bool firstWrite = true;
					while ((bytesRead = await ReadAsync (buffer, 0, buffer.Length, cancellationToken).ConfigureAwait (false)) > 0) {
						await destination.WriteAsync (buffer, 0, bytesRead, cancellationToken).ConfigureAwait (false);
						if (gateFirstCopy && firstWrite) {
							await destination.FlushAsync (cancellationToken).ConfigureAwait (false);
							firstWrite = false;
							Volatile.Write (ref firstWriteLength, bytesRead);
							firstWriteCompleted.TrySetResult (true);
							await releaseFirstCopy.Task.WaitAsync (cancellationToken).ConfigureAwait (false);
						}
					}
				} catch (Exception ex) {
					if (gateFirstCopy)
						firstWriteCompleted.TrySetException (ex);
					throw;
				}
			}
		}

		sealed class BufferedDestinationStream : Stream
		{
			readonly MemoryStream buffered = new MemoryStream ();
			readonly MemoryStream committed = new MemoryStream ();
			int flushCount;

			public int FlushCount => Volatile.Read (ref flushCount);

			public override bool CanRead => false;

			public override bool CanSeek => false;

			public override bool CanWrite => true;

			public override long Length => throw new NotSupportedException ();

			public override long Position {
				get => throw new NotSupportedException ();
				set => throw new NotSupportedException ();
			}

			public byte [] ToArray () => committed.ToArray ();

			public override void Flush ()
			{
				buffered.Position = 0;
				buffered.CopyTo (committed);
				buffered.SetLength (0);
				buffered.Position = 0;
				Interlocked.Increment (ref flushCount);
			}

			public override Task FlushAsync (CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested ();
				Flush ();
				return Task.CompletedTask;
			}

			public override int Read (byte [] buffer, int offset, int count) => throw new NotSupportedException ();

			public override long Seek (long offset, SeekOrigin origin) => throw new NotSupportedException ();

			public override void SetLength (long value) => throw new NotSupportedException ();

			public override void Write (byte [] buffer, int offset, int count)
			{
				buffered.Write (buffer, offset, count);
			}

			public override Task WriteAsync (byte [] buffer, int offset, int count, CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested ();
				Write (buffer, offset, count);
				return Task.CompletedTask;
			}

			protected override void Dispose (bool disposing)
			{
				if (disposing) {
					buffered.Dispose ();
					committed.Dispose ();
				}
				base.Dispose (disposing);
			}
		}
	}
}
