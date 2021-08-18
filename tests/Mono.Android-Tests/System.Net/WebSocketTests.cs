using NUnit.Framework;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace System.NetTests
{
	[TestFixture]
	public class WebSocketTests
	{
		[Test, Category ("InetAccess")]
		[Ignore ("echo.websocket.org is not available anymore")]
		public void TestSocketConnection()
		{
			string testMessage = "This is a test!";
			var messageBytes = CustomWebSocket.GetBytes (testMessage);
			CustomWebSocket.BytesSize = messageBytes.Length;
			var result = CustomWebSocket.Connect ("ws://echo.websocket.org", messageBytes).Result;
			Assert.AreEqual (result, testMessage, $"Socket test failed. Expected: {testMessage}, Received: {result}");
		}
	}

	public class CustomWebSocket
	{
		static string ResponseMessage { get; set; }
		public static int BytesSize { get; set; }
		static object consoleLock = new object ();
		static readonly TimeSpan delay = TimeSpan.FromMilliseconds (1000);

		public static async Task<string> Connect (string uri, byte[] message)
		{
			ClientWebSocket clientWebSocket = null;
			clientWebSocket = new ClientWebSocket ();
			await clientWebSocket.ConnectAsync (new Uri (uri), CancellationToken.None).ConfigureAwait (false);
			await Task.WhenAll (Receive (clientWebSocket), Send(clientWebSocket, message)).ConfigureAwait (false);

			if (clientWebSocket != null)
				clientWebSocket.Dispose ();

			lock (consoleLock)
			{
				Console.WriteLine ("Client WebSocket closed");
				return ResponseMessage;
			}
		}

		static async Task Send (ClientWebSocket webSocket, byte[] message)
		{
			if (webSocket.State == WebSocketState.Open)
			{
				await webSocket.SendAsync (new ArraySegment<byte> (message), WebSocketMessageType.Binary, false, CancellationToken.None);
				LogStatus (false, message, message.Length);
				await Task.Delay (delay);
			}
		}

		static async Task Receive (ClientWebSocket webSocket)
		{
			byte[] buffer = new byte[BytesSize];
			if (webSocket.State == WebSocketState.Open)
			{
				var result = await webSocket.ReceiveAsync (new ArraySegment<byte> (buffer), CancellationToken.None);
				if (result.MessageType == WebSocketMessageType.Close)
				{
					await webSocket.CloseAsync (WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
				}
				else
				{
					LogStatus (true, buffer, result.Count);
				}
			}
		}

		static void LogStatus (bool receiving, byte[] buffer, int length)
		{
			lock (consoleLock)
			{
				Console.Write (string.Format ("{0} {1} bytes", receiving ? "Received" : "Sent", length));
				Console.WriteLine (GetString (buffer));

				if (receiving)
					ResponseMessage = GetString (buffer);
			}
		}

		public static byte[] GetBytes (string str)
		{
			byte[] bytes = new byte[str.Length * sizeof (char)];
			Buffer.BlockCopy (str.ToCharArray (), 0, bytes, 0, bytes.Length);
			return bytes;
		}

		static string GetString (byte[] bytes)
		{
			char[] chars = new char[bytes.Length / sizeof (char)];
			Buffer.BlockCopy (bytes, 0, chars, 0, bytes.Length);
			return new string (chars);
		}
	}
}
