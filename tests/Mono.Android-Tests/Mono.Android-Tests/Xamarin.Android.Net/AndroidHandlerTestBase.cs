//
// HttpClientHandlerTestBase.cs
//
// Authors:
//	Marek Safar  <marek.safar@gmail.com>
//
// Copyright (C) 2011 Xamarin Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using Android.OS;

using NUnit.Framework;

namespace Xamarin.Android.NetTests {
	[Category ("InetAccess")]
	[Category ("SSL")] // TODO: https://github.com/dotnet/android/issues/10069
	public abstract class HttpClientHandlerTestBase
	{
		protected abstract HttpMessageHandler CreateHandler ();

		class Proxy : IWebProxy
		{
			public ICredentials Credentials {
				get {
					throw new NotImplementedException ();
				}
				set {
					throw new NotImplementedException ();
				}
			}

			public Uri GetProxy (Uri destination)
			{
				throw new NotImplementedException ();
			}

			public bool IsBypassed (Uri host)
			{
				throw new NotImplementedException ();
			}
		}

		[Test]
		public void Disposed ()
		{
			using var server = LocalHttpServer.Start ();
			var h = CreateHandler ();
			h.Dispose ();
			var c = new HttpClient (h);
			try {
				var t = c.GetAsync (server.OkUri);
				t.Wait ();
				Assert.Fail ("#1");
			} catch (AggregateException e) {
				Assert.IsTrue (e.InnerException is ObjectDisposedException, "#2");
			}
		}

		protected Task<HttpResponseMessage> ConnectIgnoreFailure (Func<Task<HttpResponseMessage>> connector, out bool connectionFailed)
		{
			connectionFailed = false;
			try {
				return connector ();
			} catch (Exception ex) when (IsConnectionFailure (ex)) {
				connectionFailed = true;
				return null;
			}
		}

		protected void RunIgnoringNetworkIssues (Action runner, out bool connectionFailed)
		{
			connectionFailed = false;
			try {
				runner ();
			} catch (Exception ex) when (IsConnectionFailure (ex)) {
				connectionFailed = true;
			}
		}

		protected bool IsConnectionFailure (Exception ex)
		{
			if (ex is AggregateException aex) {
				foreach (var inner in aex.Flatten ().InnerExceptions) {
					if (IsConnectionFailure (inner))
						return true;
				}
				return false;
			}

			var current = ex;
			while (current != null) {
				if (current is WebException wex) {
					switch (wex.Status) {
						case WebExceptionStatus.ConnectFailure:
						case WebExceptionStatus.NameResolutionFailure:
						case WebExceptionStatus.Timeout:
							return true;
					}
				}

				if (current is Java.Net.ConnectException)
					return true;

				if (current is Java.Net.SocketException socketEx) {
					var message = socketEx.Message ?? "";
					if (message.Contains ("Broken pipe", StringComparison.OrdinalIgnoreCase) ||
							message.Contains ("Connection reset", StringComparison.OrdinalIgnoreCase))
						return true;
				}

				if (current is System.Net.Sockets.SocketException)
					return true;

				current = current.InnerException;
			}

			return false;
		}
	}

	public abstract class AndroidHandlerTestBase : HttpClientHandlerTestBase
	{
		static IEnumerable<Exception> Exceptions (Exception e)
		{
			yield return e;
			for (var i = e.InnerException; i != null; i = i.InnerException) {
				yield return i;
			}
		}

		static bool IsSecureChannelFailure (Exception e)
		{
			return Exceptions (e).Any (v => (v as WebException)?.Status == WebExceptionStatus.SecureChannelFailure);
		}

		[Test]
		public void Cancel_Client_Works ()
		{
			var cts = new CancellationTokenSource ();
			cts.Cancel (); //Cancel immediately
			using (var c = new HttpClient (CreateHandler ())) {
				var tr = ConnectIgnoreFailure (() => c.GetAsync ("http://10.255.255.1", cts.Token), out bool connectionFailed);
				if (connectionFailed)
					return;

				try {
					RunIgnoringNetworkIssues (() => tr.Wait (), out connectionFailed);
					if (connectionFailed)
						return;

					Assert.Fail ("SHOULD NOT HAPPEN: Request is expected to cancel");
				}
				catch (AggregateException ex) {
					Assert.IsTrue (ex.InnerExceptions.Any (ie => ie is System.OperationCanceledException), "Request did not throw cancellation exception; threw: {0}", ex);
					Assert.IsTrue (cts.IsCancellationRequested, "The request was canceled before cancellation was requested");
				}
			}
		}

		[Test]
		public void Token_Timeout_Works ()
		{
			var cts = new CancellationTokenSource (2000); //Cancel after 2000ms through token
			using (var c = new HttpClient (CreateHandler ())) {
				var tr = ConnectIgnoreFailure (() => c.GetAsync ("http://10.255.255.1", cts.Token), out bool connectionFailed);
				if (connectionFailed)
					return;

				try {
					RunIgnoringNetworkIssues (() => tr.Wait (), out connectionFailed);
					if (connectionFailed)
						return;

					Assert.Fail ("SHOULD NOT HAPPEN: Request is expected to cancel");
				}
				catch (AggregateException ex) {
					Assert.IsTrue (ex.InnerExceptions.Any (ie => ie is System.OperationCanceledException), "Request did not throw cancellation exception; threw: {0}", ex);
					Assert.IsTrue (cts.IsCancellationRequested, "The request was canceled before cancellation was requested");
				}
			}
		}

		[Test]
		public void Property_Timeout_Works ()
		{
			using (var c = new HttpClient (CreateHandler ()))
			{
				c.Timeout = TimeSpan.FromMilliseconds (2000); //Cancel after 2000ms through Timeout property
				var tr = ConnectIgnoreFailure (() => c.GetAsync ("http://10.255.255.1"), out bool connectionFailed);
				if (connectionFailed)
					return;

				try {
					RunIgnoringNetworkIssues (() => tr.Wait (), out connectionFailed);
					if (connectionFailed)
						return;

					Assert.Fail ("SHOULD NOT HAPPEN: Request is expected to cancel");
				}
				catch (AggregateException ex)
				{
					Assert.IsTrue (ex.InnerExceptions.Any (ie => ie is System.OperationCanceledException), "Request did not throw cancellation exception; threw: {0}", ex);
				}
			}
		}

		[Test]
		public void Redirect_Without_Protocol_Works ()
		{
			using var server = LocalHttpServer.Start ();
			var requestURI = $"/redirect-to?url={Uri.EscapeDataString (server.OkUri.ToString ())}";
			var redirectedURI = server.OkUri;
			using (var c = new HttpClient (CreateHandler ())) {
				c.BaseAddress = server.Uri;
				var tr = ConnectIgnoreFailure (() => c.GetAsync (requestURI), out bool connectionFailed);
				if (connectionFailed)
					return;

				RunIgnoringNetworkIssues (() => tr.Wait (), out connectionFailed);
				if (connectionFailed)
					return;

				EnsureSuccessStatusCode (tr.Result);
				Assert.AreEqual (redirectedURI, tr.Result.RequestMessage.RequestUri, "Invalid redirected URI");
			}
			server.AssertNoUnhandledExceptions ();
		}

		[Test]
		public void Redirect_POST_With_Content_Works ()
		{
			using var server = LocalHttpServer.Start ();
			var requestURI = $"/redirect-to?url={Uri.EscapeDataString (server.OkUri.ToString ())}";
			var redirectedURI = server.OkUri;
			using (var c = new HttpClient (CreateHandler ())) {
				c.BaseAddress = server.Uri;
				var request = new HttpRequestMessage (HttpMethod.Post, requestURI);
				request.Content = new StringContent ("{}", Encoding.UTF8, "application/json");
				var t = ConnectIgnoreFailure (() => c.SendAsync (request), out bool connectionFailed);
				if (connectionFailed)
					return;

				HttpResponseMessage response = null;
				RunIgnoringNetworkIssues (() => response = t.Result, out connectionFailed);
				if (connectionFailed)
					return;

				EnsureSuccessStatusCode (response);
				Assert.AreEqual (redirectedURI, response.RequestMessage.RequestUri, "Invalid redirected URI");
			}
			server.AssertNoUnhandledExceptions ();
		}

		public void EnsureSuccessStatusCode (HttpResponseMessage response)
		{
			// These status codes all indicate a temporary network/server failure,
			// so just ignore the test if we hit them.
			if (ShouldIgnoreSuccessStatusCode (response.StatusCode)) {
				Assert.Ignore ($"Ignoring network/server failure: {response.StatusCode}");
				return;
			}

			response.EnsureSuccessStatusCode ();
		}

		public bool ShouldIgnoreSuccessStatusCode (HttpStatusCode code)
		{
			// These status codes all indicate a temporary network/server failure,
			// so just ignore the test if we hit them.
			switch (code) {
				case HttpStatusCode.InternalServerError:
				case HttpStatusCode.BadGateway:
				case HttpStatusCode.ServiceUnavailable:
				case HttpStatusCode.GatewayTimeout:
					return true;
			}

			return false;
		}
	}
}
