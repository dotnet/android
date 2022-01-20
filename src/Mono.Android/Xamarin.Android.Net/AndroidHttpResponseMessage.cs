using System.Collections.Generic;
using System.Net.Http;

using Java.Net;

namespace Xamarin.Android.Net
{
	/// <summary>
#if MONOANDROID1_0
	/// A convenience wrapper around <see cref="System.Net.Http.HttpResponseMessage"/> returned by <see cref="AndroidClientHandler.SendAsync"/>
	/// that allows easy access to authentication data as returned by the server, if any.
#else
	/// A convenience wrapper around <see cref="System.Net.Http.HttpResponseMessage"/> returned by <see cref="AndroidMessageHandler.SendAsync"/>
	/// that allows easy access to authentication data as returned by the server, if any.
#endif
	/// </summary>
	public class AndroidHttpResponseMessage : HttpResponseMessage
	{
		URL? javaUrl;
		HttpURLConnection? httpConnection;

		/// <summary>
#if MONOANDROID1_0
		/// Set to the same value as <see cref="AndroidClientHandler.RequestedAuthentication"/>.
#else
		/// Set to the same value as <see cref="AndroidMessageHandler.RequestedAuthentication"/>.
#endif
		/// </summary>
		/// <value>The requested authentication.</value>
		public IList <AuthenticationData>? RequestedAuthentication { get; internal set; }

		/// <summary>
#if MONOANDROID1_0
		/// Set to the same value as <see cref="AndroidClientHandler.RequestNeedsAuthorization"/>
#else
		/// Set to the same value as <see cref="AndroidMessageHandler.RequestNeedsAuthorization"/>
#endif
		/// </summary>
		/// <value>The request needs authorization.</value>
		public bool RequestNeedsAuthorization {
			get { return RequestedAuthentication?.Count > 0; }
		}

		public AndroidHttpResponseMessage ()
		{}

		public AndroidHttpResponseMessage (URL javaUrl, HttpURLConnection httpConnection) 
		{
			this.javaUrl = javaUrl;
			this.httpConnection = httpConnection;
		}

		protected override void Dispose (bool disposing)
		{
			base.Dispose(disposing);

			if (javaUrl != null) {
				javaUrl.Dispose ();
			}

			if (httpConnection != null) {
				httpConnection.Dispose ();
			}
		}
	}
}
