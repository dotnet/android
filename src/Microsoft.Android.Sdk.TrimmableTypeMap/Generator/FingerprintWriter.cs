using System;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Streams model fields into one or two SHA-256 hashes without materialising the whole
/// serialised model in memory.
/// </summary>
/// <remarks>
/// <para>
/// The byte stream produced for each sink is identical to what
/// <see cref="System.IO.BinaryWriter"/> would have written into a <see cref="System.IO.MemoryStream"/>:
/// strings are UTF-8 encoded with a 7-bit encoded byte-length prefix, <see cref="bool"/> is one
/// byte, and <see cref="int"/> is four bytes little-endian.  Keeping the encoding identical means
/// the resulting fingerprints — and therefore the deterministic MVIDs derived from them — are
/// unchanged.
/// </para>
/// <para>
/// Two sinks are supported so the content fingerprint (which seeds the MVID) and the
/// incremental-build fingerprint can be produced from a single walk over the model. Fields shared
/// by both fingerprints are UTF-8 encoded once and appended to both sinks; fields belonging to
/// only one fingerprint are appended to that sink alone.
/// </para>
/// </remarks>
sealed class FingerprintWriter : IDisposable
{
	/// <summary>Selects which fingerprint(s) a write applies to.</summary>
	[Flags]
	public enum Sink
	{
		Content = 1,
		Incremental = 2,
		Both = Content | Incremental,
	}

	// Large enough to absorb the small field writes that dominate the model walk without
	// paying per-field hash update costs, small enough to stay off the large object heap.
	const int BufferSize = 8 * 1024;

	readonly IncrementalHash contentHash;
	readonly IncrementalHash? incrementalHash;
	readonly byte [] contentBuffer;
	readonly byte []? incrementalBuffer;
	byte [] scratch = new byte [512];
	int contentPosition;
	int incrementalPosition;

	public FingerprintWriter (bool includeIncremental)
	{
		contentHash = IncrementalHash.CreateHash (HashAlgorithmName.SHA256);
		contentBuffer = new byte [BufferSize];
		if (includeIncremental) {
			incrementalHash = IncrementalHash.CreateHash (HashAlgorithmName.SHA256);
			incrementalBuffer = new byte [BufferSize];
		}
	}

	public void WriteString (Sink sink, string value)
	{
		int byteCount = Encoding.UTF8.GetByteCount (value);
		EnsureScratch (byteCount + 5);
		int offset = Write7BitEncodedInt (scratch, 0, byteCount);
		Encoding.UTF8.GetBytes (value, 0, value.Length, scratch, offset);
		Write (sink, scratch, 0, offset + byteCount);
	}

	public void WriteOptionalString (Sink sink, string? value)
	{
		WriteBoolean (sink, value is not null);
		if (value is not null) {
			WriteString (sink, value);
		}
	}

	public void WriteBoolean (Sink sink, bool value) => WriteByte (sink, value ? (byte) 1 : (byte) 0);

	public void WriteByte (Sink sink, byte value)
	{
		scratch [0] = value;
		Write (sink, scratch, 0, 1);
	}

	public void WriteInt32 (Sink sink, int value)
	{
		scratch [0] = (byte) value;
		scratch [1] = (byte) (value >> 8);
		scratch [2] = (byte) (value >> 16);
		scratch [3] = (byte) (value >> 24);
		Write (sink, scratch, 0, 4);
	}

	public void WriteRaw (Sink sink, byte [] value) => Write (sink, value, 0, value.Length);

	public byte [] GetContentFingerprint ()
	{
		FlushContent ();
		return contentHash.GetHashAndReset ();
	}

	public byte [] GetIncrementalFingerprint ()
	{
		if (incrementalHash is null) {
			throw new InvalidOperationException ("The incremental fingerprint was not requested.");
		}
		FlushIncremental ();
		return incrementalHash.GetHashAndReset ();
	}

	public void Dispose ()
	{
		contentHash.Dispose ();
		incrementalHash?.Dispose ();
	}

	void EnsureScratch (int required)
	{
		if (scratch.Length < required) {
			scratch = new byte [Math.Max (required, scratch.Length * 2)];
		}
	}

	void Write (Sink sink, byte [] data, int offset, int count)
	{
		if ((sink & Sink.Content) != 0) {
			if (count > contentBuffer.Length - contentPosition) {
				FlushContent ();
			}
			if (count > contentBuffer.Length) {
				contentHash.AppendData (data, offset, count);
			} else {
				Buffer.BlockCopy (data, offset, contentBuffer, contentPosition, count);
				contentPosition += count;
			}
		}
		if ((sink & Sink.Incremental) == 0) {
			return;
		}
		if (incrementalHash is null || incrementalBuffer is null) {
			// Silently dropping the write would produce a fingerprint that looks valid but
			// covers less than it claims to, so fail fast on the caller's mistake instead.
			throw new InvalidOperationException (
				$"Cannot write to {nameof (Sink.Incremental)} because the incremental fingerprint was not requested.");
		}
		if (count > incrementalBuffer.Length - incrementalPosition) {
			FlushIncremental ();
		}
		if (count > incrementalBuffer.Length) {
			incrementalHash.AppendData (data, offset, count);
		} else {
			Buffer.BlockCopy (data, offset, incrementalBuffer, incrementalPosition, count);
			incrementalPosition += count;
		}
	}

	void FlushContent ()
	{
		if (contentPosition > 0) {
			contentHash.AppendData (contentBuffer, 0, contentPosition);
			contentPosition = 0;
		}
	}

	void FlushIncremental ()
	{
		if (incrementalPosition > 0 && incrementalHash is not null && incrementalBuffer is not null) {
			incrementalHash.AppendData (incrementalBuffer, 0, incrementalPosition);
			incrementalPosition = 0;
		}
	}

	static int Write7BitEncodedInt (byte [] destination, int offset, int value)
	{
		uint remaining = (uint) value;
		while (remaining > 0x7Fu) {
			destination [offset++] = (byte) (remaining | ~0x7Fu);
			remaining >>= 7;
		}
		destination [offset++] = (byte) remaining;
		return offset;
	}
}
