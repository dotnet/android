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

namespace Xamarin.Android.NetTests;

[TestFixture]
[Category ("AndroidMessageHandlerUpload")]
[Category ("AndroidMessageHandlerExceptions")]
public class AndroidMessageHandlerUploadTests
{
	[TestCase (true, false, false)]
	[TestCase (false, false, false)]
	[TestCase (true, true, false)]
	[TestCase (false, true, false)]
	[TestCase (true, false, true)]
	[TestCase (true, true, true)]
	public async Task ContentFailurePreservesOriginalException (bool ioFailure, bool streamAcquisitionFailure, bool cancelRequest)
	{
		var listener = new TcpListener (IPAddress.Loopback, 0);
		listener.Start ();
		using var serverCts = new CancellationTokenSource (TimeSpan.FromSeconds (15));
		using var requestCts = new CancellationTokenSource ();
		using var handler = new AndroidMessageHandler { UseProxy = false };
		using var client = new HttpClient (handler) { Timeout = TimeSpan.FromSeconds (15) };
		Exception failure = ioFailure ? new IOException ("Content source failed.") : new InvalidOperationException ("Content source failed.");
		using var body = new FaultingCopyStream (failure, OnFailure);
		using var request = new HttpRequestMessage (HttpMethod.Put,
			$"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}/upload") {
			Content = streamAcquisitionFailure ? new FaultingReadContent (failure, OnFailure) : new StreamContent (body),
		};
		var connectionTask = listener.AcceptTcpClientAsync (serverCts.Token).AsTask ();
		try {
			var ex = Assert.CatchAsync (async () => await client.SendAsync (request, requestCts.Token));
			await connectionTask;
			if (cancelRequest) {
				Assert.IsTrue (requestCts.IsCancellationRequested);
				Assert.IsInstanceOf<OperationCanceledException> (ex, "Cancellation must take precedence over I/O translation.");
			} else if (ioFailure) {
				Assert.IsInstanceOf<HttpRequestException> (ex);
				var compatibilityException = ex?.InnerException as WebException;
				Assert.IsNotNull (compatibilityException);
				Assert.AreEqual (WebExceptionStatus.UnknownError, compatibilityException?.Status);
				Assert.AreSame (failure, compatibilityException?.InnerException);
			} else {
				Assert.AreSame (failure, ex, "Unrelated application exceptions must not be translated.");
			}
			if (!streamAcquisitionFailure)
				Assert.AreEqual (0, body.Position, "Acquired seekable content must be rewound after failure.");
		} finally {
			serverCts.Cancel ();
			listener.Stop ();
			using var connection = await connectionTask;
		}

		void OnFailure ()
		{
			if (cancelRequest)
				requestCts.Cancel ();
		}
	}

	sealed class FaultingReadContent : HttpContent
	{
		readonly Exception failure;
		readonly Action onFailure;

		public FaultingReadContent (Exception failure, Action onFailure)
		{
			this.failure = failure;
			this.onFailure = onFailure;
		}

		protected override Task<Stream> CreateContentReadStreamAsync ()
		{
			onFailure ();
			return Task.FromException<Stream> (failure);
		}

		protected override Task SerializeToStreamAsync (Stream stream, TransportContext? context)
			=> Task.FromException (new InvalidOperationException ("The test content must use CreateContentReadStreamAsync."));

		protected override bool TryComputeLength (out long length)
		{
			length = 1024;
			return true;
		}
	}

	sealed class FaultingCopyStream : MemoryStream
	{
		readonly Exception failure;
		readonly Action onFailure;

		public FaultingCopyStream (Exception failure, Action onFailure)
			: base (new byte [1024])
		{
			this.failure = failure;
			this.onFailure = onFailure;
		}

		public override Task CopyToAsync (Stream destination, int bufferSize, CancellationToken cancellationToken)
		{
			Position = Length;
			onFailure ();
			return Task.FromException (failure);
		}
	}

	[TestCase (1, false, true, false)]
	[TestCase (2, false, true, false)]
	[TestCase (1, false, true, true)]
	[TestCase (1, false, false, false)]
	[TestCase (0, false, true, false)]
	[TestCase (0, false, false, false)]
	[TestCase (0, false, true, true)]
	[TestCase (1, true, true, false)]
	[TestCase (2, true, true, false)]
	[TestCase (1, true, true, true)]
	[TestCase (1, true, false, false)]
	[TestCase (0, true, true, false)]
	[TestCase (0, true, false, false)]
	[TestCase (0, true, true, true)]
	public async Task RewindFailurePreservesOriginalException (int uploadFailureKind, bool canSeekFailure, bool rewindIoFailure, bool cancelRequest)
	{
		var listener = new TcpListener (IPAddress.Loopback, 0);
		listener.Start ();
		using var serverCts = new CancellationTokenSource (TimeSpan.FromSeconds (15));
		using var requestCts = new CancellationTokenSource ();
		using var handler = new AndroidMessageHandler { UseProxy = false };
		using var client = new HttpClient (handler) { Timeout = TimeSpan.FromSeconds (15) };
		Exception? uploadFailure = uploadFailureKind switch {
			1 => new IOException ("Upload failed."),
			2 => new InvalidOperationException ("Upload failed."),
			_ => null,
		};
		Exception rewindFailure = rewindIoFailure ? new IOException ("Rewind failed.") : new InvalidOperationException ("Rewind failed.");
		using var body = new FaultingRewindStream (uploadFailure, rewindFailure, canSeekFailure, requestCts, cancelRequest);
		using var request = new HttpRequestMessage (HttpMethod.Put,
			$"http://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}/upload") {
			Content = new StreamReturningContent (body),
		};
		var connectionTask = listener.AcceptTcpClientAsync (serverCts.Token).AsTask ();
		try {
			var ex = Assert.CatchAsync (async () => await client.SendAsync (request, requestCts.Token));
			await connectionTask;
			Assert.IsTrue (body.RewindAttempted, "The test must exercise rewind after copying.");
			Exception expectedFailure = uploadFailure ?? rewindFailure;
			if (cancelRequest) {
				Assert.IsTrue (requestCts.IsCancellationRequested);
				Assert.IsInstanceOf<OperationCanceledException> (ex);
				Assert.AreSame (expectedFailure, ex?.GetBaseException (), "Rewind must not replace the cancellation cause.");
			} else if (expectedFailure is IOException) {
				Assert.IsInstanceOf<HttpRequestException> (ex);
				var compatibilityException = ex?.InnerException as WebException;
				Assert.IsNotNull (compatibilityException);
				Assert.AreEqual (WebExceptionStatus.UnknownError, compatibilityException?.Status);
				Assert.AreSame (expectedFailure, compatibilityException?.InnerException);
			} else {
				Assert.AreSame (expectedFailure, ex, "Application exceptions must retain their identity.");
			}
		} finally {
			serverCts.Cancel ();
			listener.Stop ();
			using var connection = await connectionTask;
		}
	}

	sealed class StreamReturningContent (Stream source) : HttpContent
	{
		protected override Task<Stream> CreateContentReadStreamAsync ()
			=> Task.FromResult (source);

		protected override Task SerializeToStreamAsync (Stream stream, TransportContext? context)
			=> source.CopyToAsync (stream);

		protected override bool TryComputeLength (out long length)
		{
			length = source.Length;
			return true;
		}
	}

	sealed class FaultingRewindStream (
		Exception? uploadFailure,
		Exception rewindFailure,
		bool canSeekFailure,
		CancellationTokenSource requestCts,
		bool cancelRequest) : MemoryStream (new byte [1024])
	{
		bool copyFinished;
		public bool RewindAttempted { get; private set; }

		public override bool CanSeek {
			get {
				if (copyFinished && canSeekFailure)
					ThrowRewindFailure ();
				return base.CanSeek;
			}
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			if (copyFinished)
				ThrowRewindFailure ();
			return base.Seek (offset, origin);
		}

		public override async Task CopyToAsync (Stream destination, int bufferSize, CancellationToken cancellationToken)
		{
			if (uploadFailure != null) {
				copyFinished = true;
				if (cancelRequest)
					requestCts.Cancel ();
				throw uploadFailure;
			}
			await base.CopyToAsync (destination, bufferSize, cancellationToken);
			copyFinished = true;
		}

		void ThrowRewindFailure ()
		{
			RewindAttempted = true;
			if (cancelRequest)
				requestCts.Cancel ();
			throw rewindFailure;
		}
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
		using var body = new BoundedCopyMemoryStream (new byte [16 * 1024 * 1024]);
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

	sealed class BoundedCopyMemoryStream (byte[] contents) : MemoryStream (contents)
	{
		public override async Task CopyToAsync (Stream destination, int bufferSize, CancellationToken cancellationToken)
		{
			// MemoryStream copies its entire backing array in one write, which exceeds small Java heaps.
			byte[] buffer = new byte [bufferSize];
			int count;
			while ((count = await ReadAsync (buffer, 0, buffer.Length, cancellationToken)) != 0)
				await destination.WriteAsync (buffer, 0, count, cancellationToken);
		}
	}
}
