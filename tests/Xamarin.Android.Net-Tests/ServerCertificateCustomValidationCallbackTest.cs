using System;
using System.Net.Http;
using System.Threading.Tasks;

using Xamarin.Android.Net;

using NUnit.Framework;

namespace Xamarin.Android.Net.Tests
{
	[TestFixture]
	public class ServerCertificateCustomValidationCallbackTest
	{
		[Test]
		public async Task ValidatesRequest ()
		{
			bool callbackHasBeenCalled = false;

			var handler = new AndroidMessageHandler ();
			handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => {
				Assert.NotNull (request, "request");
				Assert.AreEqual (request.RequestUri.Host, "tls-test.internalx.com");
				Assert.NotNull (cert, "cert");
				Assert.AreEqual (cert.Issuer, "CN=Microsoft IT TLS CA 2, OU=Microsoft IT, O=Microsoft Corporation, L=Redmond, S=Washington, C=US");
				Assert.AreEqual (cert.Subject, "CN=*.internalx.com");
				Assert.NotNull (chain, "chain");
				// Assert.AreEqual (SslPolicyErrors.None, errors); -- the certificate expired on 1/24/2022 and hasn't been replaced yet

				callbackHasBeenCalled = true;
				return true;
			};

			var client = new HttpClient (handler);
			await client.GetStringAsync ("https://microsoft.com/");

			Assert.IsTrue (callbackHasBeenCalled);
		}

		[Test]
		public async Task RejectsRequest ()
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
			catch (HttpRequestException)
			{
				// this is the expected exception type when validation fails
			}
			catch (Exception exception)
			{
				Assert.Fail ($"An unexpected exception {exception} has been thrown.");
			}

			Assert.IsTrue (callbackHasBeenCalled);
		}
	}
}

