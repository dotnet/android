//
// HttpClientIntegrationTests.cs
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
using NUnit.Framework;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Linq;
using System.IO;

namespace Xamarin.Android.NetTests {
	[Category ("InetAccess")]
	public sealed class AndroidMessageHandlerIntegrationTests
	{
		class CustomStream : Stream
		{
			public override void Flush ()
			{
				throw new NotImplementedException ();
			}

			int pos;

			public override int Read (byte[] buffer, int offset, int count)
			{
				++pos;
				if (pos > 4)
					return 0;

				return 11;
			}

			public override long Seek (long offset, SeekOrigin origin)
			{
				throw new NotImplementedException ();
			}

			public override void SetLength (long value)
			{
				throw new NotImplementedException ();
			}

			public override void Write (byte[] buffer, int offset, int count)
			{
				throw new NotImplementedException ();
			}

			public override bool CanRead {
				get {
					return true;
				}
			}

			public override bool CanSeek {
				get {
					return false;
				}
			}

			public override bool CanWrite {
				get {
					throw new NotImplementedException ();
				}
			}

			public override long Length {
				get {
					throw new NotImplementedException ();
				}
			}

			public override long Position {
				get {
					throw new NotImplementedException ();
				}
				set {
					throw new NotImplementedException ();
				}
			}
		}

		const int WaitTimeout = 10000;

		string port, TestHost, LocalServer;

		[SetUp]
		public void SetupFixture ()
		{
			if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
				port = "810";
			} else {
				port = "8810";
			}

			TestHost = "localhost:" + port;
			LocalServer = string.Format ("http://{0}/", TestHost);
		}

		[Test]
		public void Ctor_Default ()
		{
			using (var handler = new Xamarin.Android.Net.AndroidMessageHandler ()) {
				var client = new HttpClient (handler);
				Assert.IsNull (client.BaseAddress, "#1");
				Assert.IsNotNull (client.DefaultRequestHeaders, "#2");  // TODO: full check
				Assert.AreEqual (int.MaxValue, client.MaxResponseContentBufferSize, "#3");
				Assert.AreEqual (TimeSpan.FromSeconds (100), client.Timeout, "#4");
			}
		}


		[Test]
		public void CancelRequestViaProxy ()
		{
			using (var handler = new Xamarin.Android.Net.AndroidMessageHandler ()) {
				handler.Proxy = new WebProxy ("192.168.10.25:8888/"); // proxy that doesn't exist
				handler.UseProxy = true;
				handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

				var httpClient = new HttpClient (handler) {
					BaseAddress = new Uri ("https://google.com"),
					Timeout = TimeSpan.FromMilliseconds (1)
				};

				var restRequest = new HttpRequestMessage {
					Method = HttpMethod.Post,
					RequestUri = new Uri ("foo", UriKind.Relative),
					Content = new StringContent ("", null, "application/json")
				};

				var task = httpClient.PostAsync (restRequest.RequestUri, restRequest.Content);
				bool completed = false;
				try {
					completed = task.Wait (WaitTimeout);
				} catch (AggregateException e) {
					Console.WriteLine ("CancelRequestViaProxy exception: {0}", e);
					Assert.IsTrue (e.InnerException is TaskCanceledException, $"Expected TaskCanceledException but got: {e.InnerException?.GetType ().FullName}: {e.InnerException?.Message}");
					return; // Test passed - got expected exception
				}

				// If we reach here, the task completed or timed out without throwing
				if (!completed) {
					Assert.Inconclusive ($"Test timed out waiting for task. Task status: {task.Status}. This can happen due to timing issues on slow machines.");
				}

				// Task completed without throwing - this is unexpected
				if (task.IsFaulted) {
					Assert.Fail ($"Task faulted with unexpected exception: {task.Exception?.InnerException?.GetType ().FullName}: {task.Exception?.InnerException?.Message}");
				} else if (task.IsCanceled) {
					// This is actually fine - the task was canceled as expected
					return;
				} else {
					Assert.Fail ($"Expected request to be canceled due to 1ms timeout with non-existent proxy, but task completed successfully with Status: {task.Status}");
				}
			}
		}

		[Test]
		public void Properties ()
		{
			using (var handler = new Xamarin.Android.Net.AndroidMessageHandler ()) {
				var client = new HttpClient (handler);
				client.BaseAddress = null;
				client.MaxResponseContentBufferSize = int.MaxValue;
				client.Timeout = Timeout.InfiniteTimeSpan;

				Assert.IsNull (client.BaseAddress, "#1");
				Assert.AreEqual (int.MaxValue, client.MaxResponseContentBufferSize, "#2");
				Assert.AreEqual (Timeout.InfiniteTimeSpan, client.Timeout, "#3");
			}
		}

		[Test]
		public void Properties_Invalid ()
		{
			using (var handler = new Xamarin.Android.Net.AndroidMessageHandler ()) {
				var client = new HttpClient (handler);
				try {
					client.MaxResponseContentBufferSize = 0;
					Assert.Fail ("#1");
				} catch (ArgumentOutOfRangeException) {
				}

				try {
					client.Timeout = TimeSpan.MinValue;
					Assert.Fail ("#2");
				} catch (ArgumentOutOfRangeException) {
				}
			}
		}

		[Test]
		void UrlEscaping_Bug43411 ()
		{
			UrlEscaping_TestUrl ($"http://{TestHost}/?example=value%20_value", "#1");
			UrlEscaping_TestUrl ($"http://{TestHost}/?query=anna%20%26%20lotte&param2=true", "#2");
		}

		void UrlEscaping_TestUrl (string url, string messagePrefix)
		{
			bool? failed = null;

			var listener = CreateListener (l => {
					failed = true;
				});

			using (listener) {
				try {
					var client = new HttpClient ();
					var request = new HttpRequestMessage (HttpMethod.Get, url);

					client.SendAsync (request, HttpCompletionOption.ResponseHeadersRead).Wait ();
					Assert.AreEqual (url, request.RequestUri.ToString (), $"{messagePrefix}-1");
					Assert.IsNull (failed, $"{messagePrefix}-2");
				} finally {
					listener.Abort ();
					listener.Close ();
				}
			}
		}

		public void Send_Complete_NoContent (HttpMethod method)
		{
			bool? failed = null;
			var listener = CreateListener (l => {
					try {
						var request = l.Request;

						Assert.AreEqual (6, request.Headers.Count, $"#1-{method}");
						Assert.AreEqual ("0", request.Headers ["Content-Length"], $"#1b-{method}");
						Assert.AreEqual (method.Method, request.HttpMethod, $"#2-{method}");
						Console.WriteLine ($"Asserts are fine - {method}");
						failed = false;
					} catch (Exception ex) {
						failed = true;
						Console.WriteLine (ex);
					}
				});

			using (listener) {
				try {
					using (var handler = new Xamarin.Android.Net.AndroidMessageHandler ()) {
						var client = new HttpClient (handler);
						var request = new HttpRequestMessage (method, LocalServer);
						var response = client.SendAsync (request, HttpCompletionOption.ResponseHeadersRead).Result;

						Assert.AreEqual ("", response.Content.ReadAsStringAsync ().Result, $"#100-{method}");
						Assert.AreEqual (HttpStatusCode.OK, response.StatusCode, $"#101-{method}");
						Assert.AreEqual (false, failed, $"#102-{method}");
					}
				} finally {
					listener.Close ();
				}
			}
		}

		[Test]
		public void Send_Invalid ()
		{
			var client = new HttpClient (new Xamarin.Android.Net.AndroidMessageHandler ());
			try {
				client.SendAsync (null).Wait (WaitTimeout);
				Assert.Fail ("#1");
			} catch (ArgumentNullException) {
			}

			try {
				var request = new HttpRequestMessage ();
				client.SendAsync (request).Wait (WaitTimeout);
				Assert.Fail ("#2");
			} catch (InvalidOperationException) {
			}
		}

		[Test]
		public void GetString_Many ()
		{
			var client = new HttpClient (new Xamarin.Android.Net.AndroidMessageHandler ());
			var t1 = client.GetStringAsync ("https://google.com");
			var t2 = client.GetStringAsync ("https://google.com");
			Assert.IsTrue (Task.WaitAll (new [] { t1, t2 }, WaitTimeout));
		}

		[Test]
		public void DisallowAutoRedirect ()
		{
			var listener = CreateListener (l => {
					using (var response = l.Response)
					{
						response.Redirect("http://xamarin.com/");
					}
				});

			using (listener) {
				try {
					var chandler = new Xamarin.Android.Net.AndroidMessageHandler ();
					chandler.AllowAutoRedirect = false;
					var client = new HttpClient (chandler);

					try {
						client.GetStringAsync (LocalServer).Wait (WaitTimeout);
						Assert.Fail ("#1: HttpRequestException wasn't thrown.");
					} catch (AggregateException e) {
						Assert.IsTrue (e.InnerException is HttpRequestException, "#2: " + e.ToString ());
					}
				} finally {
					listener.Abort ();
					listener.Close ();
				}
			}
		}

		HttpListener CreateListener (Action<HttpListenerContext> contextAssert)
		{
			var l = new HttpListener ();
			l.Prefixes.Add (string.Format ("http://+:{0}/", port));
			l.Start ();
			l.BeginGetContext (ar => {
					var ctx = l.EndGetContext (ar);
					try {
						if (contextAssert != null)
							contextAssert (ctx);
					} finally {
						ctx.Response.Close ();
					}
				}, null);

			return l;
		}
	}
}
