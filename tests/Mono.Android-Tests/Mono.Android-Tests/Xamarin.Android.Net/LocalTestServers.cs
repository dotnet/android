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
	abstract class LocalTestServer : IDisposable
	{
		protected const string LoopbackHost = "127.0.0.1";

		readonly string name;
		readonly List<Exception> handlerExceptions = [];

		protected LocalTestServer (string name)
		{
			Port = GetAvailablePort ();
			this.name = name;
		}

		public int Port { get; }

		public abstract void Dispose ();

		public void AssertNoUnhandledExceptions ()
		{
			lock (handlerExceptions) {
				if (handlerExceptions.Count == 0) {
					return;
				}

				Assert.Fail ($"{name} handler failed: {handlerExceptions [0]}");
			}
		}

		protected void AddHandlerException (Exception ex)
		{
			lock (handlerExceptions) {
				handlerExceptions.Add (ex);
			}
		}

		protected static void WaitForShutdown (Task task, Func<Exception, bool> isExpectedException)
		{
			try {
				task.Wait (TimeSpan.FromSeconds (5));
			} catch (AggregateException ex) {
				if (!OnlyExpectedShutdownExceptions (ex, isExpectedException)) {
					throw;
				}
			}
		}

		static bool OnlyExpectedShutdownExceptions (AggregateException ex, Func<Exception, bool> isExpectedException)
		{
			foreach (var inner in ex.Flatten ().InnerExceptions) {
				if (!isExpectedException (inner)) {
					return false;
				}
			}
			return true;
		}

		static int GetAvailablePort ()
		{
			using (var tcpListener = new TcpListener (IPAddress.Loopback, 0)) {
				tcpListener.Start ();
				int port = ((IPEndPoint) tcpListener.LocalEndpoint).Port;
				tcpListener.Stop ();
				return port;
			}
		}
	}

	sealed class LocalHttpServer : LocalTestServer
	{
		readonly HttpListener listener;
		readonly Func<HttpListenerContext, Task> handler;
		readonly Task acceptLoop;
		bool disposed;

		LocalHttpServer (Func<HttpListenerContext, Task> handler)
			: base ("Local HTTP server")
		{
			this.handler = handler;
			listener = new HttpListener ();
			listener.Prefixes.Add ($"http://{LoopbackHost}:{Port}/");
			listener.Start ();
			acceptLoop = Task.Run (AcceptLoop);
		}

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

		public override void Dispose ()
		{
			disposed = true;
			listener.Close ();
			WaitForShutdown (acceptLoop, inner => inner is ObjectDisposedException || inner is HttpListenerException);
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
				AddHandlerException (ex);

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

	sealed class LocalHttpTestServer : IDisposable
	{
		readonly LocalHttpServer server;

		LocalHttpTestServer ()
		{
			server = LocalHttpServer.Start (HandleRequest);
		}

		public Uri OkUri {
			get { return GetUri ("ok"); }
		}

		public static LocalHttpTestServer Start ()
		{
			return new LocalHttpTestServer ();
		}

		public Uri GetUri (string relativeUri)
		{
			return server.GetUri (relativeUri);
		}

		public Uri GetRedirectUri (Uri location)
		{
			return GetRedirectUri (location, HttpStatusCode.Redirect);
		}

		public Uri GetRedirectUri (Uri location, HttpStatusCode statusCode)
		{
			return GetUri ($"redirect-to?url={Uri.EscapeDataString (location.ToString ())}&status_code={(int) statusCode}");
		}

		public void AssertNoUnhandledExceptions ()
		{
			server.AssertNoUnhandledExceptions ();
		}

		public void Dispose ()
		{
			server.Dispose ();
		}

		Task HandleRequest (HttpListenerContext context)
		{
			string path = context.Request.Url?.AbsolutePath ?? "";
			switch (path) {
				case "/brotli":
					return LocalHttpServer.WriteCompressedStringAsync (context.Response, "br", "{ \"brotli\": true }", "application/json");
				case "/gzip":
					return LocalHttpServer.WriteCompressedStringAsync (context.Response, "gzip", "{ \"gzipped\": true }", "application/json");
				case "/ok":
					return LocalHttpServer.WriteStringAsync (context.Response, "OK", "text/plain");
				case "/post":
					return HandlePost (context);
				case "/redirect-to":
					LocalHttpServer.DrainRequestBody (context.Request);
					context.Response.StatusCode = GetStatusCode (context);
					context.Response.RedirectLocation = context.Request.QueryString ["url"];
					return Task.CompletedTask;
				default:
					context.Response.StatusCode = (int) HttpStatusCode.NotFound;
					return Task.CompletedTask;
			}
		}

		static Task HandlePost (HttpListenerContext context)
		{
			using var reader = new StreamReader (context.Request.InputStream, context.Request.ContentEncoding);
			string body = reader.ReadToEnd ();
			if (context.Request.HttpMethod != "POST" || !body.Contains ("\"foo\": \"bar\"", StringComparison.Ordinal)) {
				context.Response.StatusCode = (int) HttpStatusCode.BadRequest;
				return LocalHttpServer.WriteStringAsync (context.Response, "{\"ok\": false}", "application/json");
			}

			return LocalHttpServer.WriteStringAsync (context.Response, "{\"ok\": true}", "application/json");
		}

		static int GetStatusCode (HttpListenerContext context)
		{
			string statusCode = context.Request.QueryString ["status_code"];
			if (Int32.TryParse (statusCode, out int code)) {
				return code;
			}
			return (int) HttpStatusCode.Redirect;
		}
	}

	sealed class LocalHttpsServer : LocalTestServer
	{
		readonly TcpListener listener;
		readonly RSA certificateKey;
		readonly X509Certificate2 certificate;
		readonly Func<Stream, Task> handler;
		readonly Task acceptLoop;
		bool disposed;

		LocalHttpsServer (Func<Stream, Task> handler, string certificateHost)
			: base ("Local HTTPS server")
		{
			this.handler = handler;
			certificateKey = RSA.Create (keySizeInBits: 2048);
			certificate = CreateCertificate (certificateKey, certificateHost);
			listener = new TcpListener (IPAddress.Loopback, Port);
			listener.Start ();
			acceptLoop = Task.Run (AcceptLoop);
		}

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

		public static LocalHttpsServer StartOk (string certificateHost = "localhost")
		{
			return Start (stream => WriteResponseAsync (stream, HttpStatusCode.OK, "OK"), certificateHost);
		}

		public static LocalHttpsServer StartRedirectTo (string location, HttpStatusCode statusCode = HttpStatusCode.Redirect)
		{
			return Start (stream => WriteResponseAsync (stream, statusCode, "", location));
		}

		public override void Dispose ()
		{
			disposed = true;
			listener.Stop ();
			WaitForShutdown (acceptLoop, inner => inner is ObjectDisposedException || inner is SocketException);
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
					AddHandlerException (ex);
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
	}
}
