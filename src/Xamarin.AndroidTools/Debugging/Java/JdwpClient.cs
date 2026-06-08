using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mono.AndroidTools;

namespace Xamarin.AndroidTools.Debugging.Java
{
	public class JdwpClient : IDisposable
	{
		const string handshake = "JDWP-Handshake";
		const int packetSize = 11;
		private bool disposed = false;

		private TcpClient tcpClient;
		private NetworkStream stream;

		public string HostName { get; }

		public int Port { get; }

		public JdwpClient(string hostname = "127.0.0.1", int port = 8100)
		{
			HostName = hostname;
			Port = port;
		}

		public async Task ConnectAsync(CancellationToken cancellationToken = default)
		{
			tcpClient = new TcpClient();

			await tcpClient.ConnectAsync(HostName, Port);
			stream = tcpClient.GetStream();

			var data = Encoding.ASCII.GetBytes(handshake);

			await stream.WriteAsync(data, 0, data.Length);

			var buffer = new byte[handshake.Length];
			var replyBuffer = new byte[packetSize];

			// Read handshake response
			var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

			var str = Encoding.ASCII.GetString(buffer, 0, read);
			if (str.Equals(handshake))
			{
				// Send version request command to kick things off
				await SendAsync(new VersionCommandPacket(), cancellationToken);

				var replies = await ReadReply<ReplyPacket> (cancellationToken);
				// Read the Result but we do not need to process it.
				AndroidLogger.LogDebug  ($"VersionCommandPacket:");
				foreach (var reply in replies) {
					str = Encoding.ASCII.GetString(reply.Data.ToArray (), 0, reply.Data.Length);
					AndroidLogger.LogDebug  ($"\t{str}");
				}
			}
			else
			{
				throw new InvalidDataException($"Debugger response did not match expected value: '{handshake}'");
			}
		}

		async Task<IEnumerable<T>> ReadReply<T>(CancellationToken cancellationToken = default) where T : ReplyPacket, new()
		{
			List<T> packets = new List<T> ();
			do
			{
				if (stream != null)
				{
					byte[] headerData = new byte[11];

					// Read the header data or bust
					var read = await stream.ReadAsync (headerData, 0, headerData.Length, cancellationToken);
					if (read != headerData.Length) {
						break;
					}
					
					// Get overall packet length from header
					ReadOnlyMemory<byte> h = headerData;
					var packetLength = BinaryPrimitives.ReadUInt32BigEndian(h.Slice (0, 4).Span);
					// The remaining packet buffer is total packet length minus header length
					byte[] packetData = new byte[packetLength - headerData.Length];

					if (packetData.Length > 0) {
						// Read the remainder of the packet into the second buffer
						int datalen = packetData.Length;
						while (datalen > 0) {
							read = await stream.ReadAsync (packetData, 0, datalen, cancellationToken);
							datalen -= read;
							if (read == 0)
								break;
						}
						if (datalen > 0) {
							break;
						}
					}
					var packet = new T ();
					packet.FromMemory (headerData, packetData);
					packets.Add (packet);
				}
				else
				{
					break;
				}
			} while (stream.DataAvailable);
			return packets;
		}

		[Obsolete ("Use DisconnectAsync instead", error:true)]
		public void Disconnect()
		{
		}

		public Task DisconnectAsync()
		{
			if (stream != null)
			{
				try
				{
					stream.Dispose();
				}
				catch
				{
					// nothing to do
				}
				finally { stream = null; }
			}

			if (tcpClient != null)
			{
				try
				{
					tcpClient?.Close();
				}
				catch { }

				try
				{
					tcpClient?.Dispose();
				}
				catch
				{
					// nothing to do
				}
				finally { tcpClient = null; }
			}
			return Task.CompletedTask;
		}

		async Task SendAsync(CommandPacket packet, CancellationToken cancellationToken = default)
		{
			if (stream != null)
			{
				var buffer = packet.ToMemory().ToArray();
				await stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
			}
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if(!this.disposed)
			{
				if(disposing)
				{
					// Dispose managed resources.
					DisconnectAsync().Wait ();
				}

				// Note disposing has been done.
				disposed = true;
			}
		}
	}

}
