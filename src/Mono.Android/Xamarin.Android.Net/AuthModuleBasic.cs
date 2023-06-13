using System;
using System.Net;
using System.Text;

using Java.Net;

namespace Xamarin.Android.Net
{
	sealed class AuthModuleBasic : IAndroidAuthenticationModule
	{
		public AuthenticationScheme Scheme { get; } = AuthenticationScheme.Basic;
		public string AuthenticationType { get; } = "Basic";
		public bool CanPreAuthenticate { get; } = true;

		public Authorization? Authenticate (string challenge, HttpURLConnection request, ICredentials credentials)
		{
			var header = challenge?.Trim ();
			if (credentials == null || String.IsNullOrEmpty (header))
				return null;

			if (header?.IndexOf ("basic", StringComparison.OrdinalIgnoreCase) == -1)
				return null;

                        return InternalAuthenticate (request, credentials);
		}
		
		public Authorization? PreAuthenticate (HttpURLConnection request, ICredentials credentials)
		{
			return InternalAuthenticate (request, credentials);
		}

		Authorization? InternalAuthenticate (HttpURLConnection request, ICredentials credentials)
                {
			if (request == null || credentials == null)
				return null;

			NetworkCredential? cred = credentials.GetCredential (new Uri (request.URL?.ToString ()!), AuthenticationType.ToLowerInvariant ());
			if (cred == null)
				return null;

			if (String.IsNullOrEmpty (cred.UserName))
				return null;

			var domain = cred.Domain?.Trim ();
			string response = String.Empty;

			// If domain is set, MS sends "domain\user:password".
			if (!String.IsNullOrEmpty (domain))
				response = domain + "\\";
			response += cred.UserName + ":" + cred.Password;

			return new Authorization ($"{AuthenticationType} {Convert.ToBase64String (Encoding.ASCII.GetBytes (response))}");
                }
	}
}
