using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if !NETSTANDARD2_0
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
#endif

namespace Xamarin.Android.NetTests {
	abstract class LocalTestServer : IDisposable
	{
		protected const string LoopbackHost = "127.0.0.1";

		readonly string name;
		readonly List<Exception> handlerExceptions = new List<Exception> ();

		protected LocalTestServer (string name)
		{
			this.name = name;
		}

		public abstract void Dispose ();

		public void AssertNoUnhandledExceptions ()
		{
			Exception exception;
			lock (handlerExceptions) {
				if (handlerExceptions.Count == 0) {
					return;
				}

				exception = handlerExceptions [0];
			}

			throw new InvalidOperationException ($"{name} handler failed.", exception);
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
	}

	class LocalHttpServer : LocalTestServer
	{
		readonly TcpListener listener;
		Task acceptLoop = Task.CompletedTask;
		bool disposed;

		protected LocalHttpServer (string name)
			: base (name)
		{
			listener = new TcpListener (IPAddress.Loopback, 0);
		}

		public int Port { get; private set; }

		public Uri Uri {
			get { return new Uri ($"{Scheme}://{Host}:{Port}/"); }
		}

		public string Url {
			get { return Uri.ToString (); }
		}

		public Uri OkUri {
			get { return GetUri ("ok"); }
		}

		protected virtual string Scheme {
			get { return "http"; }
		}

		protected virtual string Host {
			get { return LoopbackHost; }
		}

		public static LocalHttpServer Start ()
		{
			var server = new LocalHttpServer ("Local HTTP server");
			server.StartListening ();
			return server;
		}

		public Uri GetUri (string relativeUri)
		{
			return new Uri (Uri, relativeUri);
		}

		public override void Dispose ()
		{
			disposed = true;
			listener.Stop ();
			WaitForShutdown (acceptLoop, inner => inner is ObjectDisposedException || inner is SocketException);
		}

		protected void StartListening ()
		{
			listener.Start ();
			Port = ((IPEndPoint) listener.LocalEndpoint).Port;
			acceptLoop = Task.Run (AcceptLoop);
		}

		protected virtual Task<Stream> GetRequestStream (TcpClient client)
		{
			return Task.FromResult<Stream> (client.GetStream ());
		}

		protected virtual bool IgnoreHandlerException (Exception ex, bool handlerCompleted)
		{
			return handlerCompleted && (ex is IOException || ex is ObjectDisposedException);
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
					using (Stream stream = await GetRequestStream (client).ConfigureAwait (false)) {
						LocalHttpRequest request = await ReadRequestAsync (stream).ConfigureAwait (false);
						await HandleRequest (stream, request).ConfigureAwait (false);
						handlerCompleted = true;
					}
				} catch (Exception ex) {
					if (!IgnoreHandlerException (ex, handlerCompleted)) {
						AddHandlerException (ex);
					}
				}
			}
		}

		static async Task<LocalHttpRequest> ReadRequestAsync (Stream stream)
		{
			byte[] endOfHeaders = Encoding.ASCII.GetBytes ("\r\n\r\n");
			byte[] buffer = new byte [1];
			int matched = 0;

			using var headersStream = new MemoryStream ();
			while (headersStream.Length < 64 * 1024) {
				int read = await stream.ReadAsync (buffer, 0, buffer.Length).ConfigureAwait (false);
				if (read == 0) {
					break;
				}

				headersStream.WriteByte (buffer [0]);
				if (buffer [0] == endOfHeaders [matched]) {
					matched++;
					if (matched == endOfHeaders.Length) {
						break;
					}
				} else {
					matched = buffer [0] == endOfHeaders [0] ? 1 : 0;
				}
			}

			string headers = Encoding.ASCII.GetString (headersStream.ToArray ());
			string method = "";
			string target = "/";
			string[] lines = headers.Split (new [] { "\r\n" }, StringSplitOptions.None);
			if (lines.Length > 0) {
				string[] parts = lines [0].Split (new [] { ' ' }, 3);
				if (parts.Length > 0) {
					method = parts [0];
				}
				if (parts.Length > 1) {
					target = parts [1];
				}
			}

			int contentLength = GetContentLength (lines);
			byte[] body = new byte [contentLength];
			int offset = 0;
			while (offset < body.Length) {
				int read = await stream.ReadAsync (body, offset, body.Length - offset).ConfigureAwait (false);
				if (read == 0) {
					break;
				}
				offset += read;
			}

			return new LocalHttpRequest (method, target, Encoding.UTF8.GetString (body, 0, offset));
		}

		static int GetContentLength (string[] headers)
		{
			foreach (string line in headers) {
				if (line.StartsWith ("Content-Length:", StringComparison.OrdinalIgnoreCase) &&
						Int32.TryParse (line.Substring ("Content-Length:".Length).Trim (), out int contentLength)) {
					return contentLength;
				}
			}
			return 0;
		}

		protected virtual Task HandleRequest (Stream stream, LocalHttpRequest request)
		{
			switch (request.Path) {
#if !NETSTANDARD2_0
				case "/brotli":
					return WriteCompressedStringAsync (stream, "br", "{ \"brotli\": true }", "application/json");
#endif
				case "/gzip":
					return WriteCompressedStringAsync (stream, "gzip", "{ \"gzipped\": true }", "application/json");
				case "/ok":
					return WriteStringAsync (stream, "OK", "text/plain");
				case "/post":
					return HandlePost (stream, request);
				case "/redirect-to":
					request.Query.TryGetValue ("url", out string location);
					return WriteResponseAsync (stream, (HttpStatusCode) GetStatusCode (request), "", "text/plain", null, location);
				default:
					return WriteResponseAsync (stream, HttpStatusCode.NotFound, "", "text/plain", null, null);
			}
		}

		static Task HandlePost (Stream stream, LocalHttpRequest request)
		{
			if (request.Method != "POST" || request.Body.IndexOf ("\"foo\": \"bar\"", StringComparison.Ordinal) < 0) {
				return WriteResponseAsync (stream, HttpStatusCode.BadRequest, "{\"ok\": false}", "application/json", null, null);
			}

			return WriteStringAsync (stream, "{\"ok\": true}", "application/json");
		}

		static int GetStatusCode (LocalHttpRequest request)
		{
			request.Query.TryGetValue ("status_code", out string statusCode);
			if (Int32.TryParse (statusCode, out int code)) {
				return code;
			}
			return (int) HttpStatusCode.Redirect;
		}

		public static Task WriteStringAsync (Stream stream, string content, string contentType)
		{
			byte[] bytes = Encoding.UTF8.GetBytes (content);
			return WriteResponseAsync (stream, HttpStatusCode.OK, bytes, contentType, null, null);
		}

		public static async Task WriteCompressedStringAsync (Stream stream, string encoding, string content, string contentType)
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

			await WriteResponseAsync (stream, HttpStatusCode.OK, compressedBytes, contentType, encoding, null).ConfigureAwait (false);
		}

		static Stream CreateCompressionStream (Stream stream, string encoding)
		{
			if (String.Compare (encoding, "gzip", StringComparison.OrdinalIgnoreCase) == 0) {
				return new GZipStream (stream, CompressionLevel.Fastest, leaveOpen: true);
			}
#if !NETSTANDARD2_0
			if (String.Compare (encoding, "br", StringComparison.OrdinalIgnoreCase) == 0) {
				return new BrotliStream (stream, CompressionLevel.Fastest, leaveOpen: true);
			}
#endif

			throw new ArgumentOutOfRangeException (nameof (encoding), encoding, "Unsupported compression encoding.");
		}

		static Task WriteResponseAsync (Stream stream, HttpStatusCode statusCode, string content, string contentType, string contentEncoding, string location)
		{
			return WriteResponseAsync (stream, statusCode, Encoding.UTF8.GetBytes (content), contentType, contentEncoding, location);
		}

		static async Task WriteResponseAsync (Stream stream, HttpStatusCode statusCode, byte[] body, string contentType, string contentEncoding, string location)
		{
			var headers = new StringBuilder ();
			headers.Append ("HTTP/1.1 ").Append ((int) statusCode).Append (' ').Append (GetReasonPhrase (statusCode)).Append ("\r\n");
			headers.Append ("Content-Type: ").Append (contentType).Append ("\r\n");
			if (contentEncoding != null) {
				headers.Append ("Content-Encoding: ").Append (contentEncoding).Append ("\r\n");
			}
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
					return statusCode.ToString ();
			}
		}

		protected sealed class LocalHttpRequest
		{
			public LocalHttpRequest (string method, string target, string body)
			{
				Method = method;
				Body = body;

				int queryIndex = target.IndexOf ('?');
				if (queryIndex < 0) {
					Path = target;
					Query = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
				} else {
					Path = target.Substring (0, queryIndex);
					Query = ParseQuery (target.Substring (queryIndex + 1));
				}
			}

			public string Method { get; }
			public string Path { get; }
			public string Body { get; }
			public Dictionary<string, string> Query { get; }

			static Dictionary<string, string> ParseQuery (string query)
			{
				var ret = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
				foreach (string parameter in query.Split ('&')) {
					if (parameter.Length == 0) {
						continue;
					}

					int separator = parameter.IndexOf ('=');
					if (separator < 0) {
						ret [Unescape (parameter)] = "";
					} else {
						ret [Unescape (parameter.Substring (0, separator))] = Unescape (parameter.Substring (separator + 1));
					}
				}
				return ret;
			}

			static string Unescape (string value)
			{
				return Uri.UnescapeDataString (value.Replace ("+", " "));
			}
		}
	}

#if !NETSTANDARD2_0
	sealed class LocalHttpsServer : LocalHttpServer
	{
		readonly RSA certificateKey;
		readonly X509Certificate2 certificate;
		readonly bool clientCertificateRequired;

		LocalHttpsServer (string certificateHost, bool clientCertificateRequired)
			: base ("Local HTTPS server")
		{
			certificateKey = RSA.Create (keySizeInBits: 2048);
			certificate = CreateCertificate (certificateKey, certificateHost);
			this.clientCertificateRequired = clientCertificateRequired;
		}

		public byte [] CertificateData {
			get { return certificate.RawData; }
		}

		protected override string Scheme {
			get { return "https"; }
		}

		protected override string Host {
			get { return "localhost"; }
		}

		public static LocalHttpsServer Start (bool clientCertificateRequired = false)
		{
			return Start ("localhost", clientCertificateRequired);
		}

		public static LocalHttpsServer Start (string certificateHost, bool clientCertificateRequired = false)
		{
			var server = new LocalHttpsServer (certificateHost, clientCertificateRequired);
			server.StartListening ();
			return server;
		}

		public override void Dispose ()
		{
			base.Dispose ();
			certificate.Dispose ();
			certificateKey.Dispose ();
		}

		protected override async Task<Stream> GetRequestStream (TcpClient client)
		{
			var sslStream = new SslStream (client.GetStream (), leaveInnerStreamOpen: false, userCertificateValidationCallback: (sender, clientCertificate, chain, sslPolicyErrors) => true);
			await sslStream.AuthenticateAsServerAsync (certificate, clientCertificateRequired: clientCertificateRequired, enabledSslProtocols: SslProtocols.None, checkCertificateRevocation: false).ConfigureAwait (false);
			return sslStream;
		}

		protected override Task HandleRequest (Stream stream, LocalHttpRequest request)
		{
			if (request.Path == "/echo-client-certificate") {
				return WriteClientCertificateAsync (stream);
			}

			return base.HandleRequest (stream, request);
		}

		static Task WriteClientCertificateAsync (Stream stream)
		{
			string clientCertificateData = "";
			if (stream is SslStream sslStream && sslStream.RemoteCertificate != null) {
				clientCertificateData = Convert.ToBase64String (sslStream.RemoteCertificate.Export (X509ContentType.Cert));
			}

			return WriteStringAsync (stream, clientCertificateData, "text/plain");
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

	sealed class LocalWebSocketServer : LocalTestServer
	{
		const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

		readonly TcpListener listener;
		Task acceptLoop = Task.CompletedTask;
		bool disposed;

		LocalWebSocketServer ()
			: base ("Local WebSocket server")
		{
			listener = new TcpListener (IPAddress.Loopback, 0);
		}

		public int Port { get; private set; }

		public Uri Uri {
			get { return new Uri ($"ws://{LoopbackHost}:{Port}/"); }
		}

		public string Url {
			get { return Uri.ToString (); }
		}

		public static LocalWebSocketServer Start ()
		{
			var server = new LocalWebSocketServer ();
			server.StartListening ();
			return server;
		}

		public override void Dispose ()
		{
			disposed = true;
			listener.Stop ();
			WaitForShutdown (acceptLoop, inner => inner is ObjectDisposedException || inner is SocketException);
		}

		void StartListening ()
		{
			listener.Start ();
			Port = ((IPEndPoint) listener.LocalEndpoint).Port;
			acceptLoop = Task.Run (AcceptLoop);
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
				bool handshakeCompleted = false;
				try {
					Stream stream = client.GetStream ();
					string key = await ReadWebSocketKeyAsync (stream).ConfigureAwait (false);
					if (key == null) {
						return;
					}

					await WriteHandshakeResponseAsync (stream, key).ConfigureAwait (false);
					handshakeCompleted = true;

					using var webSocket = WebSocket.CreateFromStream (stream, isServer: true, subProtocol: null, keepAliveInterval: TimeSpan.FromSeconds (30));
					await EchoLoop (webSocket).ConfigureAwait (false);
				} catch (Exception ex) {
					if (!(handshakeCompleted && (ex is IOException || ex is ObjectDisposedException || ex is WebSocketException))) {
						AddHandlerException (ex);
					}
				}
			}
		}

		static async Task<string> ReadWebSocketKeyAsync (Stream stream)
		{
			byte[] endOfHeaders = Encoding.ASCII.GetBytes ("\r\n\r\n");
			byte[] buffer = new byte [1];
			int matched = 0;

			using var headersStream = new MemoryStream ();
			while (headersStream.Length < 64 * 1024) {
				int read = await stream.ReadAsync (buffer, 0, buffer.Length).ConfigureAwait (false);
				if (read == 0) {
					break;
				}

				headersStream.WriteByte (buffer [0]);
				if (buffer [0] == endOfHeaders [matched]) {
					matched++;
					if (matched == endOfHeaders.Length) {
						break;
					}
				} else {
					matched = buffer [0] == endOfHeaders [0] ? 1 : 0;
				}
			}

			string headers = Encoding.ASCII.GetString (headersStream.ToArray ());
			foreach (string line in headers.Split (new [] { "\r\n" }, StringSplitOptions.None)) {
				if (line.StartsWith ("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)) {
					return line.Substring ("Sec-WebSocket-Key:".Length).Trim ();
				}
			}

			return null;
		}

		static Task WriteHandshakeResponseAsync (Stream stream, string key)
		{
			string accept;
#pragma warning disable CA5350 // SHA-1 is mandated by the WebSocket handshake (RFC 6455 §1.3)
			using (var sha1 = SHA1.Create ()) {
				byte[] hash = sha1.ComputeHash (Encoding.ASCII.GetBytes (key + WebSocketGuid));
				accept = Convert.ToBase64String (hash);
			}
#pragma warning restore CA5350

			var response = new StringBuilder ();
			response.Append ("HTTP/1.1 101 Switching Protocols\r\n");
			response.Append ("Upgrade: websocket\r\n");
			response.Append ("Connection: Upgrade\r\n");
			response.Append ("Sec-WebSocket-Accept: ").Append (accept).Append ("\r\n\r\n");

			byte[] bytes = Encoding.ASCII.GetBytes (response.ToString ());
			return stream.WriteAsync (bytes, 0, bytes.Length);
		}

		static async Task EchoLoop (WebSocket webSocket)
		{
			byte[] buffer = new byte [4096];
			while (webSocket.State == WebSocketState.Open) {
				WebSocketReceiveResult result;
				try {
					result = await webSocket.ReceiveAsync (new ArraySegment<byte> (buffer), CancellationToken.None).ConfigureAwait (false);
				} catch (Exception ex) when (ex is WebSocketException || ex is IOException || ex is ObjectDisposedException) {
					// The client closed the connection without a WebSocket close handshake.
					return;
				}

				if (result.MessageType == WebSocketMessageType.Close) {
					await webSocket.CloseAsync (WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait (false);
					return;
				}

				await webSocket.SendAsync (new ArraySegment<byte> (buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None).ConfigureAwait (false);
			}
		}
	}
#endif
}
