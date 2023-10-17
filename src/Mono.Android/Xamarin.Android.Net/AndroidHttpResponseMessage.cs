using System.Collections.Generic;
using System.Net.Http;

using Java.Net;

namespace Xamarin.Android.Net
{
	/// <summary>
	/// A convenience wrapper around <see cref="System.Net.Http.HttpResponseMessage"/> returned by <see cref="AndroidMessageHandler.SendAsync"/>
	/// that allows easy access to authentication data as returned by the server, if any.
	/// </summary>
	public class AndroidHttpResponseMessage : HttpResponseMessage
	{
		URL? javaUrl;
		HttpURLConnection? httpConnection;

		/// <summary>
		/// Set to the same value as <see cref="AndroidMessageHandler.RequestedAuthentication"/>.
		/// </summary>
		/// <value>The requested authentication.</value>
		public IList <AuthenticationData>? RequestedAuthentication { get; internal set; }

		/// <summary>
		/// Set to the same value as <see cref="AndroidMessageHandler.RequestNeedsAuthorization"/>
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
