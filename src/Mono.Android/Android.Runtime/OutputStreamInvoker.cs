using System;
using System.IO;

namespace Android.Runtime
{
	public class OutputStreamInvoker : Stream
	{
		public Java.IO.OutputStream BaseOutputStream {get; private set;}

		public OutputStreamInvoker (Java.IO.OutputStream stream)
		{
			this.BaseOutputStream = stream;
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing && BaseOutputStream != null) {
				BaseOutputStream.Dispose ();
				BaseOutputStream = null;
			}
		}

		public override void Flush ()
		{
			BaseOutputStream.Flush ();
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException ();
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
			BaseOutputStream.Write (buffer, offset, count);
		}

		public override bool CanRead { get { return false; } }
		public override bool CanSeek { get { return false; } }
		public override bool CanWrite { get { return true; } }

		public override long Length { get { throw new NotSupportedException (); } }

		public override long Position {
			get { throw new NotSupportedException (); }
			set { throw new NotSupportedException (); }
		}
		
		[Preserve (Conditional=true)]
		public static Stream FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			return FromNative (handle, transfer);
		}

		internal static Stream FromNative (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			IJavaObject inst = Java.Lang.Object.PeekObject (handle);

			if (inst == null)
				inst = Java.Interop.TypeManager.CreateInstance (handle, transfer);
			else
				JNIEnv.DeleteRef (handle, transfer);

			return new OutputStreamInvoker ((Java.IO.OutputStream)inst);
		}
	}
}

