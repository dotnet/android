using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Xamarin.Installer.AndroidSDK.Manager
{
	/// <summary>
	/// Creates a HttpClient.
	/// </summary>
	public static class HttpClientProvider
	{
		static Func<Uri, HttpClient> httpClientFactory;

		/// <summary>
		/// Allows the default HttpClient creation to be overridden.
		/// </summary>
		/// <param name="factory">Factory method used to create the HttpClient.</param>
		public static void SetHttpClientFactory (Func<Uri, HttpClient> factory)
		{
			httpClientFactory = factory;
		}

		/// <summary>
		/// Creates a new HttpClient.
		/// </summary>
		/// <returns>The HttpClient.</returns>
		/// <param name="uri">The request url.</param>
		/// <param name="cookieContainer">The cookie container to use, or null to not use cookies.</param>
		/// <param name="automaticDecompression">The decompression methods to support.</param>
		public static HttpClient CreateHttpClient (Uri uri, CookieContainer cookieContainer = null, DecompressionMethods automaticDecompression = DecompressionMethods.None)
		{
			if (httpClientFactory != null)
				return httpClientFactory.Invoke (uri);

			var handler = new HttpClientHandler
			{
				AutomaticDecompression = automaticDecompression,
				CheckCertificateRevocationList = true,
				ServerCertificateCustomValidationCallback = SslValidationCallback
			};

			if (cookieContainer != null)
			{
				handler.CookieContainer = cookieContainer;
			}

			if (WebRequest.DefaultWebProxy != null)
				handler.Proxy = WebRequest.DefaultWebProxy;

			var client = new HttpClient(handler);
			return client;
		}

		static bool SslValidationCallback(HttpRequestMessage request, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			if (sslPolicyErrors == SslPolicyErrors.None)
				return true;

			if (sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
				return false;

			// If all we have is "Revocation Unknown" errors, then accept the certificate. Works around Enterprise proxy issues
			// such as the one described in https://devdiv.visualstudio.com/DevDiv/_workitems/edit/2057013
			foreach (var status in chain.ChainStatus)
			{
				if (status.Status != X509ChainStatusFlags.NoError && status.Status != X509ChainStatusFlags.RevocationStatusUnknown)
					return false;
			}

			return true;
		}
	}
}