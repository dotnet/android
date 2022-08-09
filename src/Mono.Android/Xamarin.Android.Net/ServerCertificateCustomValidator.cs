using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using Javax.Net.Ssl;

using JavaCertificateException = Java.Security.Cert.CertificateException;
using JavaX509Certificate = Java.Security.Cert.X509Certificate;

namespace Xamarin.Android.Net
{
	internal sealed class ServerCertificateCustomValidator
	{
		public Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> Callback { get; }

		public ServerCertificateCustomValidator (Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> callback)
		{
			Callback = callback;
		}

		public ITrustManager[] InjectTrustManager (
			ITrustManager[]? trustManagers,
			HttpRequestMessage requestMessage)
		{
			var originalX509TrustManager = FindX509TrustManager(trustManagers);
			var trustManagerWithCallback = new TrustManager (originalX509TrustManager, requestMessage, Callback);
			return ModifyTrustManagersArray (trustManagers, original: originalX509TrustManager, withCallback: trustManagerWithCallback);
		}

		private static IX509TrustManager? FindX509TrustManager(ITrustManager[] trustManagers)
		{
			foreach (var trustManager in trustManagers) {
				if (trustManager is IX509TrustManager tm)
					return tm;
			}

			return null;
		}

		private static ITrustManager[] ModifyTrustManagersArray (ITrustManager[] trustManagers, IX509TrustManager? original, IX509TrustManager withCallback)
		{
			var modifiedTrustManagersArray = new ITrustManager [original is null ? trustManagers.Length + 1 : trustManagers.Length];
			modifiedTrustManagersArray[0] = withCallback;

			int nextIndex = 1;
			foreach (var trustManager in trustManagers) {
				if (trustManager == original)
					continue;

				modifiedTrustManagersArray [nextIndex++] = trustManager;
			}

			return modifiedTrustManagersArray;
		}

		private sealed class TrustManager : Java.Lang.Object, IX509TrustManager
		{
			private readonly IX509TrustManager? _internalTrustManager;
			private readonly HttpRequestMessage _request;
			private readonly Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> _serverCertificateCustomValidationCallback;

			public TrustManager (
				IX509TrustManager? internalTrustManager,
				HttpRequestMessage request,
				Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> serverCertificateCustomValidationCallback)
			{
				_request = request;
				_internalTrustManager = internalTrustManager;
				_serverCertificateCustomValidationCallback = serverCertificateCustomValidationCallback;
			}

			public void CheckServerTrusted (JavaX509Certificate[] javaChain, string authType)
			{
				var sslPolicyErrors = SslPolicyErrors.None;
				var certificates = ConvertCertificates (javaChain);

				try {
					_internalTrustManager?.CheckServerTrusted (javaChain, authType);
				} catch (JavaCertificateException) {
					sslPolicyErrors |= SslPolicyErrors.RemoteCertificateChainErrors;
				}

				X509Certificate2? certificate = certificates.Length > 0 ? certificates [0] : null;
				using X509Chain chain = CreateChain (certificates);

				if (certificate == null) {
					sslPolicyErrors |= SslPolicyErrors.RemoteCertificateNotAvailable;
				}

				if (!_serverCertificateCustomValidationCallback (_request, certificate, chain, sslPolicyErrors)) {
					throw new JavaCertificateException ("The remote certificate was rejected by the provided RemoteCertificateValidationCallback.");
				}
			}

			public void CheckClientTrusted (JavaX509Certificate[] chain, string authType)
				=> _internalTrustManager?.CheckClientTrusted (chain, authType);

			public JavaX509Certificate[] GetAcceptedIssuers ()
				=> _internalTrustManager?.GetAcceptedIssuers () ?? Array.Empty<JavaX509Certificate> ();

			private static X509Chain CreateChain (X509Certificate2[] certificates)
			{
				// the chain initialization is based on dotnet/runtime implementation in System.Net.Security.SecureChannel
				var chain = new X509Chain ();

				chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
				chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;

				chain.ChainPolicy.ExtraStore.AddRange (certificates);

				return chain;
			}

			private static X509Certificate2[] ConvertCertificates (JavaX509Certificate[] certificates)
			{
				var convertedCertificates = new X509Certificate2 [certificates.Length];
				for (int i = 0; i < certificates.Length; i++)
					convertedCertificates [i] = new X509Certificate2 (certificates [i].GetEncoded ()!);

				return convertedCertificates;
			}
		}
	}
}
