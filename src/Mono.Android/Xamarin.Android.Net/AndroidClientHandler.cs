using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Android.OS;
using Android.Runtime;
using Java.IO;
using Java.Net;
using Java.Security;
using Java.Security.Cert;
using Javax.Net.Ssl;

namespace Xamarin.Android.Net
{
	/// <summary>
	/// A custom implementation of <see cref="System.Net.Http.HttpClientHandler"/> which internally uses <see cref="Java.Net.HttpURLConnection"/>
	/// (or its HTTPS incarnation) to send HTTP requests.
	/// </summary>
	/// <remarks>
	/// <para>Instance of this class is used to configure <see cref="System.Net.Http.HttpClient"/> instance
	/// in the following way:
	///
	/// <example>
	/// var handler = new AndroidClientHandler {
	///    UseCookies = true,
	///    AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
	/// };
	///
	/// var httpClient = new HttpClient (handler);
	/// var response = httpClient.GetAsync ("http://example.com")?.Result as AndroidHttpResponseMessage;
	/// </example></para>
	/// <para>
	/// The class supports pre-authentication of requests albeit in a slightly "manual" way. Namely, whenever a request to a server requiring authentication
	/// is made and no authentication credentials are provided in the <see cref="PreAuthenticationData"/> property (which is usually the case on the first
	/// request), the <see cref="RequestNeedsAuthorization"/> property will return <c>true</c> and the <see cref="RequestedAuthentication"/> property will
	/// contain all the authentication information gathered from the server. The application must then fill in the blanks (i.e. the credentials) and re-send
	/// the request configured to perform pre-authentication. The reason for this manual process is that the underlying Java HTTP client API supports only a
	/// single, VM-wide, authentication handler which cannot be configured to handle credentials for several requests. AndroidClientHandler, therefore, implements
	/// the authentication in managed .NET code. Message handler supports both Basic and Digest authentication. If an authentication scheme that's not supported
	/// by AndroidClientHandler is requested by the server, the application can provide its own authentication module (<see cref="AuthenticationData"/>,
	/// <see cref="PreAuthenticationData"/>) to handle the protocol authorization.</para>
	/// <para>AndroidClientHandler also supports requests to servers with "invalid" (e.g. self-signed) SSL certificates. Since this process is a bit convoluted using
	/// the Java APIs, AndroidClientHandler defines a way to handle the situation. It can store the necessary certificates (either CA or server certificates)
	/// in the <see cref="TrustedCerts"/> collection. If, however, the application requires finer control over the SSL configuration (e.g. it implements its own
	/// TrustManager) then it should derive a custom class from <see cref="Xamarin.Android.Net.AndroidMessageHandler"/> instead of using AndroidClientHandler.
	/// Note that the instance of AndroidClientHandler configured to accept an "invalid" certificate from the particular server will most likely fail to validate
	/// certificates from other servers (even if they use a certificate with a fully validated trust chain) unless you store the CA certificates from your Android
	/// system in <see cref="TrustedCerts"/> along with the self-signed certificate(s).</para>
	/// </remarks>
	[Obsolete("AndroidClientHandler has been deprecated. Use AndroidMessageHandler instead.")]
	public class AndroidClientHandler : HttpClientHandler
	{
		internal const string LOG_APP = "monodroid-net";
		AndroidMessageHandler _underlyingHander;

		bool disposed;

		public AndroidClientHandler ()
		{
			object? handler = GetUnderlyingHandler ();
			_underlyingHander = handler as AndroidMessageHandler ?? throw new InvalidOperationException ($"Unknown underlying handler '{GetHandlerTypeName (handler)}'.  Only AndroidMessageHandler is supported for AndroidClientHandler");
		}

		static string GetHandlerTypeName (object? handler) => handler?.GetType()?.FullName ?? "<null>";

		/// <summary>
		/// <para>
		/// Gets or sets the pre authentication data for the request. This property must be set by the application
		/// before the request is made. Generally the value can be taken from <see cref="RequestedAuthentication"/>
		/// after the initial request, without any authentication data, receives the authorization request from the
		/// server. The application must then store credentials in instance of <see cref="AuthenticationData"/> and
		/// assign the instance to this propery before retrying the request.
		/// </para>
		/// <para>
		/// The property is never set by AndroidClientHandler.
		/// </para>
		/// </summary>
		/// <value>The pre authentication data.</value>
		public AuthenticationData? PreAuthenticationData
		{
			get { return _underlyingHander.PreAuthenticationData; }
			set { _underlyingHander.PreAuthenticationData = value; }
		}

		/// <summary>
		/// If the website requires authentication, this property will contain data about each scheme supported
		/// by the server after the response. Note that unauthorized request will return a valid response - you
		/// need to check the status code and and (re)configure AndroidClientHandler instance accordingly by providing
		/// both the credentials and the authentication scheme by setting the <see cref="PreAuthenticationData"/>
		/// property. If AndroidClientHandler is not able to detect the kind of authentication scheme it will store an
		/// instance of <see cref="AuthenticationData"/> with its <see cref="AuthenticationData.Scheme"/> property
		/// set to <c>AuthenticationScheme.Unsupported</c> and the application will be responsible for providing an
		/// instance of <see cref="IAndroidAuthenticationModule"/> which handles this kind of authorization scheme
		/// (<see cref="AuthenticationData.AuthModule"/>
		/// </summary>
		public IList <AuthenticationData>? RequestedAuthentication
		{
			get { return _underlyingHander.RequestedAuthentication; }
		}

		/// <summary>
		/// Server authentication response indicates that the request to authorize comes from a proxy if this property is <c>true</c>.
		/// All the instances of <see cref="AuthenticationData"/> stored in the <see cref="RequestedAuthentication"/> property will
		/// have their <see cref="AuthenticationData.UseProxyAuthentication"/> preset to the same value as this property.
		/// </summary>
		public bool ProxyAuthenticationRequested
		{
			get { return _underlyingHander.ProxyAuthenticationRequested; }
		}

		/// <summary>
		/// If <c>true</c> then the server requested authorization and the application must use information
		/// found in <see cref="RequestedAuthentication"/> to set the value of <see cref="PreAuthenticationData"/>
		/// </summary>
		public bool RequestNeedsAuthorization
		{
			get { return _underlyingHander.RequestNeedsAuthorization; }
		}

		/// <summary>
		/// <para>
		/// If the request is to the server protected with a self-signed (or otherwise untrusted) SSL certificate, the request will
		/// fail security chain verification unless the application provides either the CA certificate of the entity which issued the
		/// server's certificate or, alternatively, provides the server public key. Whichever the case, the certificate(s) must be stored
		/// in this property in order for AndroidClientHandler to configure the request to accept the server certificate.</para>
		/// <para>AndroidClientHandler uses a custom <see cref="KeyStore"/> and <see cref="TrustManagerFactory"/> to configure the connection.
		/// If, however, the application requires finer control over the SSL configuration (e.g. it implements its own TrustManager) then
		/// it should derive a custom class from <see cref="Xamarin.Android.Net.AndroidMessageHandler"/> instead of using AndroidClientHandler.</para>
		/// </summary>
		/// <value>The trusted certs.</value>
		public IList <Certificate>? TrustedCerts
		{
			get { return _underlyingHander.TrustedCerts; }
			set { _underlyingHander.TrustedCerts = value; }
		}

		/// <summary>
		/// <para>
		/// Specifies the connection read timeout.
		/// </para>
		/// <para>
		/// Since there's no way for the handler to access <see cref="t:System.Net.Http.HttpClient.Timeout"/>
		/// directly, this property should be set by the calling party to the same desired value. Value of this
		/// property will be passed to the native Java HTTP client, unless it is set to <see
		/// cref="t:System.TimeSpan.Zero"/>
		/// </para>
		/// <para>
		/// The default value is <c>24</c> hours, much higher than the documented value of <see
		/// cref="t:System.Net.Http.HttpClient.Timeout"/> and the same as the value of iOS-specific
		/// NSUrlSessionHandler.
		/// </para>
		/// </summary>
		public TimeSpan ReadTimeout
		{
			get { return _underlyingHander.ReadTimeout; }
			set { _underlyingHander.ReadTimeout = value; }
		}

		/// <summary>
		/// <para>
		/// Specifies the connect timeout
		/// </para>
		/// <para>
		/// The native Java client supports two separate timeouts - one for reading from the connection (<see
		/// cref="ReadTimeout"/>) and another for establishing the connection. This property sets the value of
		/// the latter timeout, unless it is set to <see cref="t:System.TimeSpan.Zero"/> in which case the
		/// native Java client defaults are used.
		/// </para>
		/// <para>
		/// The default value is <c>120</c> seconds.
		/// </para>
		/// </summary>
		public TimeSpan ConnectTimeout
		{
			get { return _underlyingHander.ConnectTimeout; }
			set { _underlyingHander.ConnectTimeout = value; }
		}

		protected override void Dispose (bool disposing)
		{
			disposed  = true;

			base.Dispose (disposing);
		}

		protected void AssertSelf ()
		{
			if (!disposed)
				return;
			throw new ObjectDisposedException (nameof (AndroidClientHandler));
		}

		/// <summary>
		/// Returns a custom host name verifier for a HTTPS connection. By default it returns <c>null</c> and
		/// thus the connection uses whatever host name verification mechanism the operating system defaults to.
		/// Override in your class to define custom host name verification behavior. The overriding class should
		/// not set the <see cref="m:HttpsURLConnection.HostnameVerifier"/> property directly on the passed
		/// <paramref name="connection"/>
		/// </summary>
		/// <returns>Instance of IHostnameVerifier to be used for this HTTPS connection</returns>
		/// <param name="connection">HTTPS connection object.</param>
		protected virtual IHostnameVerifier? GetSSLHostnameVerifier (HttpsURLConnection connection)
		{
			return _underlyingHander.GetSSLHostnameVerifierInternal (connection);
		}

		/// <summary>
		/// Creates, configures and processes an asynchronous request to the indicated resource.
		/// </summary>
		/// <returns>Task in which the request is executed</returns>
		/// <param name="request">Request provided by <see cref="System.Net.Http.HttpClient"/></param>
		/// <param name="cancellationToken">Cancellation token.</param>
		protected override async Task <HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken cancellationToken)
		{
			AssertSelf ();
			return await base.SendAsync (request, cancellationToken);
		}

		protected virtual async Task <Java.Net.Proxy?> GetJavaProxy (Uri destination, CancellationToken cancellationToken)
		{
			return await _underlyingHander.GetJavaProxyInternal (destination, cancellationToken);
		}

		protected virtual async Task WriteRequestContentToOutput (HttpRequestMessage request, HttpURLConnection httpConnection, CancellationToken cancellationToken)
		{
			await _underlyingHander.WriteRequestContentToOutputInternal (request, httpConnection, cancellationToken);
		}

		/// <summary>
		/// Configure the <see cref="HttpURLConnection"/> before the request is sent. This method is meant to be overriden
		/// by applications which need to perform some extra configuration steps on the connection. It is called with all
		/// the request headers set, pre-authentication performed (if applicable) but before the request body is set
		/// (e.g. for POST requests). The default implementation in AndroidClientHandler does nothing.
		/// </summary>
		/// <param name="request">Request data</param>
		/// <param name="conn">Pre-configured connection instance</param>
		protected virtual Task SetupRequest (HttpRequestMessage request, HttpURLConnection conn)
		{
			return _underlyingHander.SetupRequestInternal (request, conn);
		}

		/// <summary>
		/// Configures the key store. The <paramref name="keyStore"/> parameter is set to instance of <see cref="KeyStore"/>
		/// created using the <see cref="KeyStore.DefaultType"/> type and with populated with certificates provided in the <see cref="TrustedCerts"/>
		/// property. AndroidClientHandler implementation simply returns the instance passed in the <paramref name="keyStore"/> parameter
		/// </summary>
		/// <returns>The key store.</returns>
		/// <param name="keyStore">Key store to configure.</param>
		protected virtual KeyStore? ConfigureKeyStore (KeyStore? keyStore)
		{
			AssertSelf ();

			return _underlyingHander.ConfigureKeyStoreInternal (keyStore);
		}

		/// <summary>
		/// Create and configure an instance of <see cref="KeyManagerFactory"/>. The <paramref name="keyStore"/> parameter is set to the
		/// return value of the <see cref="ConfigureKeyStore"/> method, so it might be null if the application overrode the method and provided
		/// no key store. It will not be <c>null</c> when the default implementation is used. The application can return <c>null</c> here since
		/// KeyManagerFactory is not required for the custom SSL configuration, but it might be used by the application to implement a more advanced
		/// mechanism of key management.
		/// </summary>
		/// <returns>The key manager factory or <c>null</c>.</returns>
		/// <param name="keyStore">Key store.</param>
		protected virtual KeyManagerFactory? ConfigureKeyManagerFactory (KeyStore? keyStore)
		{
			AssertSelf ();

			return _underlyingHander.ConfigureKeyManagerFactoryInternal (keyStore);
		}

		/// <summary>
		/// Create and configure an instance of <see cref="TrustManagerFactory"/>. The <paramref name="keyStore"/> parameter is set to the
		/// return value of the <see cref="ConfigureKeyStore"/> method, so it might be null if the application overrode the method and provided
		/// no key store. It will not be <c>null</c> when the default implementation is used. The application can return <c>null</c> from this
		/// method in which case AndroidClientHandler will create its own instance of the trust manager factory provided that the <see cref="TrustCerts"/>
		/// list contains at least one valid certificate. If there are no valid certificates and this method returns <c>null</c>, no custom
		/// trust manager will be created since that would make all the HTTPS requests fail.
		/// </summary>
		/// <returns>The trust manager factory.</returns>
		/// <param name="keyStore">Key store.</param>
		protected virtual TrustManagerFactory? ConfigureTrustManagerFactory (KeyStore? keyStore)
		{
			AssertSelf ();

			return _underlyingHander.ConfigureTrustManagerFactoryInternal (keyStore);
		}

		/// <summary>
		/// Configure and return a custom <see cref="t:SSLSocketFactory"/> for the passed HTTPS <paramref
		/// name="connection"/>. If the class overriding the method returns anything but the default
		/// <c>null</c>, the SSL setup code will not call the <see cref="ConfigureKeyManagerFactory"/> nor the
		/// <see cref="ConfigureTrustManagerFactory"/> methods used to configure a custom trust manager which is
		/// then used to create a default socket factory.
		/// Deriving class must perform all the key manager and trust manager configuration to ensure proper
		/// operation of the returned socket factory.
		/// </summary>
		/// <returns>Instance of SSLSocketFactory ready to use with the HTTPS connection.</returns>
		/// <param name="connection">HTTPS connection to return socket factory for</param>
		protected virtual SSLSocketFactory? ConfigureCustomSSLSocketFactory (HttpsURLConnection connection)
		{
			return _underlyingHander.ConfigureCustomSSLSocketFactoryInternal (connection);
		}

		[DynamicDependency (DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof (AndroidMessageHandler))]
		object? GetUnderlyingHandler ()
		{
			Logger.Log (LogLevel.Info, LOG_APP, "grendel: GetUnderlyingHandler()");
			var fieldName = "_nativeHandler";
			FieldInfo? field = null;

			for (var type = GetType (); type != null; type = type.BaseType) {
				Logger.Log (LogLevel.Info, LOG_APP, $"grendel: checking in type '{type.FullName}'");
				field = type.GetField (fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
				if (field != null) {
					Logger.Log (LogLevel.Info, LOG_APP, "grendel: found");
					break;
				}
			}

			if (field == null) {
				throw new InvalidOperationException ($"Field '{fieldName}' is missing from type '{GetType ()}'.");
			}

			object? ret = field.GetValue (this);
			Logger.Log (LogLevel.Info, LOG_APP, $"grendel: field '{fieldName}' value == '{ret}'");
			return ret;
		}
	}
}
