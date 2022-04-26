using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Xamarin.Android.Net;

using NUnit.Framework;

namespace Xamarin.Android.NetTests
{
	[Category ("InetAccess")]
	[TestFixture]
	public sealed class AndroidMessageHandlerTests : AndroidHandlerTestBase
	{
		protected override HttpMessageHandler CreateHandler ()
		{
			return new AndroidMessageHandler ();
		}

		[Test]
		public async Task ServerCertificateCustomValidationCallback_ApproveRequest ()
		{
			bool callbackHasBeenCalled = false;

			var handler = new AndroidMessageHandler {
				ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
					Assert.NotNull (request, "request");
					Assert.AreEqual ("microsoft.com", request.RequestUri.Host);
					Assert.NotNull (cert, "cert");
					Assert.True (cert!.Subject.Contains ("microsoft.com"), $"Unexpected certificate subject {cert!.Subject}");
					Assert.True (cert!.Issuer.Contains ("Microsoft"), $"Unexpected certificate issuer {cert!.Issuer}");
					Assert.NotNull (chain, "chain");
					Assert.AreEqual (SslPolicyErrors.None, errors);

					callbackHasBeenCalled = true;
					return true;
				}
			};

			var client = new HttpClient (handler);
			await client.GetStringAsync ("https://microsoft.com/");

			Assert.IsTrue (callbackHasBeenCalled, "Custom validation callback hasn't been called");
		}

		[Test]
		public async Task ServerCertificateCustomValidationCallback_RejectRequest ()
		{
			bool callbackHasBeenCalled = false;
			bool expectedExceptionHasBeenThrown = false;

			var handler = new AndroidMessageHandler {
				ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
					callbackHasBeenCalled = true;
					return false;
				}
			};

			var client = new HttpClient (handler);

			try {
				await client.GetStringAsync ("https://microsoft.com/");
			} catch (Javax.Net.Ssl.SSLHandshakeException) {
				expectedExceptionHasBeenThrown = true;
			}

			Assert.IsTrue (callbackHasBeenCalled, "Custom validation callback hasn't been called");
			Assert.IsTrue (expectedExceptionHasBeenThrown, "the expected exception hasn't been thrown");
		}

		[Test]
		public async Task ServerCertificateCustomValidationCallback_ApprovesRequestWithInvalidCertificate ()
		{
			bool callbackHasBeenCalled = false;
			bool exceptionWasThrown = false;

			var handler = new AndroidMessageHandler {
				ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
					callbackHasBeenCalled = true;
					return true;
				}
			};

			var client = new HttpClient (handler);

			try {
				await client.GetStringAsync ("https://self-signed.badssl.com/");
			} catch (Javax.Net.Ssl.SSLHandshakeException) {
				exceptionWasThrown = true;
			}

			Assert.IsTrue (callbackHasBeenCalled, "Custom validation callback hasn't been called");
			Assert.IsFalse (exceptionWasThrown, "the ssl handshake exception has been thrown");
		}
	}
}
