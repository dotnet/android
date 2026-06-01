#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Xamarin.Android.Net;

using NUnit.Framework;

namespace Xamarin.Android.NetTests
{
	[TestFixture]
	[Category ("AndroidMessageHandlerCancellation")]
	[Category ("InetAccess")]
	public class AndroidMessageHandlerCancellationTests
	{
		const int StalledResponseContentLength = 1024 * 1024;
		const int BodyReadBlockDelayMilliseconds = 250;
		const int PromptCancellationTimeoutMilliseconds = 3000;

		static readonly byte[] InitialResponseChunk = new byte[] { 42 };

		static readonly byte[] InitialResponseChunk = [42];

		[SetUp]
		public void SetUp ()
		{
			stalledResponseServer = new StalledResponseServer ();
		}

		[TearDown]
		public void TearDown ()
		{
			var server = stalledResponseServer;
			stalledResponseServer = null;

			// NUnitLite used by the on-device tests does not support async TearDown methods.
			if (server != null)
				server.Stop ();
		}

		[Test]
		public async Task ResponseContentReadBodyReadCancellationIsPrompt ()
		{
			var server = stalledResponseServer ?? throw new InvalidOperationException ("The stalled response server was not initialized.");
			using var handler = new AndroidMessageHandler ();
			using var client = new HttpClient (handler);
			using var cts = new CancellationTokenSource ();
			using var request = new HttpRequestMessage (HttpMethod.Get, $"http://localhost:{server.Port}/");

			Task readTask = client.SendAsync (request, HttpCompletionOption.ResponseContentRead, cts.Token);

			await WaitForBodyReadToBlock (server.BodyStartedTask).ConfigureAwait (false);
			cts.Cancel ();
			await AssertCanceledPromptly (readTask, server.ReleaseResponseBody).ConfigureAwait (false);
		}

		[Test]
		public async Task ResponseHeadersReadBodyReadCancellationIsPrompt ()
		{
			var server = stalledResponseServer ?? throw new InvalidOperationException ("The stalled response server was not initialized.");
			using var handler = new AndroidMessageHandler ();
			using var client = new HttpClient (handler);
			using var request = new HttpRequestMessage (HttpMethod.Get, $"http://localhost:{server.Port}/");
			using var response = await client.SendAsync (request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait (false);
			using var readCts = new CancellationTokenSource ();

			Task readContentTask = response.Content.ReadAsByteArrayAsync (readCts.Token);

			await WaitForBodyReadToBlock (server.BodyStartedTask).ConfigureAwait (false);
			readCts.Cancel ();
			await AssertCanceledPromptly (readContentTask, server.ReleaseResponseBody).ConfigureAwait (false);
		}

		static int GetAvailablePort ()
		{
			using var tcpListener = new TcpListener (IPAddress.Loopback, 0);
			tcpListener.Start ();
			int port = ((IPEndPoint) tcpListener.LocalEndpoint).Port;
			tcpListener.Stop ();
			return port;
		}

		static async Task WaitForBodyReadToBlock (Task bodyStarted)
		{
			var completed = await Task.WhenAny (bodyStarted, Task.Delay (PromptCancellationTimeoutMilliseconds)).ConfigureAwait (false);
			if (completed != bodyStarted)
				Assert.Fail ($"The test server did not start sending a response body within {PromptCancellationTimeoutMilliseconds}ms.");

			await bodyStarted.ConfigureAwait (false);
			await Task.Delay (BodyReadBlockDelayMilliseconds).ConfigureAwait (false);
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

		sealed class StalledResponseServer
		{
			readonly HttpListener listener;
			readonly TaskCompletionSource<bool> bodyStarted = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
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

			public Task BodyStartedTask => bodyStarted.Task;

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
				try {
					var context = await listener.GetContextAsync ().ConfigureAwait (false);
					using var response = context.Response;
					response.StatusCode = 200;
					response.ContentLength64 = StalledResponseContentLength;
					await response.OutputStream.WriteAsync (InitialResponseChunk, 0, InitialResponseChunk.Length).ConfigureAwait (false);
					await response.OutputStream.FlushAsync ().ConfigureAwait (false);
					bodyStarted.TrySetResult (true);

					await releaseBody.Task.ConfigureAwait (false);
					await WriteRemainingResponseBody (response).ConfigureAwait (false);
				} catch (Exception ex) {
					if (!BodyStartedTask.IsCompleted) {
						bodyStarted.TrySetException (ex);
						return;
					}
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
					return;

				await serverTask.ConfigureAwait (false);
			}
		}
	}
}
