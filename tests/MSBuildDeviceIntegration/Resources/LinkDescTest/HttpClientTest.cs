using System;
using System.Text;
using System.Net.Http;
using Xamarin.Android.NetTests;

public class HttpClientTest
{
	// [Test]
	public static string Post ()
	{
		try {
			using var server = LocalHttpServer.Start ();
			using var client = new HttpClient {
				BaseAddress = server.Uri,
			};
			using var data = new StringContent ("{\"foo\": \"bar\" }", Encoding.UTF8, "application/json");
			using var response = client.PostAsync ("/post", data).Result;
			response.EnsureSuccessStatusCode ();
			server.AssertNoUnhandledExceptions ();

			return $"[PASS] {nameof (HttpClientTest)}.{nameof (Post)}";
		} catch (Exception ex) {
			return $"[FAIL] {nameof (HttpClientTest)}.{nameof (Post)} FAILED: {ex}";
		}
	}
}
