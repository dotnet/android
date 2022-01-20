using System.Net;

using Java.Net;

namespace Xamarin.Android.Net
{
	/// <summary>
#if MONOANDROID1_0
	/// Implement this interface in order to provide support for HTTP authentication scheme not supported by <see cref="AndroidClientHandler"/>
#else
	/// Implement this interface in order to provide support for HTTP authentication scheme not supported by <see cref="AndroidMessageHandler"/>
#endif
	/// </summary>
	public interface IAndroidAuthenticationModule
	{
		/// <summary>
#if MONOANDROID1_0
		/// The authentication scheme supported by the implementation. Should be set to <c>AuthenticationScheme.Unsupported</c> for
		/// schemes unsupported by <see cref="AndroidClientHandler"/> natively.
#else
		/// The authentication scheme supported by the implementation. Should be set to <c>AuthenticationScheme.Unsupported</c> for
		/// schemes unsupported by <see cref="AndroidMessageHandler"/> natively.
#endif
		/// </summary>
		/// <value>The scheme.</value>
		AuthenticationScheme Scheme { get; }

		/// <summary>
		/// Name of the authentication scheme, as sent in the WWW-Authenticate HTTP header (the very first verb in the header's value)
		/// </summary>
		/// <value>The type of the authentication.</value>
		string AuthenticationType { get; }

		/// <summary>
		/// Whether the implementation supports pre-authentication
		/// </summary>
		/// <value>The can pre authenticate.</value>
		bool CanPreAuthenticate { get; }

		/// <summary>
#if MONOANDROID1_0
		/// Authenticate using the specified challenge, request and credentials. This is currently not used by <see cref="AndroidClientHandler"/>
		/// since the requests aren't restarted automatically, but it can be used in the future implementations of <see cref="AndroidClientHandler"/>
#else
		/// Authenticate using the specified challenge, request and credentials. This is currently not used by <see cref="AndroidMessageHandler"/>
		/// since the requests aren't restarted automatically, but it can be used in the future implementations of <see cref="AndroidMessageHandler"/>
#endif
		/// </summary>
		/// <returns><see cref="Authorization"/> instance which contains the value of the response header to authorize the connection</returns>
		/// <param name="challenge">Challenge.</param>
		/// <param name="request">Request.</param>
		/// <param name="credentials">Credentials.</param>
		Authorization Authenticate (string challenge, HttpURLConnection request, ICredentials credentials);

		/// <summary>
		/// Pre-authenticate using the specified credentials.
		/// </summary>
		/// <returns><see cref="Authorization"/> instance which contains the value of the response header to authorize the connection</returns>
		/// <param name="request">Request.</param>
		/// <param name="credentials">Credentials.</param>
		Authorization PreAuthenticate (HttpURLConnection request, ICredentials credentials);
	}
}
