using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using NUnit.Framework;

namespace BclTests {

    [TestFixture]
    public class HttpClientTest
    {
        [Test] // Covers #26896
        public void TestUnescapedURI ()
        {
            try
            {
                HttpClient client = new HttpClient();

                var t = client.GetStringAsync("http://naver.com/t[e]st.txt");
                t.Wait(1000);
                Assert.IsNotNull(t.Result);
            } catch (TaskCanceledException) {
                Assert.Ignore ("Connection timed out");
            } catch (AggregateException e) {
                Assert.AreEqual (1, e.InnerExceptions.Count);
                if (e.InnerExceptions[0].GetType () == typeof (TaskCanceledException))
                    Assert.Ignore ("Connection timed out");
                else
                    Assert.AreEqual (typeof(HttpRequestException), e.InnerExceptions[0].GetType ());
            }
        }
    }
}
