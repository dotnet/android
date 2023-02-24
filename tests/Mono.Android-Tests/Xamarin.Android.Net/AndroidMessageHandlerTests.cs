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
			},

			new object[] {
				"brotli", // urlPath
				"br", // encoding
			},
		};

#if NET
		[Test]
		[TestCaseSource (nameof (DecompressionSource))]
		public async Task Decompression (string urlPath, string encoding)
		{
			var handler = new AndroidMessageHandler {
				AutomaticDecompression = DecompressionMethods.All
			};

			var client = new HttpClient (handler);
			HttpResponseMessage response = await client.GetAsync ($"https://httpbin.org/{urlPath}");

			foreach (string enc in response.Content.Headers.ContentEncoding) {
				if (String.Compare (enc, encoding, StringComparison.Ordinal) == 0) {
					Assert.Fail ($"Encoding '{encoding}' should have been removed from the Content-Encoding header");
				}
			}

			string responseBody = await response.Content.ReadAsStringAsync ();

			Assert.IsTrue (responseBody.Length > 0, "Response was empty");
			Assert.IsTrue (responseBody.Contains ($"\"{urlPath}\"", StringComparison.OrdinalIgnoreCase), $"\"{urlPath}\" should have been in the response JSON");
		}
#endif

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

		private async Task AssertRejectsRemoteCertificate (Func<Task> makeRequest)
		{
			// there is a difference between the exception that's thrown in the .NET build and the legacy Xamarin
			// because there's a difference in the $(AndroidBoundExceptionType) property value (legacy: Java, .NET: System)
			try {
				await makeRequest();
				Assert.Fail ("The request wasn't rejected");
			}
#if NET
			// While technically we should be throwing only HttpRequestException (as per HttpClient.SendAsync docs), in reality
			// we need to consider legacy code that migrated to .NET and may still expect WebException.  Thus, we throw both
			// of these and we need to catch both here
			catch (System.Net.WebException) {}
#else
			catch (Java.IO.IOException) {}
#endif
			catch (System.Net.Http.HttpRequestException) {}
		}
	}
}
