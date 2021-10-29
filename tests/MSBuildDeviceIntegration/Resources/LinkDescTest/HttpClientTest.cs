using System;
using System.Net.Http;

public class HttpClientTest
{
	// [Test]
	public static string NewHttpClient()
	{
		try
		{
			new HttpClient();
			return $"[PASS] {nameof(NewHttpClient)}";
		}
		catch (Exception ex)
		{
			return $"[FAIL] {nameof(NewHttpClient)} FAILED: {ex}";
		}
	}
}
