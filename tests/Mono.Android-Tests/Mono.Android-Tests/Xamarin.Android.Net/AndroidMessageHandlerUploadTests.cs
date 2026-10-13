#nullable enable

#pragma warning disable CS0618 // These tests intentionally exercise the obsolete AndroidMessageHandler.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Xamarin.Android.Net;

namespace Xamarin.Android.NetTests
{
	[TestFixture]
	[Category ("AndroidMessageHandlerUpload")]
	[Category ("AndroidMessageHandlerExceptions")]
	public class AndroidMessageHandlerUploadTests
	{
		[TestCase (true)]
		[TestCase (false)]
		public async Task ContentCopyFailurePreservesOriginalException (bool ioFailure)
		{
			var listener = new TcpListener (IPAddress.Loopback, 0);
			listener.Start ();
			using var serverCts = new CancellationTokenSource (TimeSpan.FromSeconds (15));
			using var handler = new AndroidMessageHandler { UseProxy = false };
			using var client = new HttpClient (handler) { Timeout = TimeSpan.FromSeconds (15) };
			Exception failure = ioFailure ? new IOException ("Content source failed.") : new InvalidOperationException ("Content source failed.");
			using var body = new FaultingCopyStream (failure);
			using var request = new HttpRequestMessage (HttpMethod.Put,
				$"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}/upload") {
				Content = new StreamContent (body),
			};
			var connectionTask = listener.AcceptTcpClientAsync (serverCts.Token).AsTask ();
			try {
				var ex = Assert.CatchAsync (async () => await client.SendAsync (request));
				await connectionTask;
				if (ioFailure) {
					Assert.IsInstanceOf<HttpRequestException> (ex);
					var compatibilityException = ex?.InnerException as WebException;
					Assert.IsNotNull (compatibilityException);
					Assert.AreSame (failure, compatibilityException?.InnerException);
				} else {
					Assert.AreSame (failure, ex, "Unrelated application exceptions must not be translated.");
				}
			} finally {
				serverCts.Cancel ();
				listener.Stop ();
				using var connection = await connectionTask;
			}
		}

		sealed class FaultingCopyStream : MemoryStream
		{
			readonly Exception failure;

			public FaultingCopyStream (Exception failure)
				: base (new byte [1024])
			{
				this.failure = failure;
			}

			public override Task CopyToAsync (Stream destination, int bufferSize, CancellationToken cancellationToken)
				=> Task.FromException (failure);
		}

		[TestCase (HttpCompletionOption.ResponseHeadersRead)]
		[TestCase (HttpCompletionOption.ResponseContentRead)]
		[Repeat (3)]
		public async Task UploadConnectionResetThrowsHttpRequestException (HttpCompletionOption completionOption)
		{
			var listener = new TcpListener (IPAddress.Loopback, 0);
			listener.Start ();
			using var serverCts = new CancellationTokenSource (TimeSpan.FromSeconds (15));
			using var requestCts = new CancellationTokenSource ();
			using var handler = new AndroidMessageHandler { UseProxy = false };
			using var client = new HttpClient (handler) { Timeout = TimeSpan.FromSeconds (30) };
			using var body = new MemoryStream (new byte [16 * 1024 * 1024]);
			using var request = new HttpRequestMessage (HttpMethod.Put,
				$"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}/upload") {
				Content = new StreamContent (body),
			};
			bool bodyReceived = false;
			Task serverTask = ResetUpload ();
			try {
				var ex = Assert.CatchAsync (async () =>
					await client.SendAsync (request, completionOption, requestCts.Token));
				await serverTask;
				Assert.IsTrue (bodyReceived, "The server must reset an upload already in progress.");
				Assert.IsFalse (requestCts.IsCancellationRequested, "The upload must fail without caller cancellation.");
				Assert.IsInstanceOf<HttpRequestException> (ex);
				var compatibilityException = ex?.InnerException as WebException;
				Assert.IsNotNull (compatibilityException);
				if (compatibilityException == null)
					throw new InvalidOperationException ("The compatibility exception was not preserved.");
				Assert.AreEqual (WebExceptionStatus.UnknownError, compatibilityException.Status);
				Assert.IsInstanceOf<IOException> (compatibilityException.InnerException);
				Assert.IsInstanceOf<Java.IO.IOException> (compatibilityException.InnerException?.InnerException);
				Assert.AreEqual (0, body.Position, "The content stream must be rewound after failure.");
			} finally {
				serverCts.Cancel ();
				listener.Stop ();
				await serverTask;
			}

			async Task ResetUpload ()
			{
				using var connection = await listener.AcceptTcpClientAsync (serverCts.Token);
				using var stream = connection.GetStream ();
				byte[] buffer = new byte [1];
				int headerEnd = 0;
				int headerBytes = 0;
				byte[] terminator = [13, 10, 13, 10];
				while (headerEnd < terminator.Length) {
					Assert.Less (++headerBytes, 64 * 1024, "Request headers exceeded the test limit.");
					Assert.AreEqual (1, await stream.ReadAsync (buffer, serverCts.Token), "Request ended before headers.");
					headerEnd = buffer [0] == terminator [headerEnd] ? headerEnd + 1 : (buffer [0] == 13 ? 1 : 0);
				}
				buffer = new byte [1024];
				await stream.ReadExactlyAsync (buffer, serverCts.Token);
				bodyReceived = true;
				connection.Client.LingerState = new LingerOption (true, 0);
			}
		}
	}
}
