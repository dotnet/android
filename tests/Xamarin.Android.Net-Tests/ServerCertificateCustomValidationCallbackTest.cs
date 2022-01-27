using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Xamarin.Android.Net;

using NUnit.Framework;

namespace Xamarin.Android.Net.Tests
{
	[Category ("InetAccess")]
	public abstract class ServerCertificateCustomValidationCallbackTest
	{
		protected abstract HttpMessageHandler CreateHandlerWithCallback(Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors> callback);

		[Test]
		public async Task ApproveRequest ()
		{
			bool callbackHasBeenCalled = false;

			var handler = CreateHandlerWithCallback(
				(request, cert, chain, errors) => {
					Assert.NotNull (request, "request");
					Assert.AreEqual ("microsoft.com", request.RequestUri.Host);
					Assert.NotNull (cert, "cert");
					Assert.True (cert.Subject.Contains ("microsoft.com"), $"Unexpected certificate subject {cert.Subject}");
					Assert.True (cert.Issuer.Contains ("Microsoft"), $"Unexpected certificate issuer {cert.Issuer}");
					Assert.NotNull (chain, "chain");
					Assert.AreEqual (SslPolicyErrors.None, errors);

					callbackHasBeenCalled = true;
					return true;
				}
			);

			var client = new HttpClient (handler);
			await client.GetStringAsync ("https://microsoft.com/");

			Assert.IsTrue (callbackHasBeenCalled);
		}

		[Test]
		public async Task RejectRequest ()
		{
			bool callbackHasBeenCalled = false;

			var handler = CreateHandlerWithCallback(
				(request, cert, chain, errors) => {
					callbackHasBeenCalled = true;
					return false;
				}
			);

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

	[TestFixture]
	public class AndroidMessageHandler_ServerCertificateCustomValidationCallbackTest : ServerCertificateCustomValidationCallbackTest
	{
		protected override HttpMessageHandler CreateHandlerWithCallback(Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors> callback)
			=> new Xamarin.Android.Net.AndroidMessageHandler
			{
				ServerCertificateCustomValidationCallback = callback
			};
	}

	[TestFixture]
	public class AndroidClientHandler_ServerCertificateCustomValidationCallbackTest : ServerCertificateCustomValidationCallbackTest
	{
		protected override HttpMessageHandler CreateHandlerWithCallback(Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors> callback)
			=> new Xamarin.Android.Net.AndroidClientHandler
			{
				ServerCertificateCustomValidationCallback = (Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors>)callback
			};
	}
}



