using System;
using System.IO;

namespace ApplicationUtility;

/// <summary>
/// A read-only stream that represents a sub-range of a parent stream.
/// All reads and seeks are constrained to the specified offset and length within the parent.
/// </summary>
class SubStream : Stream
{
	readonly Stream baseStream;
	readonly long length;
	readonly long offsetInParentStream;
	readonly long subEndInParent;

	long position = 0;

	public override bool CanRead => true;
	public override bool CanSeek => true;
	public override bool CanWrite => false;
	public override bool CanTimeout => baseStream.CanTimeout;
	public override long Length => length;

	public override long Position {
		get => position;
		set {
			if (value < 0) {
				throw new ArgumentOutOfRangeException ("Position must not be less than zero.");
			}

			if (value > length) {
				throw new ArgumentOutOfRangeException ("Position must not be larger than stream length.");
			}

			position = value;
			baseStream.Position = offsetInParentStream + value;
		}
	}

	public override int ReadTimeout {
		get => baseStream.ReadTimeout;
		set => throw new NotSupportedException ();
	}

	public SubStream (Stream baseStream, long offsetInParentStream, long length)
	{
		if (length < 0) {
			throw new ArgumentOutOfRangeException (nameof (length), "Must not be less than zero.");
		}

		if (offsetInParentStream < 0) {
			throw new ArgumentOutOfRangeException (nameof (offsetInParentStream), "Offset into parent stream must not be less than zero.");
		}

		if (!baseStream.CanSeek) {
			throw new InvalidOperationException ("Base stream must support seeking");
		}

		if (!baseStream.CanRead) {
			throw new InvalidOperationException ("Base stream must support reading");
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
		subEndInParent = offsetInParentStream + length;
	}

	public override int Read (byte[] buffer, int offset, int count)
	{
		if (count < 0) {
			throw new ArgumentOutOfRangeException (nameof (count), "Must not be less than zero.");
		}


		baseStream.Seek (offsetInParentStream + position, SeekOrigin.Begin);
		int toRead = (int)Math.Min (count, length - position);
		int nread = baseStream.Read (buffer, offset, toRead);
		position += nread;

		return nread;
	}

	public override long Seek (long offset, SeekOrigin origin)
	{
		switch (origin) {
			case SeekOrigin.Begin:
				if (offset > length) {
					throw new ArgumentOutOfRangeException (nameof (offset), "Must not exceed stream length.");
				}

				if (offset < 0) {
					throw new ArgumentOutOfRangeException (nameof (offset), "Must not point to before the stream beginning.");
				}
				position = offset;
				baseStream.Seek (offsetInParentStream + offset, SeekOrigin.Begin);
				break;

			case SeekOrigin.Current:
				long newPos = position + offset;
				if (newPos < 0) {
					throw new InvalidOperationException ("Cannot seek to before the stream beginning.");
				}

				if (newPos > length) {
					throw new InvalidOperationException ("Cannot see to beyond the stream end.");
				}

				position = newPos;
				baseStream.Seek (newPos, SeekOrigin.Current);
				break;

			case SeekOrigin.End:
				if (offset > 0) {
					throw new ArgumentOutOfRangeException (nameof (offset), "Must not point to beyond the end of stream.");
				}

				if (offset < -length) {
					throw new ArgumentOutOfRangeException (nameof (offset), "Must not point to before the beginning of stream.");
				}
				position = length + offset;
				baseStream.Seek (-((baseStream.Length) - (subEndInParent + offset)), SeekOrigin.End);
				break;
		}

		return position;
	}

	public override void Flush () => baseStream.Flush ();

	public override void SetLength (long value)
	{
		throw new NotSupportedException ();
	}

	public override void Write (byte [] buffer, int offset, int count)
	{
		throw new NotSupportedException ();
	}
}
