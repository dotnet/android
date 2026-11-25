using System;
using System.Collections.Generic;
using System.Net.Http;

using Android.Runtime;
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
			// Dispose the content first (base.Dispose). For a streaming response the content is a
			// CancellationAwareResponseStream, which safely requests that any in-flight read be aborted and
			// defers closing the underlying stream until that read unwinds (see
			// AndroidMessageHandler.CancellationAwareResponseStream).
			base.Dispose (disposing);

			if (javaUrl != null) {
				javaUrl.Dispose ();
			}

			if (httpConnection != null) {
				// Release the connection with Disconnect(), never Dispose(), on the Java peer:
				//  * Disconnect() closes the socket and aborts any in-flight read. It is the backstop for
				//    non-streaming responses (e.g. the buffered StringContent error paths) whose content does
				//    not own the connection. For a streaming response, base.Dispose() either disconnected
				//    directly or queued a disconnect; this idempotent call ensures a parked read is aborted
				//    before Dispose() returns.
				//  * Disposing the peer (deleting its JNI global reference) could race a body read still
				//    unwinding on another thread and crash; the peer is reclaimed on finalization.
				// This backstop runs synchronously on the disposing thread, which may be the UI thread.
				// Disconnect() closes the socket without waiting for the body operation to unwind and, unlike
				// connect/read/write, does not trigger NetworkOnMainThreadException.
				try {
					httpConnection.Disconnect ();
				} catch (Exception ex) {
					Logger.Log (LogLevel.Info, AndroidMessageHandler.LOG_APP, $"Disconnection exception: {ex}");
				}
			}
		}
	}
}
