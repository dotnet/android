#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Android.Net;
using NUnit.Framework;

namespace Xamarin.Android.NetTests {
	// Important: We expect the Negotiate authentication feature to be enabled in all of these tests because we set $(AndroidUseNegotiateAuthentication)=true
	// in the Mono.Android.NET-Tests.csproj file.
	[TestFixture]
	[Category ("InetAccess")]
	public sealed class AndroidMessageHandlerNegotiateAuthenticationTests
	{
		// Negotiate authentication is available for Android since .NET 7
		public static bool ShouldBeAvailable => Environment.Version.Major >= 7;

		[Test]
		public void NegotiateAuthenticationIsEnabled ()
		{
			const string propertyName = "NegotiateAuthenticationIsEnabled";
			var property = typeof (AndroidMessageHandler).GetProperty (propertyName, BindingFlags.NonPublic | BindingFlags.Static);

			if (!ShouldBeAvailable) {
				Assert.IsNull (property, $"The {nameof (AndroidMessageHandler)}.{propertyName} property exists in the Monodroid build");
				return;
			}

			Assert.IsNotNull (property, $"The {nameof (AndroidMessageHandler)}.{propertyName} property is missing in the .NET build");
			Assert.IsTrue (property!.GetValue (null) as bool? ?? false, "Negotiate authentication is not enabled");
		}

		[Test]
		public async Task RequestWithoutCredentialsFails ()
		{
			if (!ShouldBeAvailable) {
				Assert.Ignore ("Negotiate authentication is only available in .NET 7+");
			}

			using var server = new FakeNtlmServer (port: 47662);
			var handler = new AndroidMessageHandler ();
			var client = new HttpClient (handler);

			var response = await client.GetAsync (server.Uri);

			Assert.IsFalse (response.IsSuccessStatusCode);
			Assert.AreEqual (HttpStatusCode.Unauthorized, response.StatusCode);
		}

		[Test]
		public async Task RequestWithCredentialsSucceeds ()
		{
			if (!ShouldBeAvailable) {
				Assert.Ignore ("Negotiate authentication is only available in .NET 7+");
			}

			using var server = new FakeNtlmServer (port: 47663);
			var cache = new CredentialCache ();
			cache.Add (server.Uri, "NTLM", FakeNtlmServer.Credentials);
			var handler = new AndroidMessageHandler { Credentials = cache };
			var client = new HttpClient (handler);

			var response = await client.GetAsync (server.Uri);
			var content = await response.Content.ReadAsStringAsync ();

			Assert.IsTrue (response.IsSuccessStatusCode);
			Assert.AreEqual (FakeNtlmServer.SecretContent, content);
		}

		sealed class FakeNtlmServer : IDisposable
		{
			public static readonly NetworkCredential Credentials = new NetworkCredential ("User", "Password", "Domain");
			public static readonly string SecretContent = "SECRET";

			HttpListener? _listener = new HttpListener ();
			Task? _loop;

			public FakeNtlmServer (int port)
			{
				Uri = new Uri ($"http://localhost:{port}/");

				_listener.Prefixes.Add ($"http://+:{port}/");
				_listener.Start ();
				_loop = Task.Run (Loop);
			}

			public Uri Uri { get; }

			public void Dispose ()
			{
				_listener?.Close ();
				_listener = null;

				_loop?.GetAwaiter ().GetResult ();
				_loop = null;
			}

			async Task Loop ()
			{
				try {
					while (true) {
						var ctx = await _listener!.GetContextAsync ();
						var authorization = ctx.Request.Headers.Get ("Authorization");
						var fakeResponse = Handle (authorization);
						fakeResponse.ConfigureAndClose (ctx.Response);
					}
				} catch (ObjectDisposedException) {
					// this exception is expected when the listener is closed
				} catch (HttpListenerException) {
					// shut down the listener
				}
			}

			const string ntlm = "NTLM";
			const string initiation = "NTLM TlRMTVNTUAABAAAAFYKIYgAAAAAAAAAAAAAAAAAAAAAGAbAdAAAADw==";
			const string challenge = "NTLM TlRMTVNTUAACAAAADAAMADgAAAAVgoliASNFZ4mrze8AAAAAAAAAADAAMABEAAAABgBwFwAAAA9EAG8AbQBhAGkAbgACAAwARABvAG0AYQBpAG4AAQAMAFMAZQByAHYAZQByAAcACADffWrlcGTYAQAAAAA=";
			const string challengeResponsePrefix = "NTLM TlRMTVNTUAADAAAAGAAYAFgAAACcAJwAcAAAAAwADAAUAQAACAAIAAwBAAASABIAIAEAABAAEAAyAQAAFYKIYgYBsB0AAAAP";

			// 1. the client makes an unauthenticated request
			//     -> the server responds to with the "WWW-Authenticate: NTLM" header
			// 2. the client sends a request with the "Authorization: NTLM <initiation>" header
			//     -> the server responds with the "WWW-Authenticate: NTLM <challenge>" header
			// 3. the client responds with the "Authorization: NTLM <challenge response prefix><variable suffix>" header
			//     -> the server returns 200
			static FakeResponse Handle (string? authorization)
				=> authorization switch {
					initiation => new (HttpStatusCode.Unauthorized, challenge, string.Empty),
					string challengeResponse when challengeResponse.StartsWith (challengeResponsePrefix) => new (HttpStatusCode.OK, null, SecretContent),
					_ => new (HttpStatusCode.Unauthorized, ntlm, string.Empty)
				};

			class FakeResponse
			{
				private HttpStatusCode _statusCode;
				private string? _header;
				private string _body;

				public FakeResponse (HttpStatusCode statusCode, string? header, string body)
				{
					_statusCode = statusCode;
					_header = header;
					_body = body;
				}

				public void ConfigureAndClose (HttpListenerResponse res)
				{
					res.StatusCode = (int)_statusCode;
					if (_header != null) res.AddHeader ("WWW-Authenticate", _header);
					res.Close (Encoding.UTF8.GetBytes (_body), false);
				}
			}
		}
	}
}
