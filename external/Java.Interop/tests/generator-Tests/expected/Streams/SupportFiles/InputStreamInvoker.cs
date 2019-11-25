using System;
using System.IO;

namespace Android.Runtime
{
	public class InputStreamInvoker : Stream
	{
		public Java.IO.InputStream BaseInputStream {get; private set;}

		public InputStreamInvoker (Java.IO.InputStream stream)
		{
			throw new NotImplementedException ();
		}

		protected override void Dispose (bool disposing)
		{
			throw new NotImplementedException ();
		}

		public override void Flush ()
		{
			// No need to flush an input stream
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException ();
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			throw new NotImplementedException ();
		}

		public override void SetLength (long value)
		{
			throw new NotImplementedException ();
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException ();
		}

		public override bool CanRead { get { return true; } }
		public override bool CanSeek { get { return false; } }
		public override bool CanWrite { get { return false; } }

		public override long Length { get { throw new NotImplementedException (); } }

		public override long Position {
			get { throw new NotImplementedException (); }
			set { throw new NotImplementedException (); }
		}
		
		[Preserve (Conditional=true)]
		public static Stream FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}
	}
}

