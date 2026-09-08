using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
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
		public async Task AndroidMessageHandlerPreservesHeadMethodOnRedirect ()
		{
			using var server = LocalHttpServer.Start ();
			Uri redirectUri = server.GetUri ("head");
			Uri requestUri = server.GetUri ($"redirect-to?url={Uri.EscapeDataString (redirectUri.ToString ())}&status_code=302");

			using var handler = new AndroidMessageHandler ();
			using var client = new HttpClient (handler);
			using var request = new HttpRequestMessage (HttpMethod.Head, requestUri);
			using HttpResponseMessage response = await client.SendAsync (request);

			Assert.AreEqual (HttpStatusCode.OK, response.StatusCode);
			Assert.AreEqual (HttpMethod.Head, response.RequestMessage.Method);
			Assert.AreEqual (redirectUri, response.RequestMessage.RequestUri);
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
			const int requestContentLength = 1_000_000;
			const int requestTimeoutMilliseconds = 10_000;

			int testPort = GetAvailablePort ();
			using var listener = new HttpListener ();
			listener.Prefixes.Add ($"http://+:{testPort}/");
			listener.Start ();

			var cancellationObserved = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
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
				await WaitForTask (contentStream.FirstWriteCompletedTask, "The first request body did not start uploading.").ConfigureAwait (false);
				Assert.IsTrue (contentStream.IsFirstCopyBlocked, "The first content copy was not blocked after its initial destination write.");
				Assert.AreEqual (1, contentStream.CopyCount, "The first request should start exactly one content copy.");

				cancellationTokenSource.Cancel ();
				var completedTask = await Task.WhenAny (firstResponseTask, Task.Delay (requestTimeoutMilliseconds)).ConfigureAwait (false);
				if (completedTask != firstResponseTask) {
					cancellationObserved.TrySetResult (true);
					await WaitForTask (firstServerTask, "The first server handler did not finish after releasing the request body.").ConfigureAwait (false);
					Assert.Fail ($"The first request did not observe cancellation within {requestTimeoutMilliseconds}ms.");
				}

				try {
					using var firstResponse = await firstResponseTask.ConfigureAwait (false);
					Assert.Fail ("The first request completed successfully instead of observing cancellation.");
				} catch (OperationCanceledException) {
					cancellationObserved.TrySetResult (true);
				}

				cancellationObserved.TrySetResult (true);
				await WaitForTask (firstServerTask, "The first server handler did not finish after cancellation.").ConfigureAwait (false);

				var streamAfterCancellation = await content.ReadAsStreamAsync ();
				Assert.AreEqual (0, streamAfterCancellation.Position, "Stream position should be 0 after cancellation (stream should be rewound)");
				Assert.AreEqual (1, contentStream.CopyCount, "Cancellation should finish the first content copy before retry.");

				retryServerTask = HandleRetryRequest ();
				using var retryRequest = new HttpRequestMessage (HttpMethod.Post, $"http://localhost:{testPort}/") { Content = content };
				var retryResponseTask = client.SendAsync (retryRequest, retryCancellationTokenSource.Token);
				retryRequestTask = retryResponseTask;
				var retryTasks = Task.WhenAll (retryResponseTask, retryServerTask);
				await WaitForTask (retryTasks, "The retry request and server handler did not finish.").ConfigureAwait (false);

				using var retryResponse = await retryResponseTask.ConfigureAwait (false);
				Assert.True (retryResponse.IsSuccessStatusCode, "Second request should succeed with reused content");
				Assert.AreEqual (2, contentStream.CopyCount, "The retry should perform a second, ungated content copy.");

				var streamAfterRetry = await content.ReadAsStreamAsync ();
				Assert.AreEqual (0, streamAfterRetry.Position, "Stream position should be 0 after successful request");
			} finally {
				bool firstWriteCompletedBeforeCleanup = contentStream.FirstWriteCompletedTask.IsCompleted;
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
					ObserveTaskAfterCleanup (firstRequestTask, "first request", firstRequestCompletedBeforeCleanup, cancellationExpected: firstRequestCancellationExpected, listenerAbortExpected: false),
					ObserveTaskAfterCleanup (firstServerTask, "first server handler", firstServerCompletedBeforeCleanup, cancellationExpected: false, listenerAbortExpected: true),
					ObserveTaskAfterCleanup (retryRequestTask, "retry request", retryRequestCompletedBeforeCleanup, cancellationExpected: !retryRequestCompletedBeforeCleanup, listenerAbortExpected: false),
					ObserveTaskAfterCleanup (retryServerTask, "retry server handler", retryServerCompletedBeforeCleanup, cancellationExpected: false, listenerAbortExpected: true)
				).ConfigureAwait (false);
			}

			async Task HandleCancelledRequest ()
			{
				var context = await listener.GetContextAsync ().ConfigureAwait (false);
				using var response = context.Response;
				var buffer = new byte [4096];
				await cancellationObserved.Task.ConfigureAwait (false);

				try {
					while (await context.Request.InputStream.ReadAsync (buffer, 0, buffer.Length).ConfigureAwait (false) > 0) {
					}
				} catch (IOException) {
					// The canceled client can close the connection while the server drains the request.
				} catch (HttpListenerException) {
					// The canceled client can close the connection while the server drains the request.
				}

				try {
					response.StatusCode = 204;
					response.ContentLength64 = 0;
					response.Close ();
				} catch (IOException) {
					// The canceled client can close the connection before the server closes the response.
				} catch (HttpListenerException) {
					// The canceled client can close the connection before the server closes the response.
				}
			}

			async Task HandleRetryRequest ()
			{
				var context = await listener.GetContextAsync ().ConfigureAwait (false);
				using var response = context.Response;
				Assert.AreEqual (requestBody.Length, context.Request.ContentLength64, "The retry request declared an unexpected content length.");
				var buffer = new byte [4096];
				int totalBytesRead = 0;
				int bytesRead;
				while ((bytesRead = await context.Request.InputStream.ReadAsync (buffer, 0, buffer.Length).ConfigureAwait (false)) > 0) {
					if (totalBytesRead + bytesRead > requestBody.Length)
						Assert.Fail ($"The retry request body exceeded the expected {requestBody.Length} bytes.");

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

			async Task WaitForTask (Task task, string failureMessage)
			{
				var completed = await Task.WhenAny (task, Task.Delay (requestTimeoutMilliseconds)).ConfigureAwait (false);
				if (completed != task)
					Assert.Fail ($"{failureMessage} Timeout: {requestTimeoutMilliseconds}ms.");

				await task.ConfigureAwait (false);
			}

			async Task ObserveTaskAfterCleanup (Task task, string taskName, bool completedBeforeCleanup, bool cancellationExpected, bool listenerAbortExpected)
			{
				var completed = await Task.WhenAny (task, Task.Delay (requestTimeoutMilliseconds)).ConfigureAwait (false);
				if (completed != task)
					Assert.Fail ($"The {taskName} did not finish during cleanup within {requestTimeoutMilliseconds}ms.");

				try {
					await task.ConfigureAwait (false);
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
				var firstWriteCompleted = await Task.WhenAny (stream.FirstWriteCompletedTask, Task.Delay (copyTimeoutMilliseconds)).ConfigureAwait (false);
				Assert.AreSame (stream.FirstWriteCompletedTask, firstWriteCompleted, "The controlled stream did not complete its first destination write.");
				await stream.FirstWriteCompletedTask.ConfigureAwait (false);

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

				var firstCopyCompleted = await Task.WhenAny (firstCopyTask, Task.Delay (copyTimeoutMilliseconds)).ConfigureAwait (false);
				Assert.AreSame (firstCopyTask, firstCopyCompleted, "The first controlled copy did not finish during cleanup.");
				try {
					await firstCopyTask.ConfigureAwait (false);
				} catch (OperationCanceledException) {
				}
			}
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
