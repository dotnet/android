using System;
using System.IO;

namespace Android.Runtime
{
	public class InputStreamInvoker : Stream
	{
		public Java.IO.InputStream BaseInputStream {get; private set;}

		protected Java.Nio.Channels.FileChannel? BaseFileChannel {get; private set;}

		public InputStreamInvoker (Java.IO.InputStream stream)
		{
			if (stream == null)
				throw new ArgumentNullException (nameof (stream));

			BaseInputStream = stream;

			Java.IO.FileInputStream? fileStream = stream as Java.IO.FileInputStream;
			if (fileStream != null)
				BaseFileChannel = fileStream.Channel;
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.io.InputStream.close()` throws an exception, see:
		//
		//     https://developer.android.com/reference/java/io/InputStream?hl=en
		//
		protected override void Dispose (bool disposing)
		{
			if (disposing && BaseInputStream != null) {
				try {
					BaseFileChannel = null;
					if (BaseInputStream.PeerReference.IsValid) {
						BaseInputStream.Close ();
					}
					BaseInputStream.Dispose ();
				} catch (Java.IO.IOException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new IOException (ex.Message, ex);
				}
			}
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.io.InputStream.close()` throws an exception, see:
		//
		//     https://developer.android.com/reference/java/io/InputStream?hl=en
		//
		public override void Close ()
		{
			base.Close ();
		}

		public override void Flush ()
		{
			// No need to flush an input stream
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.io.InputStream.read(byte[], int, int)` throws an exception, see:
		//
		//     https://developer.android.com/reference/java/io/InputStream?hl=en#read(byte%5B%5D,%20int,%20int)
		//
		public override int Read (byte[] buffer, int offset, int count)
		{
			int res;

			try {
				res = BaseInputStream.Read (buffer, offset, count);
			} catch (Java.IO.IOException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new IOException (ex.Message, ex);
			}

			if (res == -1)
				return 0;
			return res;
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			if (BaseFileChannel == null)
				throw new NotSupportedException ();

			switch (origin) {
			case SeekOrigin.Begin:
				BaseFileChannel.Position (offset);
				break;
			case SeekOrigin.Current:
				BaseFileChannel.Position (BaseFileChannel.Position() + offset);
				break;
			case SeekOrigin.End:
				BaseFileChannel.Position (BaseFileChannel.Size() + offset);
				break;
			}
			return BaseFileChannel.Position ();
		}

		public override void SetLength (long value)
		{
			throw new NotSupportedException ();
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException ();
		}

		public override bool CanRead { get { return true; } }
		public override bool CanSeek { get { return (BaseFileChannel != null); } }
		public override bool CanWrite { get { return false; } }

		public override long Length {
			get {
				if (BaseFileChannel != null)
					return BaseFileChannel.Size ();
				else
					throw new NotSupportedException ();
			}
		}

		public override long Position {
			get {
				if (BaseFileChannel != null)
					return BaseFileChannel.Position ();
				else
					throw new NotSupportedException ();
			}
			set {
				if (BaseFileChannel != null)
					BaseFileChannel.Position (value);
				else
					throw new NotSupportedException ();
			}
		}
		
		public static Stream? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			var inst = Java.Lang.Object.GetObject<Java.IO.InputStream> (handle, transfer);
			if (inst is null)
				return null;
			return new InputStreamInvoker (inst);
		}
	}
}
