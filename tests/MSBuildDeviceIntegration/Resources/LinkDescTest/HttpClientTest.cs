using System;
using System.Text;
using System.Net;
using System.Net.Http;

public class HttpClientTest
{
	// [Test]
	public static string Post ()
	{
		try {
			var client = new HttpClient ();
			var data = new StringContent ("{\"foo\": \"bar\" }", Encoding.UTF8, "application/json");
			var response = client.PostAsync ("https://httpbin.org/post", data).Result;
			switch (response.StatusCode) {
 				case HttpStatusCode.InternalServerError:
 				case HttpStatusCode.BadGateway:
 				case HttpStatusCode.ServiceUnavailable:
 				case HttpStatusCode.GatewayTimeout:
 					return $"[IGNORE] {nameof (HttpClientTest)}.{nameof (Post)} {response.StatusCode}";
 			}
			response.EnsureSuccessStatusCode ();
			var json = response.Content.ReadAsStringAsync ().Result;
			return $"[PASS] {nameof (HttpClientTest)}.{nameof (Post)}";
		} catch (Exception ex) {
			return $"[FAIL] {nameof (HttpClientTest)}.{nameof (Post)} FAILED: {ex}";
		}
	}
}
