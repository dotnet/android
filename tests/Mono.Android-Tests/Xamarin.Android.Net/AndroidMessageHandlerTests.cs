using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Android.Runtime;
using Xamarin.Android.Net;

using NUnit.Framework;

namespace Xamarin.Android.NetTests
{
	[TestFixture]
	public class AndroidMessageHandlerTests : AndroidHandlerTestBase
	{
		protected override HttpMessageHandler CreateHandler ()
		{
			return new AndroidMessageHandler ();
		}

		// We can't test `deflate` for now because it's broken in the BCL for https://httpbin.org/deflate (S.I.Compression.DeflateStream doesn't recognize the compression
		// method used by the server)
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
		// Disabled because it doesn't exist in NUnitLite, uncomment when/if we switch to full NUnit
		// When we can use it, replace all the Console.WriteLine calls with Assert.Warn
		// [Retry (5)]
		public async Task Decompression (string urlPath, string encoding, string jsonFieldName)
		{
			// Catch all the exceptions and warn about them or otherwise [Retry] above won't work
			try {
				int count = 0;
				// Remove the loop when [Retry] can be used
				while (count < 5) {
					if (await DoDecompression (urlPath, encoding, jsonFieldName)) {
						return;
					}
					count++;
				}
			} catch (Exception ex) {
				Console.WriteLine ("Unexpected exception thrown");
				Console.WriteLine (ex.ToString ());
				Assert.Fail ("Exception should have not been thrown");
			}
		}

		async Task<bool> DoDecompression (string urlPath, string encoding, string jsonFieldName)
		{
			var handler = new AndroidMessageHandler {
				AutomaticDecompression = DecompressionMethods.All
			};

			var client = new HttpClient (handler);
			HttpResponseMessage response = await client.GetAsync ($"https://httpbin.org/{urlPath}");

			// Failing on error codes other than 2xx will make NUnit retry the test up to the number of times specified in the
			// [Retry] attribute above.  This may or may not the desired effect if httpbin.org is throttling the requests, thus
			// we will sleep a short while before failing the test
			if (!response.IsSuccessStatusCode) {
				System.Threading.Thread.Sleep (1000);
				// Uncomment when we can use [Retry]
				//Assert.Fail ($"Request ended with a failure error code: {response.StatusCode}");
				return false;
			}

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

			return true;
		}

		[Test]
		public async Task DoesNotDisposeContentStream()
		{
			using var listener = new HttpListener ();
			listener.Prefixes.Add ("http://+:47663/");
			listener.Start ();
			listener.BeginGetContext (ar => {
				var ctx = listener.EndGetContext (ar);
				ctx.Response.StatusCode = 204;
				ctx.Response.ContentLength64 = 0;
				ctx.Response.Close ();
			}, null);

			var jsonContent = new StringContent ("hello");
			var request = new HttpRequestMessage (HttpMethod.Post, "http://localhost:47663/") { Content = jsonContent };

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

			var handler = new AndroidMessageHandler {
				ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
					Assert.NotNull (request, "request");
					Assert.AreEqual ("www.microsoft.com", request.RequestUri.Host);
					Assert.NotNull (cert, "cert");
					Assert.True (cert!.Subject.Contains ("www.microsoft.com"), $"Unexpected certificate subject {cert!.Subject}");
					Assert.True (cert!.Issuer.Contains ("Microsoft"), $"Unexpected certificate issuer {cert!.Issuer}");
					Assert.NotNull (chain, "chain");
					Assert.AreEqual (SslPolicyErrors.None, errors);

					callbackHasBeenCalled = true;
					return true;
				}
			};

			var client = new HttpClient (handler);
			await client.GetStringAsync ("https://www.microsoft.com/");

			Assert.IsTrue (callbackHasBeenCalled, "custom validation callback hasn't been called");
		}

		[Test]
		public async Task ServerCertificateCustomValidationCallback_RejectRequest ()
		{
			bool callbackHasBeenCalled = false;

			var handler = new AndroidMessageHandler {
				ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
					callbackHasBeenCalled = true;
					return false;
				}
			};
			var client = new HttpClient (handler);

			await AssertRejectsRemoteCertificate (() => client.GetStringAsync ("https://www.microsoft.com/"));

			Assert.IsTrue (callbackHasBeenCalled, "custom validation callback hasn't been called");
		}

		[Test]
		public async Task ServerCertificateCustomValidationCallback_ApprovesRequestWithInvalidCertificate ()
		{
			bool callbackHasBeenCalled = false;

			var handler = new AndroidMessageHandler {
				ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
					callbackHasBeenCalled = true;
					return true;
				}
			};

			var client = new HttpClient (handler);
			await client.GetStringAsync ("https://self-signed.badssl.com/");

			Assert.IsTrue (callbackHasBeenCalled, "custom validation callback hasn't been called");
		}

		[Test]
		public async Task NoServerCertificateCustomValidationCallback_ThrowsWhenThereIsCertificateHostnameMismatch ()
		{
			var handler = new AndroidMessageHandler ();
			var client = new HttpClient (handler);

			await AssertRejectsRemoteCertificate (() => client.GetStringAsync ("https://wrong.host.badssl.com/"));
		}

		[Test]
		public async Task ServerCertificateCustomValidationCallback_IgnoresCertificateHostnameMismatch ()
		{
			bool callbackHasBeenCalled = false;
			SslPolicyErrors reportedErrors = SslPolicyErrors.None;

			var handler = new AndroidMessageHandler {
				ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
					callbackHasBeenCalled = true;
					reportedErrors = errors;
					return true;
				}
			};

			var client = new HttpClient (handler);
			await client.GetStringAsync ("https://wrong.host.badssl.com/");

			Assert.IsTrue (callbackHasBeenCalled, "custom validation callback hasn't been called");
			Assert.AreEqual (SslPolicyErrors.RemoteCertificateNameMismatch, reportedErrors & SslPolicyErrors.RemoteCertificateNameMismatch);
		}

		[Test]
		public async Task ServerCertificateCustomValidationCallback_Redirects ()
		{
			int callbackCounter = 0;

			var handler = new AndroidMessageHandler {
				ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
					callbackCounter++;
					return errors == SslPolicyErrors.None;
				}
			};

			var client = new HttpClient (handler);
			var result = await client.GetAsync ("https://httpbin.org/redirect-to?url=https://www.microsoft.com/");

			Assert.AreEqual (2, callbackCounter);
			Assert.IsTrue (result.IsSuccessStatusCode);
		}

		[Test]
		public async Task AndroidMessageHandlerFollows308PermanentRedirect ()
		{
			int callbackCounter = 0;

			var handler = new AndroidMessageHandler ();

			var client = new HttpClient (handler);
			var result = await client.GetAsync ("https://httpbin.org/redirect-to?url=https://www.microsoft.com/&status_code=308");

			Assert.IsTrue (result.IsSuccessStatusCode);
			Assert.AreEqual ("https://www.microsoft.com/", result.RequestMessage.RequestUri.ToString ());
		}

		[Test]
		public async Task AndroidMessageHandlerSendsClientCertificate ()
		{
			const string testClientCertificate = "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";
			using X509Certificate2 certificate = new X509Certificate2 (Convert.FromBase64String (testClientCertificate), "pass");

			using var handler = new AndroidMessageHandler ();
			handler.ClientCertificates.Add (certificate);
			using var client = new HttpClient (handler);
			var response = await client.GetAsync ("https://corefx-net-tls.azurewebsites.net/EchoClientCertificate.ashx");
			var content = await response.EnsureSuccessStatusCode ().Content.ReadAsStringAsync ();

			X509Certificate2 certificate2 = new X509Certificate2 (global::System.Convert.FromBase64String (content));
			Assert.AreEqual (certificate.Thumbprint, certificate2.Thumbprint);
		}

		private async Task AssertRejectsRemoteCertificate (Func<Task> makeRequest)
		{
			// there is a difference between the exception that's thrown in the .NET build and the legacy Xamarin
			// because there's a difference in the $(AndroidBoundExceptionType) property value (legacy: Java, .NET: System)
			try {
				await makeRequest();
				Assert.Fail ("The request wasn't rejected");
			}
			// While technically we should be throwing only HttpRequestException (as per HttpClient.SendAsync docs), in reality
			// we need to consider legacy code that migrated to .NET and may still expect WebException.  Thus, we throw both
			// of these and we need to catch both here
			catch (System.Net.WebException) {}
			catch (System.Net.Http.HttpRequestException) {}
		}
	}
}
