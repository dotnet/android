using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Android.Runtime;
using Xamarin.Android.Net;

using NUnit.Framework;

namespace Xamarin.Android.NetTests
{
	[TestFixture]
	[Category ("SSL")] // TODO: https://github.com/dotnet/android/issues/10069
	public class AndroidMessageHandlerTests : AndroidHandlerTestBase
	{
		protected override HttpMessageHandler CreateHandler ()
		{
			return new AndroidMessageHandler ();
		}

		// We can't test `deflate` for now because S.I.Compression.DeflateStream doesn't recognize the compression
		// method previously used by the external test server.
		static readonly object[] DecompressionSource = new object[] {
			new object[] {
				"gzip", // urlPath
				"gzip", // encoding
				"gzipped", // jsonFieldName
			},

			new object[] {
				"brotli", // urlPath
				"br", // encoding
				"brotli", // jsonFieldName
			},
		};

		[Test]
		[TestCaseSource (nameof (DecompressionSource))]
		public async Task Decompression (string urlPath, string encoding, string jsonFieldName)
		{
			var handler = new AndroidMessageHandler {
				AutomaticDecompression = DecompressionMethods.All
			};

			using var server = LocalHttpServer.Start ();
			using var client = new HttpClient (handler);
			using HttpResponseMessage response = await client.GetAsync (server.GetUri (urlPath));
			EnsureSuccessStatusCode (response);

			foreach (string enc in response.Content.Headers.ContentEncoding) {
				if (String.Compare (enc, encoding, StringComparison.Ordinal) == 0) {
					Assert.Fail ($"Encoding '{encoding}' should have been removed from the Content-Encoding header");
				}
			}

			string responseBody = await response.Content.ReadAsStringAsync ();

			Console.WriteLine ("-- Retrieved JSON start");
			Console.WriteLine (responseBody);
			Console.WriteLine ("-- Retrieved JSON end");

			Assert.IsTrue (responseBody.Length > 0, "Response was empty");
			Assert.AreEqual (response.Content.Headers.ContentLength, responseBody.Length, "Retrieved data length is different than the one specified in the Content-Length header");
			Assert.IsTrue (responseBody.Contains ($"\"{jsonFieldName}\"", StringComparison.OrdinalIgnoreCase), $"\"{jsonFieldName}\" should have been in the response JSON");

			server.AssertNoUnhandledExceptions ();
		}

		static int GetAvailablePort ()
		{
			using var tcpListener = new TcpListener (IPAddress.Any, 0);
			tcpListener.Start ();
			int port = ((IPEndPoint) tcpListener.LocalEndpoint).Port;
			tcpListener.Stop ();
			return port;
		}

		[Test]
		public async Task DoesNotDisposeContentStream()
		{
			int port = GetAvailablePort ();
			using var listener = new HttpListener ();
			listener.Prefixes.Add ($"http://+:{port}/");
			listener.Start ();
			listener.BeginGetContext (ar => {
				var ctx = listener.EndGetContext (ar);
				ctx.Response.StatusCode = 204;
				ctx.Response.ContentLength64 = 0;
				ctx.Response.Close ();
			}, null);

			var jsonContent = new StringContent ("hello");
			var request = new HttpRequestMessage (HttpMethod.Post, $"http://localhost:{port}/") { Content = jsonContent };

			var response = await new HttpClient (new AndroidMessageHandler ()).SendAsync (request);
			Assert.True (response.IsSuccessStatusCode);

			var contentValue = await jsonContent.ReadAsStringAsync ();
			Assert.AreEqual ("hello", contentValue);

			listener.Close ();
		}

		[Test]
		public async Task ServerCertificateCustomValidationCallback_ApproveRequest ()
		{
			bool callbackHasBeenCalled = false;
			using var server = LocalHttpsServer.Start ();

			var handler = new AndroidMessageHandler {
				ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
					Assert.NotNull (request, "request");
					Assert.AreEqual ("localhost", request.RequestUri.Host);
					Assert.NotNull (cert, "cert");
					Assert.True (cert.Subject.Contains ("localhost"), $"Unexpected certificate subject {cert.Subject}");
					Assert.NotNull (chain, "chain");

					callbackHasBeenCalled = true;
					return true;
				}
			};

			var client = new HttpClient (handler);
			Assert.AreEqual ("OK", await client.GetStringAsync (server.OkUri));

			Assert.IsTrue (callbackHasBeenCalled, "custom validation callback hasn't been called");
			server.AssertNoUnhandledExceptions ();
		}

		[Test]
		public async Task ServerCertificateCustomValidationCallback_RejectRequest ()
		{
			bool callbackHasBeenCalled = false;
			using var server = LocalHttpsServer.Start ();

			var handler = new AndroidMessageHandler {
				ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
					callbackHasBeenCalled = true;
					return false;
				}
			};
			var client = new HttpClient (handler);

			await AssertRejectsRemoteCertificate (() => client.GetStringAsync (server.OkUri));

			Assert.IsTrue (callbackHasBeenCalled, "custom validation callback hasn't been called");
		}

		[Test]
		public async Task ServerCertificateCustomValidationCallback_ApprovesRequestWithInvalidCertificate ()
		{
			bool callbackHasBeenCalled = false;
			using var server = LocalHttpsServer.Start ();

			var handler = new AndroidMessageHandler {
				ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
					callbackHasBeenCalled = true;
					return true;
				}
			};

			var client = new HttpClient (handler);
			await client.GetStringAsync (server.OkUri);

			Assert.IsTrue (callbackHasBeenCalled, "custom validation callback hasn't been called");
			server.AssertNoUnhandledExceptions ();
		}

		[Test]
		public async Task NoServerCertificateCustomValidationCallback_ThrowsWhenThereIsCertificateHostnameMismatch ()
		{
			using var server = LocalHttpsServer.Start ();
			using var certificateStream = new MemoryStream (server.CertificateData);
			using var certificateFactory = Java.Security.Cert.CertificateFactory.GetInstance ("X.509")
				?? throw new InvalidOperationException ("Failed to create the X.509 certificate factory.");
			using var trustedCertificate = certificateFactory.GenerateCertificate (certificateStream)
				?? throw new InvalidOperationException ("Failed to load the local HTTPS server certificate.");
			var handler = new AndroidMessageHandler {
				TrustedCerts = new [] { trustedCertificate },
			};
			var client = new HttpClient (handler);

			Assert.AreEqual ("OK", await client.GetStringAsync (server.OkUri));

			Uri mismatchedUri = new UriBuilder (server.OkUri) {
				Host = "127.0.0.1",
			}.Uri;
			await AssertRejectsRemoteCertificate (() => client.GetStringAsync (mismatchedUri));
		}

		[Test]
		public async Task ServerCertificateCustomValidationCallback_IgnoresCertificateHostnameMismatch ()
		{
			bool callbackHasBeenCalled = false;
			SslPolicyErrors reportedErrors = SslPolicyErrors.None;
			using var server = LocalHttpsServer.Start (certificateHost: "wrong.host.test");

			var handler = new AndroidMessageHandler {
				ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
					callbackHasBeenCalled = true;
					reportedErrors = errors;
					return true;
				}
			};

			var client = new HttpClient (handler);
			await client.GetStringAsync (server.OkUri);

			Assert.IsTrue (callbackHasBeenCalled, "custom validation callback hasn't been called");
			Assert.AreEqual (SslPolicyErrors.RemoteCertificateNameMismatch, reportedErrors & SslPolicyErrors.RemoteCertificateNameMismatch);
			server.AssertNoUnhandledExceptions ();
		}

		[Test]
		public async Task ServerCertificateCustomValidationCallback_Redirects ()
		{
			int callbackCounter = 0;
			using var server = LocalHttpsServer.Start ();

			var handler = new AndroidMessageHandler {
				ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
					callbackCounter++;
					Assert.AreNotEqual (SslPolicyErrors.None, errors, "Local self-signed certificates should report policy errors.");
					return true;
				}
			};

			var client = new HttpClient (handler) {
				BaseAddress = server.Uri
			};
			using var result = await client.GetAsync ($"/redirect-to?url={Uri.EscapeDataString (server.OkUri.ToString ())}");
			EnsureSuccessStatusCode (result);
			Assert.AreEqual (2, callbackCounter);
			server.AssertNoUnhandledExceptions ();
		}

		[Test]
		public async Task AndroidMessageHandlerFollows308PermanentRedirect ()
		{
			using var server = LocalHttpServer.Start ();

			var handler = new AndroidMessageHandler ();

			var client = new HttpClient (handler) {
				BaseAddress = server.Uri
			};
			using var result = await client.GetAsync ($"/redirect-to?url={Uri.EscapeDataString (server.OkUri.ToString ())}&status_code=308");
			EnsureSuccessStatusCode (result);
			Assert.AreEqual (server.OkUri.ToString (), result.RequestMessage.RequestUri.ToString ());
			server.AssertNoUnhandledExceptions ();
		}

		[Test]
		public async Task AndroidMessageHandlerSendsClientCertificate ([Values(true, false)] bool setClientCertificateOptionsExplicitly)
		{
			using X509Certificate2 certificate = BuildClientCertificate ();
			using var server = LocalHttpsServer.Start (clientCertificateRequired: true);

			using var handler = new AndroidMessageHandler {
				ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => true,
			};
			if (setClientCertificateOptionsExplicitly) {
				handler.ClientCertificateOptions = ClientCertificateOption.Manual;
			}
			handler.ClientCertificates.Add (certificate);

			using var client = new HttpClient (handler);
			var response = await client.GetAsync (server.GetUri ("echo-client-certificate"));
			var content = await response.EnsureSuccessStatusCode ().Content.ReadAsStringAsync ();

			X509Certificate2 certificate2 = new X509Certificate2 (global::System.Convert.FromBase64String (content));
			Assert.AreEqual (certificate.Thumbprint, certificate2.Thumbprint);
			server.AssertNoUnhandledExceptions ();
		}

		[Test]
		public async Task AndroidMessageHandlerRejectsClientCertificateOptionsAutomatic ()
		{
			var handler = new AndroidMessageHandler
			{
				ClientCertificateOptions = ClientCertificateOption.Automatic,
			};

			Assert.Throws<InvalidOperationException>(() => handler.ClientCertificates.Add (BuildClientCertificate ()));
		}

		private async Task AssertRejectsRemoteCertificate (Func<Task> makeRequest)
		{
			// there is a difference between the exception that's thrown in the .NET build and the legacy Xamarin
			// because there's a difference in the $(AndroidBoundExceptionType) property value (legacy: Java, .NET: System)
			try {
				await makeRequest();
				Assert.Fail ("The request wasn't rejected");
			}
			catch (System.Net.Http.HttpRequestException) {}
		}

		// Adapted from https://github.com/dotnet/runtime/blob/e8b89a3fde2911c6cbac0488bf82c74329a7224a/src/libraries/Common/tests/System/Security/Cryptography/X509Certificates/CertificateAuthority.cs#L797
		private static X509Certificate2 BuildClientCertificate ()
		{
			DateTimeOffset start = DateTimeOffset.UtcNow;
			DateTimeOffset end = start.AddMonths (3);

			using RSA rootKey = RSA.Create (keySizeInBits: 2048);
			using RSA clientKey = RSA.Create (keySizeInBits: 2048);

			var rootReq = new CertificateRequest ("CN=Test Root, O=Test Root Organization", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			rootReq.CertificateExtensions.Add (new X509BasicConstraintsExtension (certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
			rootReq.CertificateExtensions.Add (new X509SubjectKeyIdentifierExtension (rootReq.PublicKey, critical: false));
			X509Certificate2 rootCert = rootReq.CreateSelfSigned (start.AddDays (-2), end.AddDays (2));

			var clientReq = new CertificateRequest ("CN=Test End Entity, O=Test End Entity Organization", clientKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			clientReq.CertificateExtensions.Add (new X509BasicConstraintsExtension (certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: false));
			clientReq.CertificateExtensions.Add (new X509KeyUsageExtension (X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment, critical: false));
			clientReq.CertificateExtensions.Add (new X509EnhancedKeyUsageExtension (enhancedKeyUsages: new OidCollection { new Oid ("1.3.6.1.5.5.7.3.2", null) }, critical: false)); // TLS client EKU
			clientReq.CertificateExtensions.Add (new X509SubjectKeyIdentifierExtension (clientReq.PublicKey, critical: false));

			var serial = new byte [sizeof (long)];
			RandomNumberGenerator.Fill (serial);

			X509Certificate2 clientCert = clientReq.Create (rootCert, start, end, serial);

			var tmp = clientCert;
			clientCert = clientCert.CopyWithPrivateKey (clientKey);
			tmp.Dispose ();

			return clientCert;
		}

		[Test]
		public async Task HttpContentStreamIsRewoundAfterCancellation ()
		{
			int testPort = GetAvailablePort ();
			using var listener = new HttpListener ();
			listener.Prefixes.Add ($"http://+:{testPort}/");
			listener.Start ();
			
			// Handle the first request - simulate a slow server to allow cancellation
			listener.BeginGetContext (ar => {
				var ctx = listener.EndGetContext (ar);
				// Read the request body slowly to ensure cancellation happens during upload
				var buffer = new byte[4096];
				try {
					while (ctx.Request.InputStream.Read (buffer, 0, buffer.Length) > 0) {
						System.Threading.Thread.Sleep (100); // Slow down to allow cancellation
					}
				} catch (Exception ex) {
					// Expected when connection is cancelled
					Console.WriteLine ($"Exception while reading request body: {ex}");
				}
				try {
					ctx.Response.StatusCode = 200;
					ctx.Response.Close ();
				} catch (Exception ex) {
					// Connection may already be closed
					Console.WriteLine ($"Exception while closing response: {ex}");
				}
			}, null);

			var tcs = new System.Threading.CancellationTokenSource ();
			tcs.CancelAfter (500); // Cancel after 500ms
			var client = new HttpClient (new AndroidMessageHandler ());
			var byc = new ByteArrayContent (new byte[1_000_000]); // 1 MB of data
			var request = new HttpRequestMessage (HttpMethod.Post, $"http://localhost:{testPort}/") { Content = byc };
			
			var stream = await byc.ReadAsStreamAsync ();
			var positionBefore = stream.Position;
			Assert.AreEqual (0, positionBefore, "Stream position should be 0 before first request");

			bool exceptionThrown = false;
			try {
				await client.SendAsync (request, tcs.Token).ConfigureAwait (false);
				// If we get here without exception, that's also OK for this test
			} catch (Exception ex) when (IsConnectionFailure (ex)) {
				Assert.Ignore ($"Ignoring transient connection failure: {ex.GetType ()}: {ex.Message}");
			} catch (Exception ex) {
				// Expected - cancellation or connection error
				// We catch all exceptions to ensure the test doesn't fail due to unhandled exceptions
				Console.WriteLine ($"Exception during first request (expected): {ex}");
				exceptionThrown = true;
			}

			// The key assertion: stream should be rewound even after an exception
			var stream2 = await byc.ReadAsStreamAsync ();
			var positionAfter = stream2.Position;
			Assert.AreEqual (0, positionAfter, "Stream position should be 0 after failed request (stream should be rewound)");

			// Only proceed with second request if we actually got an exception (test scenario succeeded)
			if (exceptionThrown) {
				var request2 = new HttpRequestMessage (HttpMethod.Post, $"http://localhost:{testPort}/") { Content = byc };
			
				// Set up listener for second request
				listener.BeginGetContext (ar => {
					var ctx = listener.EndGetContext (ar);
					ctx.Response.StatusCode = 200;
					ctx.Response.Close ();
				}, null);

				var response2 = await client.SendAsync (request2).ConfigureAwait (false);
				Assert.True (response2.IsSuccessStatusCode, "Second request should succeed with reused content");

				var stream3 = await byc.ReadAsStreamAsync ();
				var positionFinal = stream3.Position;
				Assert.AreEqual (0, positionFinal, "Stream position should be 0 after successful request");
			}

			listener.Close ();
		}

		[Test]
		public void ConnectionFailureThrowsHttpRequestException ()
		{
			// https://github.com/dotnet/android/issues/5761
			// HttpClient.SendAsync is documented to throw HttpRequestException when there is a problem
			// connecting to the server. It must not surface the legacy WebException as the primary exception.
			int unusedPort = GetAvailablePort ();
			using var client = new HttpClient (new AndroidMessageHandler ());

			var ex = Assert.CatchAsync (async () => await client.GetAsync ($"http://localhost:{unusedPort}/"));
			Assert.IsInstanceOf<HttpRequestException> (ex, $"Expected HttpRequestException but got {ex?.GetType ()}: {ex?.Message}");
			var inner = ex?.InnerException as WebException;
			Assert.IsNotNull (inner, $"Expected inner WebException but got {ex?.InnerException?.GetType ()}");
			Assert.AreEqual (WebExceptionStatus.ConnectFailure, inner.Status, "Inner WebException should preserve ConnectFailure status");
		}

		[Test]
		public void ExceedingMaxAutomaticRedirectionsThrowsHttpRequestException ()
		{
			// https://github.com/dotnet/android/issues/5761
			// Failures in the request path must be surfaced as HttpRequestException (per the HttpClient.SendAsync
			// contract). For back-compat with code migrated from classic Xamarin.Android, the legacy WebException
			// (and its WebExceptionStatus) is preserved as the inner exception.
			int port = GetAvailablePort ();
			using var listener = new HttpListener ();
			listener.Prefixes.Add ($"http://+:{port}/");
			listener.Start ();
			listener.BeginGetContext (ar => {
				var ctx = listener.EndGetContext (ar);
				ctx.Response.StatusCode = 302;
				ctx.Response.RedirectLocation = $"http://localhost:{port}/";
				ctx.Response.Close ();
			}, null);

			var handler = new AndroidMessageHandler { MaxAutomaticRedirections = 1 };
			using var client = new HttpClient (handler);

			var ex = Assert.CatchAsync (async () => await client.GetAsync ($"http://localhost:{port}/"));
			listener.Close ();

			Assert.IsInstanceOf<HttpRequestException> (ex, $"Expected HttpRequestException but got {ex?.GetType ()}: {ex?.Message}");
			var inner = ex?.InnerException as WebException;
			Assert.IsNotNull (inner, $"Expected inner WebException but got {ex?.InnerException?.GetType ()}");
			Assert.AreEqual (WebExceptionStatus.UnknownError, inner.Status, "Inner WebException should preserve UnknownError status");
		}
	}
}
