using System;
using System.IO;

namespace ApplicationUtility;

class SharedLibraryPayloadStream : Stream
{
	readonly Stream baseStream;
	readonly long length;
	readonly long offsetInBaseStream;

	public override bool CanRead => true;
	public override bool CanSeek => true;
	public override bool CanWrite => false;
	public override long Length => length;

	public override long Position {
		get => throw new NotSupportedException ();
		set => throw new NotSupportedException ();
	}

	public SharedLibraryPayloadStream (Stream baseStream, long offset, long length)
	{
		if (!baseStream.CanSeek) {
			throw new InvalidOperationException ($"Base stream must support seeking");
		}

		if (!baseStream.CanRead) {
			throw new InvalidOperationException ($"Base stream must support reading");
		}

		if (offset >= baseStream.Length) {
			throw new ArgumentOutOfRangeException (nameof (offset), $"{offset} exceeds length of the base stream ({baseStream.Length})");
		}

		if (offset + length > baseStream.Length) {
			throw new InvalidOperationException ($"Not enough data in base stream after offset {offset}, length of {length} bytes is too big.");
		}

		this.baseStream = baseStream;
		this.length = length;
		offsetInBaseStream = offset;
	}

	public override int Read (byte [] buffer, int offset, int count)
	{
		return baseStream.Read (buffer, offset, count);
	}

	public override long Seek (long offset, SeekOrigin origin)
	{
		return baseStream.Seek (offset + offsetInBaseStream, origin);
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
