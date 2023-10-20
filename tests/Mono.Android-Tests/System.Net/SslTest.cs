using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace System.NetTests {

	[TestFixture, Category ("InetAccess")]
	public class SslTest
	{
		bool ShouldIgnoreException (WebException wex)
		{
			switch (wex.Status) {
				case WebExceptionStatus.ConnectFailure:
				case WebExceptionStatus.NameResolutionFailure:
				case WebExceptionStatus.Timeout:
					return true;
			}

			return false;
		}

		// https://xamarin.desk.com/agent/case/35534
		[Test]
		public void SslWithinTasksShouldWork ()
		{
			var cb = ServicePointManager.ServerCertificateValidationCallback;
			ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, policy) => {
					Console.WriteLine ("# ServerCertificateValidationCallback");
					return true;
			};

			TaskStatus status     = 0;
			Exception  exception  = null;

			var thread = new Thread (() => {
				string url = "https://dotnet.microsoft.com/";

				var downloadTask = new WebClient ().DownloadDataTaskAsync (url);
				var completeTask = downloadTask.ContinueWith (t => {
						Console.WriteLine ("# DownloadDataTaskAsync complete; status={0}; exception={1}", t.Status, t.Exception);
						status    = t.Status;
						exception = t.Exception;
				});
				completeTask.Wait ();
			});
			thread.Start ();
			thread.Join ();

			ServicePointManager.ServerCertificateValidationCallback = cb;
			var wex = (exception as AggregateException)?.InnerException as WebException;
			if (wex != null) {
				if (ShouldIgnoreException (wex)) {
					Assert.Ignore ($"Ignoring network failure: {wex}");
					return;
				}
				throw wex;
			}

			if (exception != null)
				throw exception;

			Assert.AreEqual (TaskStatus.RanToCompletion, status);
		}

		[Test]
		public void HttpsShouldWork ()
		{
			if (!OperatingSystem.IsAndroidVersionAtLeast (24)) {
				Assert.Ignore ("Not supported on API 23 and lower.");
			}

			RunIgnoringWebException (DoHttpsShouldWork);
		}

		void DoHttpsShouldWork ()
		{
			// string url = "https://bugzilla.novell.com/show_bug.cgi?id=634817";
			string[] urls = new string[]  {
				"https://dotnet.microsoft.com/",
				"https://www.bing.com/",
				"https://httpbin.org/get",
			};
			// string url = "http://slashdot.org";
			HttpWebResponse response = null;
			foreach (var url in urls) {
				HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
				request.Method = "GET";
				response = (HttpWebResponse) request.GetResponse ();
				if (response.StatusCode == HttpStatusCode.TooManyRequests) {
					// try the next url.
					continue;
				}
				break;
			}
			Assert.IsNotNull (response);
			int len = 0;
			using (var _r = new StreamReader (response.GetResponseStream ())) {
				char[] buf = new char [4096];
				int n;
				while ((n = _r.Read (buf, 0, buf.Length)) > 0) {
					/* ignore; we just want to make sure we can read */
					len += n;
				}
			}

			Assert.IsTrue (len > 0);
		}

		[Test (Description="Bug https://bugzilla.xamarin.com/show_bug.cgi?id=18962")]
		public void VerifyTrustedCertificates ()
		{
			if (!OperatingSystem.IsAndroidVersionAtLeast (24)) {
				Assert.Ignore ("Not supported on API 23 and lower.");
			}

			Assert.DoesNotThrow (() => RunIgnoringWebException (DoVerifyTrustedCertificates), "Certificate validation");
		}

		void DoVerifyTrustedCertificates ()
		{
			var tcpClient = new TcpClient ("google.com", 443);
			using (var ssl = new SslStream (tcpClient.GetStream (), false)) {
				ssl.AuthenticateAsClient ("google.com");
			}
		}

		void RunIgnoringWebException (Action test)
		{
			Exception ex = null;
			WebException wex = null;

			try {
				test ();
			} catch (AggregateException e) {
				ex = e;
				wex = e.InnerException as WebException;
			} catch (WebException e) {
				wex = e;
			}

			if (wex != null) {
				if (ShouldIgnoreException (wex)) {
					Assert.Ignore ($"Ignoring network failure: {wex.Status}");
					return;
				}
				throw wex;
			}

			if (ex != null)
				throw ex;
		}
	}
}
