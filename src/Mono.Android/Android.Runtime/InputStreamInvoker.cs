using System;
using System.IO;

namespace Android.Runtime
{
	public class InputStreamInvoker : Stream
	{
		public Java.IO.InputStream BaseInputStream {get; private set;}

		public InputStreamInvoker (Java.IO.InputStream stream)
		{
			this.BaseInputStream = stream;
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing && BaseInputStream != null) {
				BaseInputStream.Dispose ();
				BaseInputStream = null;
			}
		}

		public override void Flush ()
		{
			// No need to flush an input stream
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			int res = BaseInputStream.Read (buffer, offset, count);
			if (res == -1)
				return 0;
			return res;
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			throw new NotSupportedException ();
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
		public override bool CanSeek { get { return false; } }
		public override bool CanWrite { get { return false; } }

		public override long Length { get { throw new NotSupportedException (); } }

		public override long Position {
			get { throw new NotSupportedException (); }
			set { throw new NotSupportedException (); }
		}
		
		[Preserve (Conditional=true)]
		public static Stream FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			IJavaObject inst = Java.Lang.Object.PeekObject (handle);

			if (inst == null)
				inst = Java.Interop.TypeManager.CreateInstance (handle, transfer);
			else
				JNIEnv.DeleteRef (handle, transfer);

			return new InputStreamInvoker ((Java.IO.InputStream)inst);
		}
	}
}

