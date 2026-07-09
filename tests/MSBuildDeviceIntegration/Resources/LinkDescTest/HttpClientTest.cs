using System;
using System.Text;
using System.Net.Http;

public class HttpClientTest
{
	// [Test]
	public static string Post ()
	{
		try {
			using var server = LocalHttpTestServer.Start ();
			using var client = new HttpClient ();
			using var data = new StringContent ("{\"foo\": \"bar\" }", Encoding.UTF8, "application/json");
			using var response = client.PostAsync (server.PostUrl, data).Result;
			response.EnsureSuccessStatusCode ();
			server.AssertNoUnhandledExceptions ();

			return $"[PASS] {nameof (HttpClientTest)}.{nameof (Post)}";
		} catch (Exception ex) {
			return $"[FAIL] {nameof (HttpClientTest)}.{nameof (Post)} FAILED: {ex}";
		}
	}
}
