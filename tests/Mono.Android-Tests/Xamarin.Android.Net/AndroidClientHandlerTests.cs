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
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using NUnit.Framework;

using Android.OS;
using Xamarin.Android.Net;

namespace Xamarin.Android.NetTests {
	[Category("InetAccess")]
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
				if (IgnoreIfConnectionFailed (ex, out connectionFailed))
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
				if (IgnoreIfConnectionFailed (ex, out connectionFailed))
					return;
				throw;
			}
		}

		bool IgnoreIfConnectionFailed (AggregateException aex, out bool connectionFailed)
		{
			if (IgnoreIfConnectionFailed (aex.InnerException as HttpRequestException, out connectionFailed))
				return true;

			return IgnoreIfConnectionFailed (aex.InnerException as WebException, out connectionFailed);
		}

		bool IgnoreIfConnectionFailed (HttpRequestException hrex, out bool connectionFailed)
		{
			return IgnoreIfConnectionFailed (hrex?.InnerException as WebException, out connectionFailed);
		}

		bool IgnoreIfConnectionFailed (WebException wex, out bool connectionFailed)
		{
			connectionFailed = false;
			if (wex == null)
				return false;

			switch (wex.Status) {
				case WebExceptionStatus.ConnectFailure:
				case WebExceptionStatus.NameResolutionFailure:
				case WebExceptionStatus.Timeout:
					connectionFailed = true;
					Assert.Ignore ($"Ignoring network failure: {wex}");
					return true;
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

		[UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Tests private fields are preserved by other means")]
		static Type GetInnerHandlerType (HttpClient httpClient)
		{
			BindingFlags bflasgs = BindingFlags.Instance | BindingFlags.NonPublic;
			FieldInfo handlerField = typeof (HttpMessageInvoker).GetField("_handler", bflasgs);
			Assert.IsNotNull (handlerField);
			object handler = handlerField.GetValue (httpClient);
			FieldInfo innerHandlerField = handler.GetType ().GetField ("_delegatingHandler", bflasgs);
			Assert.IsNotNull (handlerField);
			object innerHandler = innerHandlerField.GetValue (handler);
			return innerHandler.GetType ();
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
			var requestURI = new Uri ("https://httpbin.org/redirect-to?url=https://github.com/xamarin/xamarin-android");
			var redirectedURI = new Uri ("https://github.com/xamarin/xamarin-android");
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
			var requestURI = new Uri ("https://httpbin.org/redirect-to?url=https://github.com/xamarin/xamarin-android");
			var redirectedURI = new Uri ("https://github.com/xamarin/xamarin-android");
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

	[TestFixture]
	public class AndroidClientHandlerTests : AndroidHandlerTestBase
	{
		protected override HttpMessageHandler CreateHandler ()
		{
			return new AndroidClientHandler ();
		}

		[Test]
		public void Properties_Defaults ()
		{
			var h = new AndroidClientHandler ();

			Assert.IsTrue (h.AllowAutoRedirect, "#1");
			Assert.AreEqual (DecompressionMethods.None, h.AutomaticDecompression, "#2");
			Assert.AreEqual (0, h.CookieContainer.Count, "#3");
			Assert.AreEqual (4096, h.CookieContainer.MaxCookieSize, "#3b");
			Assert.AreEqual (null, h.Credentials, "#4");
			Assert.AreEqual (50, h.MaxAutomaticRedirections, "#5");
			Assert.IsFalse (h.PreAuthenticate, "#7");
			Assert.IsNull (h.Proxy, "#8");
			Assert.IsTrue (h.SupportsAutomaticDecompression, "#9");
			Assert.IsTrue (h.SupportsProxy, "#10");
			Assert.IsTrue (h.SupportsRedirectConfiguration, "#11");
			Assert.IsTrue (h.UseCookies, "#12");
			Assert.IsFalse (h.UseDefaultCredentials, "#13");
			Assert.IsTrue (h.UseProxy, "#14");
			Assert.AreEqual (ClientCertificateOption.Manual, h.ClientCertificateOptions, "#15");
			Assert.IsNull (h.ServerCertificateCustomValidationCallback, "#16");
		}

		[Test]
		public void Properties_Invalid ()
		{
			var h = new AndroidClientHandler ();

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
		}

		[Test]
		public void Properties_AfterClientCreation ()
		{
			var h = new AndroidClientHandler ();

			h.AllowAutoRedirect = true;

			// We may modify properties after creating the HttpClient.
			using (var c = new HttpClient (h, true)) {
				h.AllowAutoRedirect = false;
			}
		}
	}
}
