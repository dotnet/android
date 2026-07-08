using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Xamarin.Android.NetTests {
	sealed class LocalHttpServer : IDisposable
	{
		const string LoopbackHost = "127.0.0.1";

		readonly HttpListener listener;
		readonly Func<HttpListenerContext, Task> handler;
		readonly List<Exception> handlerExceptions = new List<Exception> ();
		readonly Task acceptLoop;
		bool disposed;

		LocalHttpServer (Func<HttpListenerContext, Task> handler)
		{
			this.handler = handler;
			Port = GetAvailablePort ();
			listener = new HttpListener ();
			listener.Prefixes.Add ($"http://{LoopbackHost}:{Port}/");
			listener.Start ();
			acceptLoop = Task.Run (AcceptLoop);
		}

		public int Port { get; }

		public Uri Uri {
			get { return new Uri ($"http://{LoopbackHost}:{Port}/"); }
		}

		public string Url {
			get { return Uri.ToString (); }
		}

		public static LocalHttpServer Start (Action<HttpListenerContext> handler)
		{
			return Start (context => {
				handler (context);
				return Task.CompletedTask;
			});
		}

		public static LocalHttpServer Start (Func<HttpListenerContext, Task> handler)
		{
			return new LocalHttpServer (handler);
		}

		public static int GetAvailablePort ()
		{
			using (var tcpListener = new TcpListener (IPAddress.Loopback, 0)) {
				tcpListener.Start ();
				int port = ((IPEndPoint) tcpListener.LocalEndpoint).Port;
				tcpListener.Stop ();
				return port;
			}
		}

		public Uri GetUri (string relativeUri)
		{
			return new Uri (Uri, relativeUri);
		}

		public static void DrainRequestBody (HttpListenerRequest request)
		{
			if (!request.HasEntityBody) {
				return;
			}

			byte[] buffer = new byte [4096];
			while (request.InputStream.Read (buffer, 0, buffer.Length) > 0) {
			}
		}

		public void AssertNoUnhandledExceptions ()
		{
			lock (handlerExceptions) {
				if (handlerExceptions.Count == 0) {
					return;
				}

				Assert.Fail ($"Local HTTP server handler failed: {handlerExceptions [0]}");
			}
		}

		public void Dispose ()
		{
			disposed = true;
			listener.Close ();
			try {
				acceptLoop.Wait (TimeSpan.FromSeconds (5));
			} catch (AggregateException ex) {
				if (!OnlyExpectedShutdownExceptions (ex)) {
					throw;
				}
			}
		}

		async Task AcceptLoop ()
		{
			while (!disposed) {
				HttpListenerContext context;
				try {
					context = await listener.GetContextAsync ().ConfigureAwait (false);
				} catch (ObjectDisposedException) {
					return;
				} catch (HttpListenerException) when (disposed) {
					return;
				}

				_ = Task.Run (() => HandleContext (context));
			}
		}

		async Task HandleContext (HttpListenerContext context)
		{
			try {
				await handler (context).ConfigureAwait (false);
			} catch (Exception ex) {
				lock (handlerExceptions) {
					handlerExceptions.Add (ex);
				}

				try {
					context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
				} catch (ObjectDisposedException) {
				} catch (InvalidOperationException) {
				} catch (HttpListenerException) {
				}
			} finally {
				try {
					context.Response.Close ();
				} catch (ObjectDisposedException) {
				} catch (InvalidOperationException) {
				} catch (HttpListenerException) {
				}
			}
		}

		static bool OnlyExpectedShutdownExceptions (AggregateException ex)
		{
			foreach (var inner in ex.Flatten ().InnerExceptions) {
				if (inner is ObjectDisposedException) {
					continue;
				}
				if (inner is HttpListenerException) {
					continue;
				}
				return false;
			}
			return true;
		}

		public static async Task WriteStringAsync (HttpListenerResponse response, string content, string contentType)
		{
			byte[] bytes = Encoding.UTF8.GetBytes (content);
			response.ContentType = contentType;
			response.ContentLength64 = bytes.Length;
			await response.OutputStream.WriteAsync (bytes, 0, bytes.Length).ConfigureAwait (false);
		}

		public static async Task WriteCompressedStringAsync (HttpListenerResponse response, string encoding, string content, string contentType)
		{
			byte[] bytes = Encoding.UTF8.GetBytes (content);
			byte[] compressedBytes;
			using (var compressedStream = new MemoryStream ()) {
				Stream compressor = CreateCompressionStream (compressedStream, encoding);
				using (compressor) {
					await compressor.WriteAsync (bytes, 0, bytes.Length).ConfigureAwait (false);
				}
				compressedBytes = compressedStream.ToArray ();
			}

			response.ContentType = contentType;
			response.AddHeader ("Content-Encoding", encoding);
			response.ContentLength64 = compressedBytes.Length;
			await response.OutputStream.WriteAsync (compressedBytes, 0, compressedBytes.Length).ConfigureAwait (false);
		}

		static Stream CreateCompressionStream (Stream stream, string encoding)
		{
			if (String.Compare (encoding, "gzip", StringComparison.OrdinalIgnoreCase) == 0) {
				return new GZipStream (stream, CompressionLevel.Fastest, leaveOpen: true);
			}
			if (String.Compare (encoding, "br", StringComparison.OrdinalIgnoreCase) == 0) {
				return new BrotliStream (stream, CompressionLevel.Fastest, leaveOpen: true);
			}

			throw new ArgumentOutOfRangeException (nameof (encoding), encoding, "Unsupported compression encoding.");
		}
	}

	sealed class LocalHttpsServer : IDisposable
	{
		readonly TcpListener listener;
		readonly RSA certificateKey;
		readonly X509Certificate2 certificate;
		readonly Func<Stream, Task> handler;
		readonly List<Exception> handlerExceptions = new List<Exception> ();
		readonly Task acceptLoop;
		bool disposed;

		LocalHttpsServer (Func<Stream, Task> handler, string certificateHost)
		{
			this.handler = handler;
			Port = LocalHttpServer.GetAvailablePort ();
			certificateKey = RSA.Create (keySizeInBits: 2048);
			certificate = CreateCertificate (certificateKey, certificateHost);
			listener = new TcpListener (IPAddress.Loopback, Port);
			listener.Start ();
			acceptLoop = Task.Run (AcceptLoop);
		}

		public int Port { get; }

		public Uri Uri {
			get { return new Uri ($"https://localhost:{Port}/"); }
		}

		public string Url {
			get { return Uri.ToString (); }
		}

		public static LocalHttpsServer Start (Func<Stream, Task> handler)
		{
			return Start (handler, "localhost");
		}

		public static LocalHttpsServer Start (Func<Stream, Task> handler, string certificateHost)
		{
			return new LocalHttpsServer (handler, certificateHost);
		}

		public void AssertNoUnhandledExceptions ()
		{
			lock (handlerExceptions) {
				if (handlerExceptions.Count == 0) {
					return;
				}

				Assert.Fail ($"Local HTTPS server handler failed: {handlerExceptions [0]}");
			}
		}

		public void Dispose ()
		{
			disposed = true;
			listener.Stop ();
			try {
				acceptLoop.Wait (TimeSpan.FromSeconds (5));
			} catch (AggregateException ex) {
				if (!OnlyExpectedShutdownExceptions (ex)) {
					throw;
				}
			}
			certificate.Dispose ();
			certificateKey.Dispose ();
		}

		async Task AcceptLoop ()
		{
			while (!disposed) {
				TcpClient client;
				try {
					client = await listener.AcceptTcpClientAsync ().ConfigureAwait (false);
				} catch (ObjectDisposedException) {
					return;
				} catch (SocketException) when (disposed) {
					return;
				}

				_ = Task.Run (() => HandleClient (client));
			}
		}

		async Task HandleClient (TcpClient client)
		{
			using (client) {
				bool handlerCompleted = false;
				try {
					using (var sslStream = new SslStream (client.GetStream (), leaveInnerStreamOpen: false)) {
						await sslStream.AuthenticateAsServerAsync (certificate, clientCertificateRequired: false, enabledSslProtocols: SslProtocols.None, checkCertificateRevocation: false).ConfigureAwait (false);
						await ReadRequestHeadersAsync (sslStream).ConfigureAwait (false);
						await handler (sslStream).ConfigureAwait (false);
						handlerCompleted = true;
					}
				} catch (IOException) when (handlerCompleted) {
				} catch (ObjectDisposedException) when (handlerCompleted) {
				} catch (Exception ex) {
					lock (handlerExceptions) {
						handlerExceptions.Add (ex);
					}
				}
			}
		}

		static async Task ReadRequestHeadersAsync (Stream stream)
		{
			byte[] endOfHeaders = Encoding.ASCII.GetBytes ("\r\n\r\n");
			byte[] buffer = new byte [1];
			int matched = 0;
			int totalBytes = 0;

			while (totalBytes < 64 * 1024) {
				int bytesRead = await stream.ReadAsync (buffer, 0, buffer.Length).ConfigureAwait (false);
				if (bytesRead == 0) {
					return;
				}

				totalBytes += bytesRead;
				if (buffer [0] == endOfHeaders [matched]) {
					matched++;
					if (matched == endOfHeaders.Length) {
						return;
					}
				} else {
					matched = buffer [0] == endOfHeaders [0] ? 1 : 0;
				}
			}
		}

		public static async Task WriteResponseAsync (Stream stream, HttpStatusCode statusCode, string content)
		{
			await WriteResponseAsync (stream, statusCode, content, null).ConfigureAwait (false);
		}

		public static async Task WriteResponseAsync (Stream stream, HttpStatusCode statusCode, string content, string location)
		{
			byte[] body = Encoding.UTF8.GetBytes (content);
			var headers = new StringBuilder ();
			headers.Append ("HTTP/1.1 ").Append ((int) statusCode).Append (' ').Append (GetReasonPhrase (statusCode)).Append ("\r\n");
			if (location != null) {
				headers.Append ("Location: ").Append (location).Append ("\r\n");
			}
			headers.Append ("Content-Length: ").Append (body.Length).Append ("\r\n");
			headers.Append ("Connection: close\r\n\r\n");

			byte[] headerBytes = Encoding.ASCII.GetBytes (headers.ToString ());
			await stream.WriteAsync (headerBytes, 0, headerBytes.Length).ConfigureAwait (false);
			await stream.WriteAsync (body, 0, body.Length).ConfigureAwait (false);
		}

		static string GetReasonPhrase (HttpStatusCode statusCode)
		{
			switch ((int) statusCode) {
				case 200:
					return "OK";
				case 302:
					return "Found";
				case 308:
					return "Permanent Redirect";
				default:
					return "OK";
			}
		}

		static X509Certificate2 CreateCertificate (RSA key, string certificateHost)
		{
			DateTimeOffset start = DateTimeOffset.UtcNow.AddDays (-30);
			DateTimeOffset end = start.AddMonths (3);

			var request = new CertificateRequest ($"CN={certificateHost}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			request.CertificateExtensions.Add (new X509BasicConstraintsExtension (certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: false));
			request.CertificateExtensions.Add (new X509KeyUsageExtension (X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: false));
			request.CertificateExtensions.Add (new X509EnhancedKeyUsageExtension (new OidCollection { new Oid ("1.3.6.1.5.5.7.3.1", null) }, critical: false));

			var subjectAlternativeNames = new SubjectAlternativeNameBuilder ();
			if (IPAddress.TryParse (certificateHost, out IPAddress address)) {
				subjectAlternativeNames.AddIpAddress (address);
			} else {
				subjectAlternativeNames.AddDnsName (certificateHost);
			}
			request.CertificateExtensions.Add (subjectAlternativeNames.Build ());

			return request.CreateSelfSigned (start, end);
		}

		static bool OnlyExpectedShutdownExceptions (AggregateException ex)
		{
			foreach (var inner in ex.Flatten ().InnerExceptions) {
				if (inner is ObjectDisposedException) {
					continue;
				}
				if (inner is SocketException) {
					continue;
				}
				return false;
			}
			return true;
		}
	}
}
