using System;
using System.Net.Http;
using System.Net.Security;
using System.Threading.Tasks;

using Xamarin.Android.Net;

using NUnit.Framework;

namespace Xamarin.Android.Net.Tests
{
	[TestFixture]
	public class ServerCertificateCustomValidationCallbackTest
	{
		[Test]
		public async Task ApproveRequest ()
		{
			bool callbackHasBeenCalled = false;

			var handler = new AndroidMessageHandler ();
			handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
				Assert.NotNull (request, "request");
				Assert.AreEqual ("microsoft.com", request.RequestUri.Host);
				Assert.NotNull (cert, "cert");
				Assert.True (cert.Subject.Contains ("microsoft.com"), $"Unexpected certificate subject {cert.Subject}");
				Assert.True (cert.Issuer.Contains ("Microsoft"), $"Unexpected certificate issuer {cert.Issuer}");
				Assert.NotNull (chain, "chain");
				Assert.AreEqual (SslPolicyErrors.None, errors);

				callbackHasBeenCalled = true;
				return true;
			};

			var client = new HttpClient (handler);
			await client.GetStringAsync ("https://microsoft.com/");

			Assert.IsTrue (callbackHasBeenCalled);
		}

		[Test]
		public async Task RejectRequest ()
		{
			bool callbackHasBeenCalled = false;

			var handler = new AndroidMessageHandler ();
			handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
				callbackHasBeenCalled = true;
				return false;
			};

			var client = new HttpClient (handler);

			try
			{
				await client.GetStringAsync ("https://microsoft.com/");
				Assert.Fail ("No exception has been thrown.");
			}
			catch (Javax.Net.Ssl.SSLHandshakeException)
			{
				// this is the expected exception type when validation fails
			}

			Assert.IsTrue (callbackHasBeenCalled);
		}
	}
}

