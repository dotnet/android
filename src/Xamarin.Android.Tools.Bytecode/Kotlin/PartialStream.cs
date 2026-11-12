using System;
using System.IO;

namespace Xamarin.Android.Tools.Bytecode
{
	// This takes a Stream that contains several separate messages and provides callers
	// a slice of it so it appears like a Stream that only contains a single message.
	// This is more efficient than copying each message into a separate Stream.
	public class PartialStream : Stream
	{
		readonly Stream stream;
		long start;
		long length;

		public PartialStream (Stream stream, long start) : this (stream, start, stream.Length - start) { }

		public PartialStream (Stream stream, long start, long length)
		{
			this.stream = stream;
			this.start = start;
			this.length = length;

			this.stream.Position = start;
		}

		public void MoveNext () => MoveNext (stream.Length - start - length);

		// Move to the next message in the original Stream
		public void MoveNext (long length)
		{
			start += this.length;
			this.length = length;
		}

		public override bool CanRead => stream.CanRead;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length => length;

		public override long Position {
			get => stream.Position - start;
			set => stream.Position = value + start;
		}

		public override void Flush () => stream.Flush ();

		public override int Read (byte [] buffer, int offset, int count) => stream.Read (buffer, offset, (int) Math.Min (count, Length - Position));

		public override long Seek (long offset, SeekOrigin origin) => throw new NotImplementedException ();

		public override void SetLength (long value) => throw new NotImplementedException ();

		public override void Write (byte [] buffer, int offset, int count) => throw new NotImplementedException ();
	}
}
