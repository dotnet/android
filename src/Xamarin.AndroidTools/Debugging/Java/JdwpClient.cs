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
		const int packetHeaderSize = 11;
		const int maximumPacketSize = 16 * 1024 * 1024;
		const int legacyDebuggerIdleDelayMilliseconds = 1400;
		const uint chunkHelo = 0x48454c4f;
		const uint chunkFeat = 0x46454154;
		const uint chunkStag = 0x53544147;
		const uint stageApplicationRunning = 0x415f474f;
		const string bootStagesFeature = "support_boot_stages";
		private bool disposed = false;

		private TcpClient tcpClient;
		private Stream stream;
		private readonly Queue<ReceivedPacket> bufferedPackets = new Queue<ReceivedPacket> ();

		public string HostName { get; }

		public int Port { get; }

		public JdwpClient(string hostname = "127.0.0.1", int port = 8100)
		{
			HostName = hostname;
			Port = port;
		}

		internal JdwpClient (Stream stream)
		{
			this.stream = stream ?? throw new ArgumentNullException (nameof (stream));
			HostName = "";
		}

		public async Task ConnectAsync(CancellationToken cancellationToken = default)
		{
			tcpClient = new TcpClient();

			using (cancellationToken.Register (() => tcpClient.Close ())) {
				try {
					await tcpClient.ConnectAsync (HostName, Port).ConfigureAwait (false);
				} catch (Exception) when (cancellationToken.IsCancellationRequested) {
					throw new OperationCanceledException (cancellationToken);
				}
			}
			stream = tcpClient.GetStream();

			var data = Encoding.ASCII.GetBytes(handshake);

			await stream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait (false);

			var buffer = new byte[handshake.Length];
			await ReadExactlyAsync (buffer, cancellationToken).ConfigureAwait (false);

			var str = Encoding.ASCII.GetString(buffer, 0, buffer.Length);
			if (str.Equals(handshake))
			{
				// Send version request command to kick things off
				var versionCommand = new VersionCommandPacket ();
				await SendAsync(versionCommand, cancellationToken).ConfigureAwait (false);

				while (true) {
					var reply = await ReadPacketAsync (cancellationToken).ConfigureAwait (false);
					if (!reply.IsReply || reply.Id != versionCommand.Id) {
						bufferedPackets.Enqueue (reply);
						continue;
					}
					if (reply.ErrorCode != 0)
						throw new InvalidDataException ($"JDWP version request failed with error {reply.ErrorCode}.");

					str = Encoding.ASCII.GetString(reply.Data.ToArray (), 0, reply.Data.Length);
					AndroidLogger.LogDebug ($"VersionCommandPacket:\n\t{str}");
					break;
				}
			}
			else
			{
				throw new InvalidDataException($"Debugger response did not match expected value: '{handshake}'");
			}
		}

		public Task WaitForDebuggerReadinessAsync (TimeSpan timeout, CancellationToken cancellationToken = default)
		{
			return WaitForDebuggerReadinessAsync (
				timeout,
				(delay, token) => Task.Delay (delay, token),
				cancellationToken
			);
		}

		internal async Task WaitForDebuggerReadinessAsync (
			TimeSpan timeout,
			Func<TimeSpan, CancellationToken, Task> delayAsync,
			CancellationToken cancellationToken = default)
		{
			if (stream == null)
				throw new InvalidOperationException ("The JDWP client is not connected.");
			if (timeout <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException (nameof (timeout));
			if (delayAsync == null)
				throw new ArgumentNullException (nameof (delayAsync));

			using (var timeoutSource = new CancellationTokenSource ())
			using (var linkedSource = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, timeoutSource.Token)) {
				timeoutSource.CancelAfter (timeout);
				using (linkedSource.Token.Register (() => stream.Dispose ())) {
					try {
						await WaitForDebuggerReadinessCoreAsync (delayAsync, linkedSource.Token).ConfigureAwait (false);
					} catch (Exception e) when (linkedSource.IsCancellationRequested) {
						if (cancellationToken.IsCancellationRequested)
							throw new OperationCanceledException ("Waiting for Android JDWP readiness was canceled.", e, cancellationToken);
						throw new TimeoutException (
							$"Timed out after {timeout.TotalMilliseconds:0} ms waiting for Android to reach JDWP stage A_GO.",
							e
						);
					}
				}
			}
		}

		async Task WaitForDebuggerReadinessCoreAsync (
			Func<TimeSpan, CancellationToken, Task> delayAsync,
			CancellationToken cancellationToken)
		{
			var heloData = new byte [4];
			BinaryPrimitives.WriteInt32BigEndian (heloData, 1);
			var heloCommand = new DdmCommandPacket ("HELO", heloData);
			var featCommand = new DdmCommandPacket ("FEAT", ReadOnlyMemory<byte>.Empty);

			await SendAsync (heloCommand, cancellationToken).ConfigureAwait (false);
			await SendAsync (featCommand, cancellationToken).ConfigureAwait (false);

			bool heloReceived = false;
			bool featReceived = false;
			bool supportsBootStages = false;
			bool applicationRunningSeen = false;

			while (true) {
				var packet = bufferedPackets.Count > 0
					? bufferedPackets.Dequeue ()
					: await ReadPacketAsync (cancellationToken).ConfigureAwait (false);

				if (packet.IsReply && packet.Id == heloCommand.Id) {
					if (packet.ErrorCode != 0)
						throw new InvalidDataException ($"DDM HELO request failed with error {packet.ErrorCode}.");
					heloReceived = true;
					applicationRunningSeen |= ReadHeloStage (packet.Data) == stageApplicationRunning;
				} else if (packet.IsReply && packet.Id == featCommand.Id) {
					if (packet.ErrorCode != 0)
						throw new InvalidDataException ($"DDM FEAT request failed with error {packet.ErrorCode}.");
					featReceived = true;
					supportsBootStages = ReadFeatures (packet.Data).Contains (bootStagesFeature);
				} else if (!packet.IsReply && packet.CommandSet == 0xc7 && packet.Command == 0x01) {
					if (TryReadChunk (packet.Data, out var chunkType, out var chunkData) &&
						chunkType == chunkStag &&
						chunkData.Length == 4) {
						applicationRunningSeen |= BinaryPrimitives.ReadUInt32BigEndian (chunkData.Span) == stageApplicationRunning;
					}
				}

				if (!featReceived)
					continue;

				if (!supportsBootStages) {
					await delayAsync (TimeSpan.FromMilliseconds (legacyDebuggerIdleDelayMilliseconds), cancellationToken).ConfigureAwait (false);
					return;
				}

				if (heloReceived && applicationRunningSeen)
					return;
			}
		}

		async Task<ReceivedPacket> ReadPacketAsync (CancellationToken cancellationToken)
		{
			if (stream == null)
				throw new InvalidOperationException ("The JDWP client is not connected.");

			var header = new byte [packetHeaderSize];
			await ReadExactlyAsync (header, cancellationToken).ConfigureAwait (false);

			var packetLength = BinaryPrimitives.ReadInt32BigEndian (header.AsSpan (0, 4));
			if (packetLength < packetHeaderSize || packetLength > maximumPacketSize)
				throw new InvalidDataException ($"Invalid JDWP packet length {packetLength}.");

			var data = new byte [packetLength - packetHeaderSize];
			await ReadExactlyAsync (data, cancellationToken).ConfigureAwait (false);
			return new ReceivedPacket (header, data);
		}

		async Task ReadExactlyAsync (byte [] buffer, CancellationToken cancellationToken)
		{
			if (stream == null)
				throw new InvalidOperationException ("The JDWP client is not connected.");

			int offset = 0;
			while (offset < buffer.Length) {
				var read = await stream.ReadAsync (buffer, offset, buffer.Length - offset, cancellationToken).ConfigureAwait (false);
				if (read == 0)
					throw new EndOfStreamException ("The JDWP connection closed before the expected data was received.");
				offset += read;
			}
		}

		static uint? ReadHeloStage (ReadOnlyMemory<byte> packetData)
		{
			if (!TryReadChunk (packetData, out var chunkType, out var data) || chunkType != chunkHelo)
				throw new InvalidDataException ("The DDM HELO reply did not contain a HELO chunk.");

			var offset = 0;
			ReadInt32 (data, ref offset);
			ReadInt32 (data, ref offset);
			var vmIdentifierLength = ReadNonNegativeLength (data, ref offset, "VM identifier");
			var applicationNameLength = ReadNonNegativeLength (data, ref offset, "application name");
			SkipUtf16String (data, ref offset, vmIdentifierLength, "VM identifier");
			SkipUtf16String (data, ref offset, applicationNameLength, "application name");

			if (!HasBytes (data, offset, 4))
				return null;
			ReadInt32 (data, ref offset);
			if (!HasBytes (data, offset, 4))
				return null;
			SkipUtf16String (data, ref offset, ReadNonNegativeLength (data, ref offset, "ABI"), "ABI");
			if (!HasBytes (data, offset, 4))
				return null;
			SkipUtf16String (data, ref offset, ReadNonNegativeLength (data, ref offset, "JVM flags"), "JVM flags");
			if (!HasBytes (data, offset, 1))
				return null;
			offset++;
			if (!HasBytes (data, offset, 4))
				return null;
			SkipUtf16String (data, ref offset, ReadNonNegativeLength (data, ref offset, "package name"), "package name");
			if (!HasBytes (data, offset, 4))
				return null;
			return BinaryPrimitives.ReadUInt32BigEndian (data.Slice (offset, 4).Span);
		}

		static HashSet<string> ReadFeatures (ReadOnlyMemory<byte> packetData)
		{
			if (!TryReadChunk (packetData, out var chunkType, out var data) || chunkType != chunkFeat)
				throw new InvalidDataException ("The DDM FEAT reply did not contain a FEAT chunk.");

			var offset = 0;
			var featureCount = ReadNonNegativeLength (data, ref offset, "feature count");
			var features = new HashSet<string> (StringComparer.Ordinal);
			for (int i = 0; i < featureCount; i++) {
				var featureLength = ReadNonNegativeLength (data, ref offset, "feature");
				var byteLength = CheckedUtf16ByteLength (featureLength, "feature");
				if (!HasBytes (data, offset, byteLength))
					throw new InvalidDataException ("The DDM FEAT reply ended in the middle of a feature name.");
				features.Add (Encoding.BigEndianUnicode.GetString (data.Slice (offset, byteLength).ToArray ()));
				offset += byteLength;
			}
			return features;
		}

		static bool TryReadChunk (ReadOnlyMemory<byte> packetData, out uint chunkType, out ReadOnlyMemory<byte> chunkData)
		{
			chunkType = 0;
			chunkData = ReadOnlyMemory<byte>.Empty;
			if (packetData.Length < 8)
				return false;

			chunkType = BinaryPrimitives.ReadUInt32BigEndian (packetData.Slice (0, 4).Span);
			var chunkLength = BinaryPrimitives.ReadInt32BigEndian (packetData.Slice (4, 4).Span);
			if (chunkLength < 0 || packetData.Length - 8 != chunkLength)
				throw new InvalidDataException ($"Invalid DDM chunk length {chunkLength}.");
			chunkData = packetData.Slice (8, chunkLength);
			return true;
		}

		static int ReadNonNegativeLength (ReadOnlyMemory<byte> data, ref int offset, string fieldName)
		{
			var length = ReadInt32 (data, ref offset);
			if (length < 0)
				throw new InvalidDataException ($"The DDM {fieldName} length cannot be negative.");
			return length;
		}

		static int ReadInt32 (ReadOnlyMemory<byte> data, ref int offset)
		{
			if (!HasBytes (data, offset, 4))
				throw new InvalidDataException ("The DDM payload ended before an expected integer.");
			var value = BinaryPrimitives.ReadInt32BigEndian (data.Slice (offset, 4).Span);
			offset += 4;
			return value;
		}

		static void SkipUtf16String (ReadOnlyMemory<byte> data, ref int offset, int characterLength, string fieldName)
		{
			var byteLength = CheckedUtf16ByteLength (characterLength, fieldName);
			if (!HasBytes (data, offset, byteLength))
				throw new InvalidDataException ($"The DDM payload ended in the middle of the {fieldName}.");
			offset += byteLength;
		}

		static int CheckedUtf16ByteLength (int characterLength, string fieldName)
		{
			try {
				return checked (characterLength * 2);
			} catch (OverflowException e) {
				throw new InvalidDataException ($"The DDM {fieldName} length is too large.", e);
			}
		}

		static bool HasBytes (ReadOnlyMemory<byte> data, int offset, int count)
		{
			return offset >= 0 && count >= 0 && offset <= data.Length - count;
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
				await stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait (false);
			}
			else
				throw new InvalidOperationException ("The JDWP client is not connected.");
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

		sealed class ReceivedPacket
		{
			public ReceivedPacket (ReadOnlyMemory<byte> header, ReadOnlyMemory<byte> data)
			{
				Id = BinaryPrimitives.ReadInt32BigEndian (header.Slice (4, 4).Span);
				Flags = header.Span [8];
				Data = data;
				if (IsReply) {
					ErrorCode = BinaryPrimitives.ReadInt16BigEndian (header.Slice (9, 2).Span);
				} else {
					CommandSet = header.Span [9];
					Command = header.Span [10];
				}
			}

			public int Id { get; }
			public byte Flags { get; }
			public short ErrorCode { get; }
			public byte CommandSet { get; }
			public byte Command { get; }
			public ReadOnlyMemory<byte> Data { get; }
			public bool IsReply => (Flags & 0x80) == 0x80;
		}
	}

}
