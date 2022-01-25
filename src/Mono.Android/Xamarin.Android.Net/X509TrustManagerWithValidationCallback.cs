using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using Javax.Net.Ssl;

using JavaCertificateException = Java.Security.Cert.CertificateException;
using JavaX509Certificate = Java.Security.Cert.X509Certificate;

namespace Xamarin.Android.Net
{
    internal sealed class X509TrustManagerWithValidationCallback : Java.Lang.Object, IX509TrustManager
    {
        private readonly IX509TrustManager? _internalTrustManager;
        private readonly HttpRequestMessage _request;
        private readonly Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> _serverCertificateCustomValidationCallback;

        public static ITrustManager[] Inject(
            ITrustManager[]? trustManagers,
            HttpRequestMessage requestMessage,
            Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> serverCertificateCustomValidationCallback)
        {
            IX509TrustManager? x509TrustManager = trustManagers?.OfType<IX509TrustManager> ().FirstOrDefault ();
            IEnumerable<ITrustManager> otherTrustManagers = trustManagers?.Where (manager => manager != x509TrustManager) ?? Enumerable.Empty<ITrustManager> ();
            var trustManagerWithCallback = new X509TrustManagerWithValidationCallback (x509TrustManager, requestMessage, serverCertificateCustomValidationCallback);
            return otherTrustManagers.Append (trustManagerWithCallback).ToArray ();
        }

        public X509TrustManagerWithValidationCallback(
            IX509TrustManager? internalTrustManager,
            HttpRequestMessage request,
            Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> serverCertificateCustomValidationCallback)
        {
            _request = request;
            _internalTrustManager = internalTrustManager;
            _serverCertificateCustomValidationCallback = serverCertificateCustomValidationCallback;
        }

        public void CheckServerTrusted (JavaX509Certificate[] javaChain, string authType)
        {
            var sslPolicyErrors = SslPolicyErrors.None;
            var certificates = ConvertCertificates (javaChain);

            try
            {
                _internalTrustManager?.CheckServerTrusted (javaChain, authType);
            }
            catch (JavaCertificateException)
            {
                sslPolicyErrors |= SslPolicyErrors.RemoteCertificateChainErrors;
            }

            X509Certificate2? certificate = certificates.FirstOrDefault ();
            using X509Chain chain = CreateChain (certificates);

            if (certificate == null)
            {
                sslPolicyErrors |= SslPolicyErrors.RemoteCertificateNotAvailable;
            }

            // certificate might be null, but we have to adhere to the Func parameters of HttpClientHandler which
            // doesn't contain the nullable annotation
            if (!_serverCertificateCustomValidationCallback (_request, certificate!, chain, sslPolicyErrors))
            {
                throw new JavaCertificateException("The remote certificate was rejected by the provided RemoteCertificateValidationCallback.");
            }
        }

        public void CheckClientTrusted (JavaX509Certificate[] chain, string authType)
            => _internalTrustManager?.CheckClientTrusted (chain, authType);

        public JavaX509Certificate[] GetAcceptedIssuers ()
            => _internalTrustManager?.GetAcceptedIssuers () ?? Array.Empty<JavaX509Certificate> ();

        private static X509Chain CreateChain (X509Certificate2[] certificates)
        {
            // TODO I doubt this is the correct way to implement this and this whole method needs revisiting
            var chain = new X509Chain ();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.ExtraStore.AddRange (certificates);
            return chain;
        }

        private static X509Certificate2[] ConvertCertificates (JavaX509Certificate[] certificates)
            => certificates.Select (cert => new X509Certificate2 (cert.GetEncoded ()!)).ToArray ();
    }
}
