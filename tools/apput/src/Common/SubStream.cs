using System;
using System.IO;

namespace ApplicationUtility;

class SubStream : Stream
{
	readonly Stream baseStream;
	readonly long length;
	readonly long offsetInParentStream;

	public override bool CanRead => true;
	public override bool CanSeek => true;
	public override bool CanWrite => false;
	public override long Length => length;

	public override long Position {
		get => throw new NotSupportedException ();
		set => throw new NotSupportedException ();
	}

	public SubStream (Stream baseStream, long offsetInParentStream, long length)
	{
		if (!baseStream.CanSeek) {
			throw new InvalidOperationException ($"Base stream must support seeking");
		}

		if (!baseStream.CanRead) {
			throw new InvalidOperationException ($"Base stream must support reading");
		}

		if (offsetInParentStream >= baseStream.Length) {
			throw new ArgumentOutOfRangeException (nameof (offsetInParentStream), $"{offsetInParentStream} exceeds length of the base stream ({baseStream.Length})");
		}

		if (offsetInParentStream + length > baseStream.Length) {
			throw new InvalidOperationException ($"Not enough data in base stream after offset {offsetInParentStream}, length of {length} bytes is too big.");
		}

		this.baseStream = baseStream;
		this.length = length;
		this.offsetInParentStream = offsetInParentStream;
	}

	public override int Read (byte [] buffer, int offset, int count)
	{
		return baseStream.Read (buffer, offset, count);
	}

	public override long Seek (long offset, SeekOrigin origin)
	{
		return baseStream.Seek (offset + offsetInParentStream, origin);
	}

	public override void Flush ()
	{
		throw new NotSupportedException ();
	}

	public override void SetLength (long value)
	{
		throw new NotSupportedException ();
	}

	public override void Write (byte [] buffer, int offset, int count)
	{
		throw new NotSupportedException ();
	}
}
