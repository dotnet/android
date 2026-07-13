// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools;

/// <summary>
/// Low-level ADB daemon socket protocol client.
/// Encapsulates a single TCP connection to the ADB server and exposes
/// the wire protocol operations (send command, read status, read length-prefixed payloads).
/// One instance can be reused across reconnections via <see cref="ReconnectAsync"/>.
/// Dispose closes the socket.
/// </summary>
/// <remarks>
/// This class is not thread-safe. All protocol operations must be serialized by the caller.
/// </remarks>
internal sealed class AdbClient : IDisposable
{
	TcpClient? client;
	NetworkStream? stream;
	bool disposed;

	/// <summary>
	/// Connects to the ADB daemon at 127.0.0.1 on the specified port.
	/// </summary>
	public async Task ConnectAsync (int port, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed ();
		var tcp = new TcpClient ();
		client = tcp;
#if NET5_0_OR_GREATER
		await tcp.ConnectAsync ("127.0.0.1", port, cancellationToken).ConfigureAwait (false);
#else
		await tcp.ConnectAsync ("127.0.0.1", port).ConfigureAwait (false);
		cancellationToken.ThrowIfCancellationRequested ();
#endif
		stream = tcp.GetStream ();
	}

	/// <summary>
	/// Closes the current connection and establishes a new one.
	/// </summary>
	public async Task ReconnectAsync (int port, CancellationToken cancellationToken = default)
	{
		CloseConnection ();
		await ConnectAsync (port, cancellationToken).ConfigureAwait (false);
	}

	/// <summary>
	/// Sends a length-prefixed command to the ADB daemon.
	/// Wire format: &lt;4-digit hex byte length&gt;&lt;command bytes&gt;
	/// </summary>
	public async Task SendCommandAsync (string command, CancellationToken cancellationToken = default)
	{
		var s = GetStream ();
		// Compute byte count without allocating a separate commandBytes array
		var byteCount = Encoding.ASCII.GetByteCount (command);
		var packetLength = 4 + byteCount;
		var packet = ArrayPool<byte>.Shared.Rent (packetLength);
		try {
			// Write 4-hex-digit length prefix directly into packet
			WriteHexLength (packet, byteCount);
			// Encode command directly into packet after the prefix
			Encoding.ASCII.GetBytes (command, 0, command.Length, packet, 4);
#if NET5_0_OR_GREATER
			await s.WriteAsync (packet.AsMemory (0, packetLength), cancellationToken).ConfigureAwait (false);
#else
			await s.WriteAsync (packet, 0, packetLength, cancellationToken).ConfigureAwait (false);
#endif
		}
		finally {
			ArrayPool<byte>.Shared.Return (packet);
		}
		await s.FlushAsync (cancellationToken).ConfigureAwait (false);
	}

	/// <summary>
	/// Reads the 4-byte status response from the ADB daemon.
	/// </summary>
	public async Task<AdbResponseStatus> ReadStatusAsync (CancellationToken cancellationToken = default)
	{
		var s = GetStream ();
		var statusBuffer = new byte [4];
		await ReadExactBytesIntoBufferAsync (s, statusBuffer, 4, cancellationToken).ConfigureAwait (false);
		if (statusBuffer [0] == (byte) 'O' && statusBuffer [1] == (byte) 'K' &&
			statusBuffer [2] == (byte) 'A' && statusBuffer [3] == (byte) 'Y')
			return AdbResponseStatus.Okay;
		if (statusBuffer [0] == (byte) 'F' && statusBuffer [1] == (byte) 'A' &&
			statusBuffer [2] == (byte) 'I' && statusBuffer [3] == (byte) 'L')
			return AdbResponseStatus.Fail;

		var status = Encoding.ASCII.GetString (statusBuffer, 0, 4);
		throw new InvalidOperationException ($"Unexpected ADB status: '{status}'");
	}

	/// <summary>
	/// Reads the failure message after a FAIL status.
	/// </summary>
	public async Task<string> ReadFailMessageAsync (CancellationToken cancellationToken = default)
	{
		return await ReadLengthPrefixedStringAsync (cancellationToken).ConfigureAwait (false) ?? string.Empty;
	}

	/// <summary>
	/// Ensures the last status was OKAY; throws with the FAIL message otherwise.
	/// </summary>
	public async Task EnsureOkayAsync (CancellationToken cancellationToken = default)
	{
		var status = await ReadStatusAsync (cancellationToken).ConfigureAwait (false);
		if (status == AdbResponseStatus.Fail) {
			var message = await ReadFailMessageAsync (cancellationToken).ConfigureAwait (false);
			throw new InvalidOperationException ($"ADB command failed: {message}");
		}
	}

	/// <summary>
	/// Reads a length-prefixed ASCII string payload from the daemon.
	/// Returns null if the connection is closed cleanly before the length prefix.
	/// </summary>
	public async Task<string?> ReadLengthPrefixedStringAsync (CancellationToken cancellationToken = default)
	{
		var bytes = await ReadLengthPrefixedBytesAsync (cancellationToken).ConfigureAwait (false);
		if (bytes == null)
			return null;
		return Encoding.ASCII.GetString (bytes, 0, bytes.Length);
	}

	/// <summary>
	/// Reads a length-prefixed byte payload from the daemon.
	/// Returns null if the connection is closed cleanly before the length prefix.
	/// The returned byte[] is caller-owned (not pooled).
	/// </summary>
	public async Task<byte[]?> ReadLengthPrefixedBytesAsync (CancellationToken cancellationToken = default)
	{
		return await ReadLengthPrefixedBytesFromStreamAsync (GetStream (), new byte [4], cancellationToken).ConfigureAwait (false);
	}

	/// <summary>
	/// Forcibly closes the underlying socket, unblocking any pending reads.
	/// </summary>
	public void Close ()
	{
		CloseConnection ();
	}

	public void Dispose ()
	{
		if (disposed)
			return;
		disposed = true;
		CloseConnection ();
	}

	void CloseConnection ()
	{
		stream = null;
		var tcp = client;
		client = null;
		if (tcp != null) {
			tcp.Close ();
			tcp.Dispose ();
		}
	}

	NetworkStream GetStream ()
	{
		ThrowIfDisposed ();
		if (stream == null)
			throw new InvalidOperationException ("Not connected. Call ConnectAsync first.");
		return stream;
	}

	void ThrowIfDisposed ()
	{
		if (disposed)
			throw new ObjectDisposedException (nameof (AdbClient));
	}

	/// <summary>
	/// Reads a length-prefixed ASCII string from a raw stream.
	/// Used by tests that cannot construct an AdbClient instance.
	/// Allocates a fresh length-prefix buffer (no pooling) since it has no instance state.
	/// </summary>
	internal static async Task<string?> ReadLengthPrefixedStringFromStreamAsync (Stream stream, CancellationToken cancellationToken)
	{
		var bytes = await ReadLengthPrefixedBytesFromStreamAsync (stream, new byte [4], cancellationToken).ConfigureAwait (false);
		if (bytes == null)
			return null;
		return Encoding.ASCII.GetString (bytes, 0, bytes.Length);
	}

	/// <summary>
	/// Shared core for length-prefixed payload reads. Reads a 4-byte hex length prefix
	/// into <paramref name="lengthBuffer"/>, then reads that many bytes from <paramref name="stream"/>.
	/// Returns null if the stream closes cleanly before the length prefix.
	/// Returns <see cref="Array.Empty{T}"/> when the prefix is "0000".
	/// Throws <see cref="IOException"/> if the stream ends mid-prefix or mid-payload.
	/// </summary>
	static async Task<byte[]?> ReadLengthPrefixedBytesFromStreamAsync (Stream stream, byte[] lengthBuffer, CancellationToken cancellationToken)
	{
		if (!await TryReadExactBytesIntoBufferAsync (stream, lengthBuffer, 4, cancellationToken).ConfigureAwait (false))
			return null;

		var length = ParseHexLength (lengthBuffer);

		if (length == 0)
			return Array.Empty<byte> ();

		var result = new byte [length];
		await ReadExactBytesIntoBufferAsync (stream, result, length, cancellationToken).ConfigureAwait (false);
		return result;
	}

	/// <summary>
	/// Reads exactly <paramref name="count"/> bytes into the provided buffer.
	/// Throws IOException if the stream ends prematurely.
	/// </summary>
	static Task ReadExactBytesIntoBufferAsync (Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
		=> ReadExactBytesCoreAsync (stream, buffer, count, allowCleanEof: false, cancellationToken);

	/// <summary>
	/// Tries to read exactly <paramref name="count"/> bytes into the buffer.
	/// Returns false if the stream ends cleanly before the first byte.
	/// Throws IOException on partial reads.
	/// </summary>
	static async Task<bool> TryReadExactBytesIntoBufferAsync (Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
		=> await ReadExactBytesCoreAsync (stream, buffer, count, allowCleanEof: true, cancellationToken).ConfigureAwait (false);

	/// <summary>
	/// Shared core for exact-byte reads. When <paramref name="allowCleanEof"/> is true, returns false
	/// if the stream ends before any byte has been read; otherwise throws <see cref="IOException"/>.
	/// Partial reads (some bytes read then EOF) always throw <see cref="IOException"/>.
	/// </summary>
	static async Task<bool> ReadExactBytesCoreAsync (Stream stream, byte[] buffer, int count, bool allowCleanEof, CancellationToken cancellationToken)
	{
		var totalRead = 0;
		while (totalRead < count) {
			cancellationToken.ThrowIfCancellationRequested ();
#if NET5_0_OR_GREATER
			var read = await stream.ReadAsync (buffer.AsMemory (totalRead, count - totalRead), cancellationToken).ConfigureAwait (false);
#else
			var read = await stream.ReadAsync (buffer, totalRead, count - totalRead, cancellationToken).ConfigureAwait (false);
#endif
			if (read == 0) {
				if (allowCleanEof && totalRead == 0)
					return false;
				throw new IOException ($"Unexpected end of stream (read {totalRead} of {count} bytes).");
			}
			totalRead += read;
		}
		return true;
	}

	/// <summary>
	/// Writes a 4-digit lowercase hex representation of <paramref name="value"/> into the first 4 bytes of <paramref name="buffer"/>.
	/// </summary>
	static void WriteHexLength (byte[] buffer, int value)
	{
		buffer [0] = ToAsciiHexNibble ((value >> 12) & 0xF);
		buffer [1] = ToAsciiHexNibble ((value >> 8) & 0xF);
		buffer [2] = ToAsciiHexNibble ((value >> 4) & 0xF);
		buffer [3] = ToAsciiHexNibble (value & 0xF);
	}

	static byte ToAsciiHexNibble (int value) => (byte) (value < 10 ? '0' + value : 'a' + value - 10);

	/// <summary>
	/// Parses a 4-byte ASCII hex length prefix without allocating a string.
	/// </summary>
	static int ParseHexLength (byte[] buffer)
	{
		var value = 0;
		for (var i = 0; i < 4; i++) {
			var b = buffer [i];
			int nibble;
			if (b >= (byte) '0' && b <= (byte) '9')
				nibble = b - '0';
			else if (b >= (byte) 'a' && b <= (byte) 'f')
				nibble = b - 'a' + 10;
			else if (b >= (byte) 'A' && b <= (byte) 'F')
				nibble = b - 'A' + 10;
			else
				throw new FormatException ($"Invalid ADB length prefix: '{Encoding.ASCII.GetString (buffer, 0, 4)}'");
			value = (value << 4) | nibble;
		}
		return value;
	}
}
