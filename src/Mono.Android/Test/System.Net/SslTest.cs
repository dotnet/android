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
	public class SslTest {

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
				string url = "https://tls-test-1.internalx.com";

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
			Assert.AreEqual (TaskStatus.RanToCompletion, status);
		}

		[Test]
		public void HttpsShouldWork ()
		{
			// string url = "https://bugzilla.novell.com/show_bug.cgi?id=634817";
			string url = "https://encrypted.google.com/";
			// string url = "http://slashdot.org";
			var request = (HttpWebRequest) WebRequest.Create(url);
			request.Method = "GET";
			var response = (HttpWebResponse) request.GetResponse ();
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
			Assert.DoesNotThrow (() => {
				var tcpClient = new TcpClient ("google.com", 443);
				using (var ssl = new SslStream (tcpClient.GetStream (), false)) {
					ssl.AuthenticateAsClient ("google.com");
				}
			}, "Certificate validation");
		}
	}
}
