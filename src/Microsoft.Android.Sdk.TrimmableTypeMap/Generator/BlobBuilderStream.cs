using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// A read-only, seekable <see cref="Stream"/> over the chunks a <see cref="BlobBuilder"/> already
/// holds, so a serialised PE image can be hashed and copied to disk without being duplicated into
/// a second contiguous buffer.
/// </summary>
/// <remarks>
/// Only the chunk arrays are retained — the <see cref="BlobBuilder"/> itself and the metadata
/// graph that produced it stay collectible — so the live byte count matches what a
/// <see cref="MemoryStream"/> copy would have held, without the transient second copy.
/// </remarks>
sealed class BlobBuilderStream : Stream
{
	readonly ArraySegment<byte> [] segments;
	readonly long [] segmentStarts;
	readonly long length;
	long position;
	int cursor;

	public BlobBuilderStream (BlobBuilder builder)
	{
		_ = builder ?? throw new ArgumentNullException (nameof (builder));

		var collected = new List<ArraySegment<byte>> ();
		foreach (var blob in builder.GetBlobs ()) {
			var bytes = blob.GetBytes ();
			if (bytes.Count == 0 || bytes.Array is null) {
				continue;
			}
			collected.Add (bytes);
		}

		segments = collected.ToArray ();
		segmentStarts = new long [segments.Length + 1];
		long total = 0;
		for (int i = 0; i < segments.Length; i++) {
			segmentStarts [i] = total;
			total += segments [i].Count;
		}
		segmentStarts [segments.Length] = total;
		length = total;
	}

	public override bool CanRead => true;

	public override bool CanSeek => true;

	public override bool CanWrite => false;

	public override long Length => length;

	public override long Position {
		get => position;
		set {
			if (value < 0) {
				throw new ArgumentOutOfRangeException (nameof (value));
			}
			position = value;
		}
	}

	public override void Flush ()
	{
	}

	public override int Read (byte [] buffer, int offset, int count)
	{
		if (buffer is null) {
			throw new ArgumentNullException (nameof (buffer));
		}
		if (offset < 0) {
			throw new ArgumentOutOfRangeException (nameof (offset));
		}
		if (count < 0) {
			throw new ArgumentOutOfRangeException (nameof (count));
		}
		if (buffer.Length - offset < count) {
			throw new ArgumentException ("The buffer is too small for the requested range.");
		}

		int copied = 0;
		while (count > 0 && position < length) {
			int index = FindSegment (position);
			var segment = segments [index];
			int within = (int) (position - segmentStarts [index]);
			int available = segment.Count - within;
			int toCopy = Math.Min (available, count);
			if (segment.Array is null) {
				break;
			}
			Buffer.BlockCopy (segment.Array, segment.Offset + within, buffer, offset, toCopy);
			position += toCopy;
			offset += toCopy;
			count -= toCopy;
			copied += toCopy;
		}
		return copied;
	}

	public override long Seek (long offset, SeekOrigin origin)
	{
		long target = origin switch {
			SeekOrigin.Begin => offset,
			SeekOrigin.Current => position + offset,
			SeekOrigin.End => length + offset,
			_ => throw new ArgumentOutOfRangeException (nameof (origin)),
		};
		if (target < 0) {
			throw new IOException ("Cannot seek before the beginning of the stream.");
		}
		position = target;
		return position;
	}

	public override void SetLength (long value) => throw new NotSupportedException ();

	public override void Write (byte [] buffer, int offset, int count) => throw new NotSupportedException ();

	int FindSegment (long offset)
	{
		// Reads are overwhelmingly sequential, so try the last used chunk first.
		if (cursor < segments.Length && offset >= segmentStarts [cursor] && offset < segmentStarts [cursor + 1]) {
			return cursor;
		}

		int low = 0;
		int high = segments.Length - 1;
		while (low <= high) {
			int middle = low + ((high - low) / 2);
			if (offset < segmentStarts [middle]) {
				high = middle - 1;
			} else if (offset >= segmentStarts [middle + 1]) {
				low = middle + 1;
			} else {
				cursor = middle;
				return middle;
			}
		}
		throw new ArgumentOutOfRangeException (nameof (offset));
	}
}
