using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

using Android.OS;
using Android.Net.Http;
using Android.Runtime;
using Javax.Net.Ssl;

using JavaCertificateException = Java.Security.Cert.CertificateException;
using JavaX509Certificate = Java.Security.Cert.X509Certificate;

namespace Xamarin.Android.Net
{
	internal sealed class ServerCertificateCustomValidator
	{
		public IHostnameVerifier HostnameVerifier => AlwaysAcceptingHostnameVerifier.Instance;

		public Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> Callback { get; set; }

		public ServerCertificateCustomValidator (Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> callback)
		{
			Callback = callback;
		}

		public ITrustManager[] ReplaceX509TrustManager (ITrustManager[]? trustManagers, HttpRequestMessage requestMessage)
		{
			trustManagers ??= [];
			var originalX509TrustManager = FindX509TrustManager(trustManagers, out int originalTrustManagerIndex);
			var trustManagerWithCallback = new TrustManager (originalX509TrustManager, requestMessage, Callback);
			return ModifyTrustManagersArray (trustManagers, originalTrustManagerIndex, trustManagerWithCallback);
		}

		private sealed class TrustManager : Java.Lang.Object, IX509TrustManager
		{
			private readonly IX509TrustManager _internalTrustManager;
			private readonly HttpRequestMessage _request;
			private readonly Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> _serverCertificateCustomValidationCallback;

			public TrustManager (
				IX509TrustManager internalTrustManager,
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

				try {
					var trustManagerExtensions = new X509TrustManagerExtensions (_internalTrustManager);
					trustManagerExtensions.CheckServerTrusted (javaChain, authType, _request.RequestUri?.Host);
				} catch (JavaCertificateException) {
					sslPolicyErrors |= SslPolicyErrors.RemoteCertificateChainErrors;
				}

				var certificates = Convert (javaChain);
				X509Certificate2? certificate = null;

				if (certificates.Length > 0) {
					certificate = certificates [0];
				} else {
					sslPolicyErrors |= SslPolicyErrors.RemoteCertificateNotAvailable;
				}

				if (!VerifyHostname (javaChain)) {
					sslPolicyErrors |= SslPolicyErrors.RemoteCertificateNameMismatch;
				}

				if (!_serverCertificateCustomValidationCallback (_request, certificate, CreateChain (certificates), sslPolicyErrors)) {
					throw new JavaCertificateException ("The remote certificate was rejected by the provided RemoteCertificateValidationCallback.");
				}
			}

			public void CheckClientTrusted (JavaX509Certificate[] chain, string authType)
				=> _internalTrustManager.CheckClientTrusted (chain, authType);

			public JavaX509Certificate[] GetAcceptedIssuers ()
				=> _internalTrustManager.GetAcceptedIssuers () ?? Array.Empty<JavaX509Certificate> ();

			private bool VerifyHostname (JavaX509Certificate[] javaChain)
			{
				var sslSession = new FakeSSLSession (javaChain);
				var hostnameVerifier = HttpsURLConnection.DefaultHostnameVerifier;
				if (hostnameVerifier is null) {
					return false;
				}
				
				return hostnameVerifier.Verify(_request.RequestUri?.Host, sslSession);
			}

			private static X509Chain CreateChain (X509Certificate2[] certificates)
			{
				// the chain initialization is based on dotnet/runtime implementation in System.Net.Security.SecureChannel
				var chain = new X509Chain ();

				chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
				chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;

				chain.ChainPolicy.ExtraStore.AddRange (certificates);

				return chain;
			}

			private static X509Certificate2[] Convert (JavaX509Certificate[] certificates)
			{
				var convertedCertificates = new X509Certificate2 [certificates.Length];
				for (int i = 0; i < certificates.Length; i++) {
					var data = certificates [i].GetEncoded () ?? throw new InvalidOperationException ("The remote certificate was not available.");
					convertedCertificates [i] = X509CertificateLoader.LoadCertificate (data);
				}

				return convertedCertificates;
			}

			// We rely on the fact that the OkHostnameVerifier class that implements the default hostname
			// verifier on Android uses the SSLSession object only to get the peer certificates (as of 2022).
			// This could change in future Android versions and we would have to implement more methods
			// and properties of this interface.
			private sealed class FakeSSLSession : Java.Lang.Object, ISSLSession
			{
				private readonly JavaX509Certificate[] _certificates;

				public FakeSSLSession (JavaX509Certificate[] certificates)
				{
					_certificates = certificates;
				}

				public Java.Security.Cert.Certificate[] GetPeerCertificates () => _certificates;

				public int ApplicationBufferSize => throw new InvalidOperationException ();
				public string CipherSuite => throw new InvalidOperationException ();
				public long CreationTime => throw new InvalidOperationException ();
				public bool IsValid => throw new InvalidOperationException ();
				public long LastAccessedTime => throw new InvalidOperationException ();
				public Java.Security.IPrincipal LocalPrincipal => throw new InvalidOperationException ();
				public int PacketBufferSize => throw new InvalidOperationException ();
				public string PeerHost => throw new InvalidOperationException ();
				public int PeerPort => throw new InvalidOperationException ();
				public Java.Security.IPrincipal PeerPrincipal => throw new InvalidOperationException ();
				public string Protocol => throw new InvalidOperationException ();
				public ISSLSessionContext SessionContext => throw new InvalidOperationException ();

				public byte[] GetId () => throw new InvalidOperationException ();
				public Java.Security.Cert.Certificate[] GetLocalCertificates () => throw new InvalidOperationException ();
				public Javax.Security.Cert.X509Certificate[] GetPeerCertificateChain () => throw new InvalidOperationException ();
				public Java.Lang.Object GetValue(string name) => throw new InvalidOperationException ();
				public string[] GetValueNames () => throw new InvalidOperationException ();
				public void Invalidate () => throw new InvalidOperationException ();
				public void PutValue(string name, Java.Lang.Object value) => throw new InvalidOperationException ();
				public void RemoveValue(string name) => throw new InvalidOperationException ();
			}
		}

		// When the hostname verifier is reached, the trust manager has already invoked the
		// custom validation callback and approved the remote certificate (including hostname
		// mismatch) so at this point there's no verification left to.
		private sealed class AlwaysAcceptingHostnameVerifier : Java.Lang.Object, IHostnameVerifier
		{
			private readonly static Lazy<AlwaysAcceptingHostnameVerifier> s_instance = new Lazy<AlwaysAcceptingHostnameVerifier> (() => new AlwaysAcceptingHostnameVerifier ());

			public static AlwaysAcceptingHostnameVerifier Instance => s_instance.Value;

			public bool Verify (string? hostname, ISSLSession? session) => true;
		}

		private static IX509TrustManager FindX509TrustManager(ITrustManager[] trustManagers, out int index)
		{
			index = -1;

			for (int i = 0; i < trustManagers.Length; i++) {
				var trustManager = trustManagers [i];
				if (trustManager is IX509TrustManager x509TrustManager) {
					index = i;
					return x509TrustManager;
				}
			}

			if (trustManagers.Length > 10_000) {
				HackToPreserveInvokers(trustManagers);
			}

			throw new InvalidOperationException($"Could not find {nameof(IX509TrustManager)} in {nameof(ITrustManager)} array.");
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static void HackToPreserveInvokers (ITrustManager[] trustManagers)
		{
			// HACK - make IX509TrustManagerInvoker visible to the linker so that it doesn't get trimmed out.
			// These branches are unreachable, but the linker doesn't know that.
			if (trustManagers.Length > 1_000_000) {
				_ = new IX509TrustManagerInvoker (IntPtr.Zero, JniHandleOwnership.DoNotTransfer);
			} else if (trustManagers.Length > 2_000_000) {
				_ = new X509ExtendedTrustManagerInvoker (IntPtr.Zero, JniHandleOwnership.DoNotTransfer);
			}
		}

		private static ITrustManager[] ModifyTrustManagersArray (ITrustManager[] trustManagers, int originalTrustManagerIndex, IX509TrustManager replacement)
		{
			var modifiedTrustManagersArray = new ITrustManager [trustManagers.Length];

			for (int i = 0; i < trustManagers.Length; i++) {
				if (i == originalTrustManagerIndex) {
					modifiedTrustManagersArray [i] = replacement;
				} else {
					modifiedTrustManagersArray [i] = trustManagers [i];
				}
			}

			return modifiedTrustManagersArray;
		}
	}
}
