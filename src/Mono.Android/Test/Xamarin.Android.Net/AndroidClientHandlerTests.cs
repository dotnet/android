//
// HttpClientHandlerTest.cs
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

using NUnit.Framework;

using Android.OS;

namespace Xamarin.Android.NetTests {
	[Category("InetAccess")]
	public abstract class HttpClientHandlerTestBase
	{
		protected abstract HttpClientHandler CreateHandler ();

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
		public void Properties_Defaults ()
		{
			var h = CreateHandler ();
			Assert.IsTrue (h.AllowAutoRedirect, "#1");
			Assert.AreEqual (DecompressionMethods.None, h.AutomaticDecompression, "#2");
			Assert.AreEqual (0, h.CookieContainer.Count, "#3");
			Assert.AreEqual (4096, h.CookieContainer.MaxCookieSize, "#3b");
			Assert.AreEqual (null, h.Credentials, "#4");
			Assert.AreEqual (50, h.MaxAutomaticRedirections, "#5");
			Assert.AreEqual (int.MaxValue, h.MaxRequestContentBufferSize, "#6");
			Assert.IsFalse (h.PreAuthenticate, "#7");
			Assert.IsNull (h.Proxy, "#8");
			Assert.IsTrue (h.SupportsAutomaticDecompression, "#9");
			Assert.IsTrue (h.SupportsProxy, "#10");
			Assert.IsTrue (h.SupportsRedirectConfiguration, "#11");
			Assert.IsTrue (h.UseCookies, "#12");
			Assert.IsFalse (h.UseDefaultCredentials, "#13");
			Assert.IsTrue (h.UseProxy, "#14");
			Assert.AreEqual (ClientCertificateOption.Manual, h.ClientCertificateOptions, "#15");
		}

		[Test]
		public void Properties_Invalid ()
		{
			var h = CreateHandler ();
			try {
				h.MaxAutomaticRedirections = 0;
				Assert.Fail ("#1");
			} catch (ArgumentOutOfRangeException) {
			}

			try {
				h.MaxRequestContentBufferSize = -1;
				Assert.Fail ("#2");
			} catch (ArgumentOutOfRangeException) {
			}

			h.UseProxy = false;
			try {
				h.Proxy = new Proxy ();
				Assert.Fail ("#3");
			} catch (InvalidOperationException) {
			}
		}

		[Test]
		public void Properties_AfterClientCreation ()
		{
			var h = CreateHandler ();
			h.AllowAutoRedirect = true;

			// We may modify properties after creating the HttpClient.
			using (var c = new HttpClient (h, true)) {
				h.AllowAutoRedirect = false;
			}
		}

		[Test]
		public void Disposed ()
		{
			var h = CreateHandler ();
			h.Dispose ();
			var c = new HttpClient (h);
			try {
				var t = ConnectIgnoreFailure (() => c.GetAsync ("http://google.com"), out bool connectionFailed);
				if (connectionFailed)
					return;

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
			} catch (AggregateException ex) {
				if (IgnoreIfConnectionFailed (ex.InnerException as WebException, out connectionFailed))
					return null;
				throw;
			}
		}

		protected void RunIgnoringNetworkIssues (Action runner, out bool connectionFailed)
		{
			connectionFailed = false;
			try {
				runner ();
			} catch (AggregateException ex) {
				if (IgnoreIfConnectionFailed (ex.InnerException as WebException, out connectionFailed))
					return;
				throw;
			}
		}

		bool IgnoreIfConnectionFailed (WebException wex, out bool connectionFailed)
		{
			connectionFailed = false;
			if (wex == null)
				return false;

			if (wex.Status != WebExceptionStatus.ConnectFailure)
				return false;

			connectionFailed = true;
			Assert.Ignore ($"Failed to connect to server. {wex}");
			return true;
		}
	}

	[TestFixture]
	public class AndroidClientHandlerTests : HttpClientHandlerTestBase
	{
		const string Tls_1_2_Url = "https://tls-test.internalx.com";

		protected override HttpClientHandler CreateHandler ()
		{
			return new Xamarin.Android.Net.AndroidClientHandler ();
		}

		[Test]
		public void Tls_1_2_Url_Works ()
		{
			if (((int) Build.VERSION.SdkInt) < 16) {
				Assert.Ignore ("Host platform doesn't support TLS 1.2.");
				return;
			}
			using (var c = new HttpClient (CreateHandler ())) {
				var tr = ConnectIgnoreFailure (() => c.GetAsync (Tls_1_2_Url), out bool connectionFailed);
				if (connectionFailed)
					return;

				RunIgnoringNetworkIssues (() => tr.Wait (), out connectionFailed);
				if (connectionFailed)
					return;

				tr.Result.EnsureSuccessStatusCode ();
			}
		}

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
		public void Sanity_Tls_1_2_Url_WithMonoClientHandlerFails ()
		{
			var tlsProvider   = global::System.Environment.GetEnvironmentVariable ("XA_TLS_PROVIDER");
			var supportTls1_2 = tlsProvider.Equals ("btls", StringComparison.OrdinalIgnoreCase);
			using (var c = new HttpClient (new HttpClientHandler ())) {
				try {
					var tr = ConnectIgnoreFailure (() => c.GetAsync (Tls_1_2_Url), out bool connectionFailed);
					if (connectionFailed)
						return;

					RunIgnoringNetworkIssues (() => tr.Wait (), out connectionFailed);
					if (connectionFailed)
						return;

					tr.Result.EnsureSuccessStatusCode ();
					if (!supportTls1_2) {
						Assert.Fail ("SHOULD NOT BE REACHED: Mono's HttpClientHandler doesn't support TLS 1.2.");
					}
				}
				catch (AggregateException e) {
					if (supportTls1_2) {
						Assert.Fail ("SHOULD NOT BE REACHED: BTLS is present, TLS 1.2 should work. Network error? {0}", e.ToString ());
					}
					if (!supportTls1_2) {
						Assert.IsTrue (IsSecureChannelFailure (e),
							       "Nested exception and/or corresponding status code did not match expected results for TLS 1.2 incompatibility {0}",
							       e);
					}
				}
			}
		}

		[Test]
		public void Cancel_Client_Works()
		{
			var cts = new CancellationTokenSource ();
			cts.Cancel (); //Cancel immediately
			using (var c = new HttpClient (CreateHandler())) {
				var tr = ConnectIgnoreFailure (() => c.GetAsync ("http://10.255.255.1", cts.Token), out bool connectionFailed);
				if (connectionFailed)
					return;

				try {
					RunIgnoringNetworkIssues (() => tr.Wait(), out connectionFailed);
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
		public void Token_Timeout_Works()
		{
			var cts = new CancellationTokenSource (2000); //Cancel after 2000ms through token
			using (var c = new HttpClient (CreateHandler())){
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
					Assert.IsTrue (ex.InnerExceptions.Any(ie => ie is System.OperationCanceledException), "Request did not throw cancellation exception; threw: {0}", ex);
					Assert.IsTrue (cts.IsCancellationRequested, "The request was canceled before cancellation was requested");
				}
			}
		}

		[Test]
		public void Property_Timeout_Works()
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
		public void Redirect_Without_Protocol_Works()
		{
			var requestURI = new Uri ("http://tls-test.internalx.com/redirect.php");
			var redirectedURI = new Uri ("http://tls-test.internalx.com/redirect-301.html");
			using (var c = new HttpClient (CreateHandler ())) {
				var tr = ConnectIgnoreFailure (() => c.GetAsync (requestURI), out bool connectionFailed);
				if (connectionFailed)
					return;

				RunIgnoringNetworkIssues (() => tr.Wait (), out connectionFailed);
				if (connectionFailed)
					return;

				tr.Result.EnsureSuccessStatusCode ();
				Assert.AreEqual (redirectedURI, tr.Result.RequestMessage.RequestUri, "Invalid redirected URI");
			}
		}

		[Test]
		public void Redirect_POST_With_Content_Works ()
		{
			var requestURI = new Uri ("http://tls-test.internalx.com/redirect.php");
			var redirectedURI = new Uri ("http://tls-test.internalx.com/redirect-301.html");
			using (var c = new HttpClient (CreateHandler ())) {
				var request = new HttpRequestMessage (HttpMethod.Post, requestURI);
				request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
				var t = ConnectIgnoreFailure (() => c.SendAsync(request), out bool connectionFailed);
				if (connectionFailed)
					return;

				HttpResponseMessage response = null;
				RunIgnoringNetworkIssues (() => response = t.Result, out connectionFailed);
				if (connectionFailed)
					return;

				response.EnsureSuccessStatusCode ();
				Assert.AreEqual (redirectedURI, response.RequestMessage.RequestUri, "Invalid redirected URI");
			}
		}
	}
}
