using System;
using System.Text;
using System.Net.Http;

public class HttpClientTest
{
	// [Test]
	public static string Post ()
	{
		try
		{
			var client = new HttpClient ();
			var data = new StringContent ("{\"foo\": \"bar\" }", Encoding.UTF8, "application/json");
			var response = client.PostAsync ("https://httpbin.org/post", data).Result;
			response.EnsureSuccessStatusCode ();
			var json = response.Content.ReadAsStringAsync ().Result;
			return $"[PASS] {nameof (HttpClientTest)}.{nameof (Post)}";
		}
		catch (Exception ex)
		{
			return $"[FAIL] {nameof (HttpClientTest)}.{nameof (Post)} FAILED: {ex}";
		}
	}
}
