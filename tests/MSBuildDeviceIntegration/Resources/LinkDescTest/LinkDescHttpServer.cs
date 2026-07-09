using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

sealed class LinkDescHttpServer : IDisposable
{
	readonly HttpListener listener;
	readonly List<Exception> handlerExceptions = new List<Exception> ();
	readonly Task serverTask;

	LinkDescHttpServer ()
	{
		Port = GetAvailablePort ();
		listener = new HttpListener ();
		listener.Prefixes.Add ($"http://localhost:{Port}/");
		listener.Start ();
		serverTask = Task.Run (() => HandleRequest ());
	}

	public int Port { get; }

	public string PostUrl {
		get { return $"http://localhost:{Port}/post"; }
	}

	public static LinkDescHttpServer Start ()
	{
		return new LinkDescHttpServer ();
	}

	public void AssertNoUnhandledExceptions ()
	{
		serverTask.Wait ();
		if (handlerExceptions.Count > 0) {
			throw handlerExceptions [0];
		}
	}

	public void Dispose ()
	{
		listener.Close ();
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

	void HandleRequest ()
	{
		try {
			var context = listener.GetContext ();
			try {
				HandlePost (context);
			} finally {
				context.Response.Close ();
			}
		} catch (Exception ex) {
			handlerExceptions.Add (ex);
		}
	}

	static void HandlePost (HttpListenerContext context)
	{
		using var reader = new StreamReader (context.Request.InputStream, context.Request.ContentEncoding);
		string requestBody = reader.ReadToEnd ();

		if (context.Request.HttpMethod != "POST" || context.Request.Url.AbsolutePath != "/post" || !requestBody.Contains ("\"foo\": \"bar\"")) {
			WriteJson (context.Response, HttpStatusCode.BadRequest, "{\"ok\": false}");
			return;
		}

		WriteJson (context.Response, HttpStatusCode.OK, "{\"ok\": true}");
	}

	static void WriteJson (HttpListenerResponse response, HttpStatusCode statusCode, string content)
	{
		byte[] responseBody = Encoding.UTF8.GetBytes (content);
		response.StatusCode = (int) statusCode;
		response.ContentType = "application/json";
		response.ContentLength64 = responseBody.Length;
		response.OutputStream.Write (responseBody, 0, responseBody.Length);
	}
}
