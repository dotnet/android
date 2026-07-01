#if !JAVA_INTEROP1
using System;
using System.IO;

namespace Android.Runtime
{
	public class OutputStreamInvoker : Stream
	{
		public Java.IO.OutputStream BaseOutputStream {get; private set;}

		public OutputStreamInvoker (Java.IO.OutputStream stream)
		{
			throw new NotImplementedException ();
		}

		protected override void Dispose (bool disposing)
		{
			throw new NotImplementedException ();
		}

		public override void Flush ()
		{
			throw new NotImplementedException ();
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

		public override bool CanRead { get { return false; } }
		public override bool CanSeek { get { return false; } }
		public override bool CanWrite { get { return true; } }

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

		internal static Stream FromNative (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}
	}
}
#endif // !JAVA_INTEROP1
