#nullable enable

#pragma warning disable CS0618 // These tests intentionally exercise the obsolete AndroidMessageHandler.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Xamarin.Android.Net;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.NetTests
{
	[TestClass]
	[TestCategory ("AndroidMessageHandlerCancellation")]
	[TestCategory ("InetAccess")]
	public partial class AndroidMessageHandlerCancellationTests
	{
		const int StalledResponseContentLength = 1024 * 1024;
		const int UploadContentLength = 16 * 1024 * 1024;
		const int PromptCancellationTimeoutMilliseconds = 30000;

		static readonly byte[] InitialResponseChunk = [42];
		StalledResponseServer? stalledResponseServer;

		[TestInitialize]
		public void SetUp ()
		{
			stalledResponseServer = new StalledResponseServer ();
		}

		[TestCleanup]
		public void TearDown ()
		{
			var server = stalledResponseServer;
			stalledResponseServer = null;

			// NUnitLite used by the on-device tests does not support async TearDown methods.
			if (server != null)
				server.Stop ();
		}

		[TestMethod]
		public async Task SocketProbeDoesNotTreatPendingWorkAsSocketIo ()
		{
			var probe = new JavaIoProbe ();
			var started = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			using var release = new ManualResetEventSlim ();
			Task work = probe.Observe (() => Task.Run (() => {
				started.TrySetResult (true);
				release.Wait ();
			}));
			try {
				await started.Task.WaitAsync (TimeSpan.FromMilliseconds (PromptCancellationTimeoutMilliseconds)).ConfigureAwait (false);
				Assert.IsFalse (work.IsCompleted);
				Assert.IsFalse (probe.IsInSocketOperation ("read"));
				Assert.IsFalse (probe.IsInSocketOperation ("write"));
			} finally {
				release.Set ();
				await work.WaitAsync (TimeSpan.FromMilliseconds (PromptCancellationTimeoutMilliseconds)).ConfigureAwait (false);
			}
		}

		[TestMethod]
		public async Task ResponseContentReadBodyReadCancellationIsPrompt ()
		{
			var server = stalledResponseServer ?? throw new InvalidOperationException ("The stalled response server was not initialized.");
			using var handler = new AndroidMessageHandler ();
			using var observer = new ObservingResponseHandler (handler);
			using var client = new HttpClient (observer);
			using var cts = new CancellationTokenSource ();
			using var request = new HttpRequestMessage (HttpMethod.Get, $"http://localhost:{server.Port}/");

			Task readTask = client.SendAsync (request, HttpCompletionOption.ResponseContentRead, cts.Token);

			await WaitForSocketOperation (observer.PendingBodyRead, observer.BodyReadProbe, "read", readTask, server.ReleaseResponseBody).ConfigureAwait (false);
			cts.Cancel ();
			await AssertCanceledPromptly (readTask, server.ReleaseResponseBody).ConfigureAwait (false);
		}

		[TestMethod]
		public async Task RequestBodyUploadCancellationIsPrompt ()
		{
			using var uploadServer = new StalledRequestServer ();
			using var handler = new AndroidMessageHandler ();
			using var client = new HttpClient (handler);
			using var cts = new CancellationTokenSource ();
			var uploadContent = new StreamingContent (UploadContentLength);
			using var request = new HttpRequestMessage (HttpMethod.Put, $"http://localhost:{uploadServer.Port}/upload") {
				// A large body ensures the socket send buffer fills while the server stalls reading it,
				// so the upload is still in progress when the caller cancels. The content streams the
				// bytes in small chunks instead of allocating the whole body up front.
				Content = uploadContent,
			};

			Task sendTask = client.SendAsync (request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

			try {
				await WaitForSocketOperation (uploadServer.BodyStartedTask, uploadContent.DestinationWriteProbe, "write", sendTask,
					uploadServer.ReleaseRequestBody, () => {
						if (!uploadContent.DestinationWritePending)
							return false;
						cts.Cancel ();
						return true;
					}).ConfigureAwait (false);
			} catch {
				cts.Cancel ();
				uploadServer.ReleaseRequestBody ();
				await ObserveReadTaskAfterRelease (sendTask).ConfigureAwait (false);
				throw;
			}
			await AssertCanceledPromptly (sendTask, uploadServer.ReleaseRequestBody).ConfigureAwait (false);
		}

		[TestMethod]
		public async Task ResponseHeadersReadBodyReadCancellationIsPrompt ()
		{
			var server = stalledResponseServer ?? throw new InvalidOperationException ("The stalled response server was not initialized.");
			using var handler = new AndroidMessageHandler ();
			using var observer = new ObservingResponseHandler (handler);
			using var client = new HttpClient (observer);
			using var request = new HttpRequestMessage (HttpMethod.Get, $"http://localhost:{server.Port}/");
			var response = await client.SendAsync (request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait (false);
			using var readCts = new CancellationTokenSource ();
			var responseStream = await response.Content.ReadAsStreamAsync ().ConfigureAwait (false);
			Assert.AreEqual (1, await ReadInitialResponseByte (responseStream, server).ConfigureAwait (false));

			Task readContentTask = response.Content.ReadAsByteArrayAsync (readCts.Token);

			await WaitForSocketOperation (observer.PendingBodyRead, observer.BodyReadProbe, "read", readContentTask, server.ReleaseResponseBody).ConfigureAwait (false);
			readCts.Cancel ();
			await AssertCanceledPromptly (readContentTask, server.ReleaseResponseBody).ConfigureAwait (false);
			response.Dispose ();
		}

		[TestMethod]
		public async Task ResponseHeadersReadDisposeResponseDuringCanceledBodyReadDoesNotRaceStreamDispose ()
		{
			var server = stalledResponseServer ?? throw new InvalidOperationException ("The stalled response server was not initialized.");
			using var handler = new AndroidMessageHandler ();
			using var observer = new ObservingResponseHandler (handler);
			using var client = new HttpClient (observer);
			using var request = new HttpRequestMessage (HttpMethod.Get, $"http://localhost:{server.Port}/");
			var response = await client.SendAsync (request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait (false);
			var responseStream = await response.Content.ReadAsStreamAsync ().ConfigureAwait (false);
			using var readCts = new CancellationTokenSource ();
			var buffer = new byte [1];

			Assert.AreEqual (1, await ReadInitialResponseByte (responseStream, server).ConfigureAwait (false));
			Task readTask = responseStream.ReadAsync (buffer, 0, buffer.Length, readCts.Token);

			await WaitForSocketOperation (observer.PendingBodyRead, observer.BodyReadProbe, "read", readTask, server.ReleaseResponseBody).ConfigureAwait (false);
			readCts.Cancel ();
			await DisposeResponsePromptly (response, server.ReleaseResponseBody).ConfigureAwait (false);
			await AssertCanceledPromptly (readTask, server.ReleaseResponseBody).ConfigureAwait (false);
		}

		[TestMethod]
		public async Task ResponseHeadersReadDisposeResponseDuringBodyReadDoesNotRaceStreamDispose ()
		{
			var server = stalledResponseServer ?? throw new InvalidOperationException ("The stalled response server was not initialized.");
			using var handler = new AndroidMessageHandler ();
			using var observer = new ObservingResponseHandler (handler);
			using var client = new HttpClient (observer);
			using var request = new HttpRequestMessage (HttpMethod.Get, $"http://localhost:{server.Port}/");
			var response = await client.SendAsync (request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait (false);
			var responseStream = await response.Content.ReadAsStreamAsync ().ConfigureAwait (false);
			var buffer = new byte [1];

			Assert.AreEqual (1, await ReadInitialResponseByte (responseStream, server).ConfigureAwait (false));
			Task<int> readTask = responseStream.ReadAsync (buffer, 0, buffer.Length);

			await WaitForSocketOperation (observer.PendingBodyRead, observer.BodyReadProbe, "read", readTask, server.ReleaseResponseBody).ConfigureAwait (false);
			await DisposeResponsePromptly (response, server.ReleaseResponseBody).ConfigureAwait (false);
			await AssertReadAbortedPromptly (readTask, server.ReleaseResponseBody).ConfigureAwait (false);
		}

		// Token-only cancellation must disconnect the transport and unwind the parked read without
		// requiring the response to be disposed.
		[TestMethod]
		public async Task ResponseHeadersReadTokenPassedToReadCancelOnlyUnwindsCleanly ()
		{
			var server = stalledResponseServer ?? throw new InvalidOperationException ("The stalled response server was not initialized.");
			using var handler = new AndroidMessageHandler ();
			using var observer = new ObservingResponseHandler (handler);
			using var client = new HttpClient (observer);
			using var request = new HttpRequestMessage (HttpMethod.Get, $"http://localhost:{server.Port}/");
			var response = await client.SendAsync (request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait (false);
			var responseStream = await response.Content.ReadAsStreamAsync ().ConfigureAwait (false);
			using var readCts = new CancellationTokenSource ();
			var buffer = new byte [1];

			Assert.AreEqual (1, await ReadInitialResponseByte (responseStream, server).ConfigureAwait (false));
			Task readTask = responseStream.ReadAsync (buffer, 0, buffer.Length, readCts.Token);

			await WaitForSocketOperation (observer.PendingBodyRead, observer.BodyReadProbe, "read", readTask, server.ReleaseResponseBody).ConfigureAwait (false);
			readCts.Cancel ();
			// No Dispose here: the read must unwind purely from the token-driven abort.
			await AssertCanceledPromptly (readTask, server.ReleaseResponseBody).ConfigureAwait (false);
			response.Dispose ();
		}

		// Concurrent caller cancellation and response disposal (the gRPC cancellation shape) must
		// coalesce their disconnect request and never race stream disposal or crash the process.
		[TestMethod]
		public async Task ResponseHeadersReadTokenPassedToReadCancelAndDisposeConcurrentlyDoesNotCrash ()
		{
			var server = stalledResponseServer ?? throw new InvalidOperationException ("The stalled response server was not initialized.");
			using var handler = new AndroidMessageHandler ();
			using var observer = new ObservingResponseHandler (handler);
			using var client = new HttpClient (observer);
			using var request = new HttpRequestMessage (HttpMethod.Get, $"http://localhost:{server.Port}/");
			var response = await client.SendAsync (request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait (false);
			var responseStream = await response.Content.ReadAsStreamAsync ().ConfigureAwait (false);
			using var readCts = new CancellationTokenSource ();
			var buffer = new byte [1];

			Assert.AreEqual (1, await ReadInitialResponseByte (responseStream, server).ConfigureAwait (false));
			Task<int> readTask = responseStream.ReadAsync (buffer, 0, buffer.Length, readCts.Token);

			await WaitForSocketOperation (observer.PendingBodyRead, observer.BodyReadProbe, "read", readTask, server.ReleaseResponseBody).ConfigureAwait (false);

			// Fire both triggers concurrently, mirroring GrpcCall.CancelCallFromCancellationToken.
			Task disposeTask = Task.Run (() => response.Dispose ());
			readCts.Cancel ();
			try {
				await disposeTask.WaitAsync (TimeSpan.FromMilliseconds (PromptCancellationTimeoutMilliseconds)).ConfigureAwait (false);
			} catch (TimeoutException) {
				server.ReleaseResponseBody ();
				throw;
			}

			await AssertReadFinishesPromptly (readTask, server.ReleaseResponseBody).ConfigureAwait (false);
		}

		static async Task WaitForSocketOperation (Task bodyStarted, JavaIoProbe probe, string operation, Task requestTask, Action releaseBody, Func<bool>? onObserved = null)
		{
			var timeoutTask = Task.Delay (PromptCancellationTimeoutMilliseconds);
			try {
				var completed = await Task.WhenAny (bodyStarted, requestTask, timeoutTask).ConfigureAwait (false);
				if (completed == requestTask) {
					await requestTask.ConfigureAwait (false);
					Assert.Fail ($"The request completed before its socket {operation} started.");
				}
				if (completed == timeoutTask)
					Assert.Fail ($"The request body did not start within {PromptCancellationTimeoutMilliseconds}ms.");
				await bodyStarted.ConfigureAwait (false);

				while (true) {
					if (requestTask.IsCompleted) {
						await requestTask.ConfigureAwait (false);
						Assert.Fail ($"The request completed before its Java socket {operation} was observed.");
					}
					if (probe.IsInSocketOperation (operation) && (onObserved == null || onObserved ()))
						return;
					if (timeoutTask.IsCompleted)
						Assert.Fail ($"A Java socket {operation} was not observed within {PromptCancellationTimeoutMilliseconds}ms.");
					await Task.WhenAny (requestTask, timeoutTask, Task.Delay (20)).ConfigureAwait (false);
				}
			} catch {
				releaseBody ();
				await ObserveReadTaskAfterRelease (requestTask).ConfigureAwait (false);
				throw;
			}
		}

		static async Task<int> ReadInitialResponseByte (Stream responseStream, StalledResponseServer server)
		{
			var buffer = new byte [1];
			var readTask = responseStream.ReadAsync (buffer, 0, buffer.Length);
			var completed = await Task.WhenAny (readTask, Task.Delay (PromptCancellationTimeoutMilliseconds)).ConfigureAwait (false);
			if (completed != readTask) {
				server.ReleaseResponseBody ();
				await ObserveReadTaskAfterRelease (readTask).ConfigureAwait (false);
				Assert.Fail ($"Initial response body read did not complete within {PromptCancellationTimeoutMilliseconds}ms.");
			}
			return await readTask.ConfigureAwait (false);
		}

		// Accepts any abort outcome (cancellation OR dispose-driven exception); only a hang
		// or a native crash (which kills the test process) is a failure.
		static async Task AssertReadFinishesPromptly (Task<int> readTask, Action releaseBody)
		{
			var completed = await Task.WhenAny (readTask, Task.Delay (PromptCancellationTimeoutMilliseconds)).ConfigureAwait (false);
			if (completed != readTask) {
				releaseBody ();
				await ObserveReadTaskAfterRelease (readTask).ConfigureAwait (false);
				Assert.Fail ($"Response body read did not finish within {PromptCancellationTimeoutMilliseconds}ms.");
			}
			try {
				await readTask.ConfigureAwait (false);
			} catch (Exception ex) when (ex is OperationCanceledException or System.IO.IOException or Java.IO.IOException or InvalidDataException or ObjectDisposedException or WebException) {
				return;
			}
		}

		static async Task DisposeResponsePromptly (HttpResponseMessage response, Action releaseBody)
		{
			var disposeTask = Task.Run (response.Dispose);
			var completed = await Task.WhenAny (disposeTask, Task.Delay (PromptCancellationTimeoutMilliseconds)).ConfigureAwait (false);
			if (completed != disposeTask) {
				releaseBody ();
				await ObserveReadTaskAfterRelease (disposeTask).ConfigureAwait (false);
				Assert.Fail ("Disposing the response blocked while aborting its active body read.");
			}
			await disposeTask.ConfigureAwait (false);
		}

		static int GetAvailablePort ()
		{
			using var tcpListener = new TcpListener (IPAddress.Loopback, 0);
			tcpListener.Start ();
			int port = ((IPEndPoint) tcpListener.LocalEndpoint).Port;
			tcpListener.Stop ();
			return port;
		}

		static async Task AssertCanceledPromptly (Task readTask, Action releaseBody)
		{
			var completed = await Task.WhenAny (readTask, Task.Delay (PromptCancellationTimeoutMilliseconds)).ConfigureAwait (false);
			if (completed != readTask) {
				releaseBody ();
				await ObserveReadTaskAfterRelease (readTask).ConfigureAwait (false);
				Assert.Fail ($"Response body read did not observe cancellation within {PromptCancellationTimeoutMilliseconds}ms.");
			}

			try {
				await readTask.ConfigureAwait (false);
				Assert.Fail ("Response body read completed successfully after cancellation.");
			} catch (OperationCanceledException) {
				return;
			}
		}

		static async Task AssertReadAbortedPromptly (Task<int> readTask, Action releaseBody)
		{
			var completed = await Task.WhenAny (readTask, Task.Delay (PromptCancellationTimeoutMilliseconds)).ConfigureAwait (false);
			if (completed != readTask) {
				releaseBody ();
				await ObserveReadTaskAfterRelease (readTask).ConfigureAwait (false);
				Assert.Fail ($"Response body read was not aborted within {PromptCancellationTimeoutMilliseconds}ms.");
			}

			try {
				Assert.AreEqual (0, await readTask.ConfigureAwait (false));
			} catch (Exception ex) when (ex is OperationCanceledException or System.IO.IOException or Java.IO.IOException or InvalidDataException or ObjectDisposedException or WebException) {
				// Disposing the response aborts the read; it may surface either as an OperationCanceledException
				// (the abort is driven by an internal CancellationTokenSource) or as a transport exception.
				return;
			}
		}

		static async Task ObserveReadTaskAfterRelease (Task readTask)
		{
			var completed = await Task.WhenAny (readTask, Task.Delay (PromptCancellationTimeoutMilliseconds)).ConfigureAwait (false);
			if (completed != readTask)
				return;

			try {
				await readTask.ConfigureAwait (false);
			} catch (Exception ex) {
				Console.WriteLine ($"Exception after releasing stalled response body: {ex}");
			}
		}

		sealed class ObservingResponseHandler : DelegatingHandler
		{
			readonly TaskCompletionSource<bool> pendingBodyRead = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			readonly JavaIoProbe bodyReadProbe = new JavaIoProbe ();

			public ObservingResponseHandler (HttpMessageHandler innerHandler) : base (innerHandler) {}

			public Task PendingBodyRead => pendingBodyRead.Task;
			public JavaIoProbe BodyReadProbe => bodyReadProbe;

			protected override async Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken cancellationToken)
			{
				var response = await base.SendAsync (request, cancellationToken).ConfigureAwait (false);
				try {
					var content = response.Content;
					var stream = await content.ReadAsStreamAsync (cancellationToken).ConfigureAwait (false);
					var observedContent = new StreamContent (new ObservingStream (stream, content, pendingBodyRead, bodyReadProbe));
					foreach (var header in content.Headers) {
						if (!observedContent.Headers.TryAddWithoutValidation (header.Key, header.Value))
							throw new InvalidOperationException ($"Unable to preserve response header '{header.Key}'.");
					}
					response.Content = observedContent;
					return response;
				} catch {
					response.Dispose ();
					throw;
				}
			}
		}

		sealed class ObservingStream : Stream
		{
			readonly Stream inner;
			readonly HttpContent content;
			readonly TaskCompletionSource<bool> pendingBodyRead;
			readonly JavaIoProbe bodyReadProbe;
			bool receivedData;

			public ObservingStream (Stream inner, HttpContent content, TaskCompletionSource<bool> pendingBodyRead, JavaIoProbe bodyReadProbe)
			{
				this.inner = inner;
				this.content = content;
				this.pendingBodyRead = pendingBodyRead;
				this.bodyReadProbe = bodyReadProbe;
			}

			public override bool CanRead => inner.CanRead;
			public override bool CanSeek => inner.CanSeek;
			public override bool CanWrite => inner.CanWrite;
			public override long Length => inner.Length;
			public override long Position { get => inner.Position; set => inner.Position = value; }
			public override void Flush () => inner.Flush ();
			public override long Seek (long offset, SeekOrigin origin) => inner.Seek (offset, origin);
			public override void SetLength (long value) => inner.SetLength (value);
			public override void Write (byte [] buffer, int offset, int count) => inner.Write (buffer, offset, count);
			public override int Read (byte [] buffer, int offset, int count)
			{
				int read = inner.Read (buffer, offset, count);
				receivedData |= read > 0;
				return read;
			}

			public override Task<int> ReadAsync (byte [] buffer, int offset, int count, CancellationToken cancellationToken) =>
				ReadAsync (buffer.AsMemory (offset, count), cancellationToken).AsTask ();

			public override async ValueTask<int> ReadAsync (Memory<byte> buffer, CancellationToken cancellationToken = default)
			{
				var read = receivedData
					? bodyReadProbe.Observe (() => inner.ReadAsync (buffer, cancellationToken))
					: inner.ReadAsync (buffer, cancellationToken);
				if (receivedData && !read.IsCompleted)
					pendingBodyRead.TrySetResult (true);
				int result = await read.ConfigureAwait (false);
				receivedData |= result > 0;
				return result;
			}

			public override async Task CopyToAsync (Stream destination, int bufferSize, CancellationToken cancellationToken)
			{
				var firstWrite = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
				var destinationObserver = new CopyDestinationStream (destination, () => firstWrite.TrySetResult (true));
				Task copyTask = bodyReadProbe.Observe (() => inner.CopyToAsync (destinationObserver, bufferSize, cancellationToken));
				if ((receivedData || await Task.WhenAny (firstWrite.Task, copyTask).ConfigureAwait (false) == firstWrite.Task) && !copyTask.IsCompleted)
					pendingBodyRead.TrySetResult (true);
				await copyTask.ConfigureAwait (false);
			}

			protected override void Dispose (bool disposing)
			{
				if (disposing)
					content.Dispose ();
				base.Dispose (disposing);
			}
		}

		sealed class JavaIoProbe
		{
			sealed class ActiveThread
			{
				public ActiveThread (Java.Lang.Thread thread) => Thread = thread;
				public Java.Lang.Thread Thread { get; }
			}

			readonly object gate = new object ();
			readonly Dictionary<int, ActiveThread> activeThreads = new Dictionary<int, ActiveThread> ();
			readonly AsyncLocal<JavaIoProbe?> current;
			Exception? failure;

			public JavaIoProbe () => current = new AsyncLocal<JavaIoProbe?> (OnContextChanged);

			public T Observe<T> (Func<T> operation)
			{
				var previous = current.Value;
				current.Value = this;
				try {
					return operation ();
				} finally {
					current.Value = previous;
				}
			}

			// Tag the operation's execution context so unrelated Java socket threads cannot satisfy the probe.
			void OnContextChanged (AsyncLocalValueChangedArgs<JavaIoProbe?> change)
			{
				if (!change.ThreadContextChanged || ReferenceEquals (change.PreviousValue, change.CurrentValue))
					return;
				try {
					int id = Environment.CurrentManagedThreadId;
					if (ReferenceEquals (change.PreviousValue, this)) {
						lock (gate)
							activeThreads.Remove (id);
					}
					if (ReferenceEquals (change.CurrentValue, this)) {
						var thread = Java.Lang.Thread.CurrentThread () ?? throw new InvalidOperationException ("The I/O worker has no Java thread.");
						lock (gate)
							activeThreads [id] = new ActiveThread (thread);
					}
				} catch (Exception ex) {
					lock (gate)
						failure ??= ex;
				}
			}

			public bool IsInSocketOperation (string operation)
			{
				KeyValuePair<int, ActiveThread> [] threads;
				lock (gate) {
					if (failure != null)
						throw new InvalidOperationException ("Unable to observe the Java I/O worker.", failure);
					threads = new List<KeyValuePair<int, ActiveThread>> (activeThreads).ToArray ();
				}

				foreach (var pair in threads) {
					bool socketFrame = false;
					bool nativeFrame = false;
					foreach (var frame in pair.Value.Thread.GetStackTrace ()) {
						string? name = frame.ClassName;
						string? method = frame.MethodName;
						if (name == null || method == null)
							continue;
						if (method == operation && (name == "libcore.io.IoBridge" || name.EndsWith (".SocketInputStream", StringComparison.Ordinal) || name.EndsWith (".SocketOutputStream", StringComparison.Ordinal)))
							socketFrame = true;
						if (frame.IsNativeMethod && method.IndexOf (operation, StringComparison.OrdinalIgnoreCase) >= 0)
							nativeFrame = true;
					}
					if (socketFrame && nativeFrame) {
						lock (gate) {
							if (activeThreads.TryGetValue (pair.Key, out var currentThread) && ReferenceEquals (currentThread, pair.Value))
								return true;
						}
					}
				}
				return false;
			}
		}

		sealed class CopyDestinationStream : Stream
		{
			readonly Stream inner;
			readonly Action onWrite;

			public CopyDestinationStream (Stream inner, Action onWrite)
			{
				this.inner = inner;
				this.onWrite = onWrite;
			}

			public override bool CanRead => inner.CanRead;
			public override bool CanSeek => inner.CanSeek;
			public override bool CanWrite => inner.CanWrite;
			public override long Length => inner.Length;
			public override long Position { get => inner.Position; set => inner.Position = value; }
			public override void Flush () => inner.Flush ();
			public override long Seek (long offset, SeekOrigin origin) => inner.Seek (offset, origin);
			public override void SetLength (long value) => inner.SetLength (value);
			public override int Read (byte [] buffer, int offset, int count) => inner.Read (buffer, offset, count);
			public override void Write (byte [] buffer, int offset, int count)
			{
				inner.Write (buffer, offset, count);
				onWrite ();
			}

			public override async Task WriteAsync (byte [] buffer, int offset, int count, CancellationToken cancellationToken)
			{
				await inner.WriteAsync (buffer, offset, count, cancellationToken).ConfigureAwait (false);
				onWrite ();
			}

			public override async ValueTask WriteAsync (ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
			{
				await inner.WriteAsync (buffer, cancellationToken).ConfigureAwait (false);
				onWrite ();
			}
		}

		sealed class StalledResponseServer
		{
			readonly HttpListener listener;
			readonly TaskCompletionSource<bool> releaseBody = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			readonly Task serverTask;

			public StalledResponseServer ()
			{
				Port = GetAvailablePort ();
				listener = new HttpListener ();
				listener.Prefixes.Add ($"http://localhost:{Port}/");
				listener.Start ();

				serverTask = ServeStalledResponseBody ();
			}

			public int Port { get; }

			public void Stop ()
			{
				ReleaseResponseBody ();
				listener.Close ();
				ObserveServerTask ().GetAwaiter ().GetResult ();
			}

			public void ReleaseResponseBody ()
			{
				releaseBody.TrySetResult (true);
			}

			async Task ServeStalledResponseBody ()
			{
				bool responseStarted = false;
				try {
					var context = await listener.GetContextAsync ().ConfigureAwait (false);
					using var response = context.Response;
					response.StatusCode = 200;
					response.ContentLength64 = StalledResponseContentLength;
					await response.OutputStream.WriteAsync (InitialResponseChunk, 0, InitialResponseChunk.Length).ConfigureAwait (false);
					await response.OutputStream.FlushAsync ().ConfigureAwait (false);
					responseStarted = true;

					await releaseBody.Task.ConfigureAwait (false);
					await WriteRemainingResponseBody (response).ConfigureAwait (false);
				} catch (ObjectDisposedException) when (!responseStarted && releaseBody.Task.IsCompleted) {
				} catch (Exception ex) when (responseStarted) {
					Console.WriteLine ($"Exception while serving stalled response body: {ex}");
				}
			}

			async Task WriteRemainingResponseBody (HttpListenerResponse response)
			{
				var buffer = new byte [4096];
				int remainingBytes = StalledResponseContentLength - InitialResponseChunk.Length;
				while (remainingBytes > 0) {
					int bytesToWrite = Math.Min (remainingBytes, buffer.Length);
					await response.OutputStream.WriteAsync (buffer, 0, bytesToWrite).ConfigureAwait (false);
					remainingBytes -= bytesToWrite;
				}
			}

			async Task ObserveServerTask ()
			{
				var completed = await Task.WhenAny (serverTask, Task.Delay (PromptCancellationTimeoutMilliseconds)).ConfigureAwait (false);
				if (completed != serverTask)
					throw new TimeoutException ("The stalled response server did not stop.");

				await serverTask.ConfigureAwait (false);
			}
		}

		sealed class StreamingContent : HttpContent
		{
			readonly long length;
			readonly JavaIoProbe destinationWriteProbe = new JavaIoProbe ();
			Task? destinationWrite;

			public StreamingContent (long length)
			{
				this.length = length;
			}

			public JavaIoProbe DestinationWriteProbe => destinationWriteProbe;
			public bool DestinationWritePending {
				get {
					var write = Volatile.Read (ref destinationWrite);
					return write != null && !write.IsCompleted;
				}
			}

			protected override Task<Stream> CreateContentReadStreamAsync ()
			{
				// AndroidMessageHandler uses ReadAsStreamAsync () before uploading; override this to avoid
				// HttpContent's default full-buffering behavior for custom content.
				return Task.FromResult<Stream> (new ZeroStream (length, this));
			}

			protected override async Task SerializeToStreamAsync (Stream stream, System.Net.TransportContext? context)
			{
				using var source = new ZeroStream (length, this);
				await source.CopyToAsync (stream, 4096).ConfigureAwait (false);
			}

			protected override bool TryComputeLength (out long computedLength)
			{
				computedLength = length;
				return true;
			}

			sealed class ZeroStream : Stream
			{
				readonly long length;
				readonly StreamingContent owner;
				long position;

				public ZeroStream (long length, StreamingContent owner)
				{
					this.length = length;
					this.owner = owner;
				}

				public override bool CanRead => true;
				public override bool CanSeek => false;
				public override bool CanWrite => false;
				public override long Length => length;

				public override long Position {
					get => position;
					set => throw new NotSupportedException ();
				}

				public override int Read (byte [] buffer, int offset, int count)
				{
					if (position >= length)
						return 0;

					int toRead = (int) Math.Min (count, length - position);
					Array.Clear (buffer, offset, toRead);
					position += toRead;
					return toRead;
				}

				public override ValueTask<int> ReadAsync (Memory<byte> buffer, CancellationToken cancellationToken = default)
				{
					if (position >= length)
						return new ValueTask<int> (0);

					int toRead = (int) Math.Min (buffer.Length, length - position);
					buffer.Span.Slice (0, toRead).Clear ();
					position += toRead;
					return new ValueTask<int> (toRead);
				}

				public override async Task CopyToAsync (Stream destination, int bufferSize, CancellationToken cancellationToken)
				{
					var buffer = new byte [bufferSize];
					while (true) {
						int bytesRead = await ReadAsync (buffer.AsMemory (), cancellationToken).ConfigureAwait (false);
						if (bytesRead == 0)
							return;
						var writeTask = owner.destinationWriteProbe.Observe (() => destination.WriteAsync (buffer, 0, bytesRead, cancellationToken));
						Volatile.Write (ref owner.destinationWrite, writeTask);
						try {
							await writeTask.ConfigureAwait (false);
						} finally {
							_ = Interlocked.CompareExchange (ref owner.destinationWrite, null, writeTask);
						}
					}
				}

				public override void Flush () { }
				public override long Seek (long offset, SeekOrigin origin) => throw new NotSupportedException ();
				public override void SetLength (long value) => throw new NotSupportedException ();
				public override void Write (byte [] buffer, int offset, int count) => throw new NotSupportedException ();
			}
		}

		sealed class StalledRequestServer : IDisposable
		{
			readonly TcpListener listener;
			readonly TaskCompletionSource<bool> bodyStarted = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			readonly TaskCompletionSource<bool> releaseBody = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			readonly Task serverTask;

			public StalledRequestServer ()
			{
				listener = new TcpListener (IPAddress.Loopback, 0);
				listener.Server.ReceiveBufferSize = 4096;
				listener.Start ();
				Port = ((IPEndPoint) listener.LocalEndpoint).Port;

				serverTask = StallRequestBody ();
			}

			public int Port { get; }

			public Task BodyStartedTask => bodyStarted.Task;

			public void ReleaseRequestBody ()
			{
				releaseBody.TrySetResult (true);
			}

			public void Dispose ()
			{
				ReleaseRequestBody ();
				try {
					listener.Stop ();
				} catch (Exception ex) {
					Console.WriteLine ($"Exception while stopping the stalled request server: {ex}");
				}
			}

			async Task StallRequestBody ()
			{
				try {
					using var client = await listener.AcceptTcpClientAsync ().ConfigureAwait (false);
					using var stream = client.GetStream ();

					// Confirm that the upload has started, then stop reading to stall the client.
					await ReadRequestHeaders (stream).ConfigureAwait (false);
					var firstByte = new byte [1];
					if (await stream.ReadAsync (firstByte, 0, firstByte.Length).ConfigureAwait (false) != 1)
						throw new IOException ("The client closed before sending the request body.");
					bodyStarted.TrySetResult (true);

					await releaseBody.Task.ConfigureAwait (false);
				} catch (Exception ex) {
					if (!BodyStartedTask.IsCompleted) {
						bodyStarted.TrySetException (ex);
						return;
					}
					Console.WriteLine ($"Exception while stalling the request body: {ex}");
				}
			}

			static async Task ReadRequestHeaders (NetworkStream stream)
			{
				var buffer = new byte [1];
				int consecutiveLineEndChars = 0;
				while (consecutiveLineEndChars < 4) {
					int read = await stream.ReadAsync (buffer, 0, 1).ConfigureAwait (false);
					if (read == 0)
						break;

					byte b = buffer [0];
					if (b == '\r' || b == '\n')
						consecutiveLineEndChars++;
					else
						consecutiveLineEndChars = 0;
				}
			}
		}
	}
}
