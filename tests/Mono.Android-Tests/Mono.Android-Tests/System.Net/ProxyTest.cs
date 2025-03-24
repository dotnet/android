using System;
using System.IO;
using System.Net;

using NUnit.Framework;

namespace System.NetTests {

	[TestFixture, Category ("InetAccess")]
	public class ProxyTest {

		// https://bugzilla.xamarin.com/show_bug.cgi?id=14968
		[Test]
		public void QuoteInvalidQuoteUrlsShouldWork ()
		{
			string url      = "http://www.msftconnecttest.com/connecttest.txt?query&foo|bar";
			var request     = (HttpWebRequest) WebRequest.Create (url);
			request.Method  = "GET";
			var response    = (HttpWebResponse) request.GetResponse ();
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
	}
}
