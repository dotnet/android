using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

public class HttpClientTest
{
	// [Test]
	public static string Post ()
	{
		HttpListener listener = null;
		try {
			int port = GetAvailablePort ();
			listener = new HttpListener ();
			listener.Prefixes.Add ($"http://127.0.0.1:{port}/");
			listener.Start ();
			var serverTask = Task.Run (() => HandlePost (listener));

			using (var client = new HttpClient ())
			using (var data = new StringContent ("{\"foo\": \"bar\" }", Encoding.UTF8, "application/json"))
			using (var response = client.PostAsync ($"http://127.0.0.1:{port}/post", data).Result) {
				response.EnsureSuccessStatusCode ();
				serverTask.Wait ();
			}
			return $"[PASS] {nameof (HttpClientTest)}.{nameof (Post)}";
		} catch (Exception ex) {
			return $"[FAIL] {nameof (HttpClientTest)}.{nameof (Post)} FAILED: {ex}";
		} finally {
			if (listener != null) {
				listener.Close ();
			}
		}
	}

	static int GetAvailablePort ()
	{
		var tcpListener = new TcpListener (IPAddress.Loopback, 0);
		try {
			tcpListener.Start ();
			int port = ((IPEndPoint) tcpListener.LocalEndpoint).Port;
			return port;
		} finally {
			tcpListener.Stop ();
		}
	}

	static void HandlePost (HttpListener listener)
	{
		var context = listener.GetContext ();
		string requestBody;
		using (var reader = new StreamReader (context.Request.InputStream, context.Request.ContentEncoding)) {
			requestBody = reader.ReadToEnd ();
		}

		if (context.Request.HttpMethod != "POST" || !requestBody.Contains ("\"foo\": \"bar\"")) {
			context.Response.StatusCode = (int) HttpStatusCode.BadRequest;
			context.Response.Close ();
			return;
		}

		byte[] responseBody = Encoding.UTF8.GetBytes ("{\"ok\": true}");
		context.Response.ContentType = "application/json";
		context.Response.ContentLength64 = responseBody.Length;
		context.Response.OutputStream.Write (responseBody, 0, responseBody.Length);
		context.Response.Close ();
	}
}
