using System;
using System.IO;

namespace Android.Runtime
{
	public class OutputStreamInvoker : Stream
	{
		public Java.IO.OutputStream BaseOutputStream {get; private set;}

		public OutputStreamInvoker (Java.IO.OutputStream stream)
		{
			if (stream == null)
				throw new ArgumentNullException (nameof (stream));

			this.BaseOutputStream = stream;
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.io.OutputStream.close()` throws an exception, see:
		//
		//     https://developer.android.com/reference/java/io/OutputStream?hl=en#close()
		//
		public override void Close ()
		{
			base.Close ();
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.io.OutputStream.close()` throws an exception, see:
		//
		//     https://developer.android.com/reference/java/io/OutputStream?hl=en#close()
		//
		protected override void Dispose (bool disposing)
		{
			if (disposing && BaseOutputStream != null) {
				try {
					if (BaseOutputStream.PeerReference.IsValid) {
						BaseOutputStream.Close ();
					}
					BaseOutputStream.Dispose ();
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
		//    `java.io.OutputStream.flush()` throws an exception, see:
		//
		//     https://developer.android.com/reference/java/io/OutputStream?hl=en#flush()
		//
		public override void Flush ()
		{
			try {
				BaseOutputStream.Flush ();
			} catch (Java.IO.IOException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new IOException (ex.Message, ex);
			}
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

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.io.OutputStream.write(byte[],int,int)` throws an exception, see:
		//
		//     https://developer.android.com/reference/java/io/OutputStream?hl=en#write(byte%5B%5D)
		//
		public override void Write (byte[] buffer, int offset, int count)
		{
			try {
				BaseOutputStream.Write (buffer, offset, count);
			} catch (Java.IO.IOException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new IOException (ex.Message, ex);
			}
		}

		public override bool CanRead { get { return false; } }
		public override bool CanSeek { get { return false; } }
		public override bool CanWrite { get { return true; } }

		public override long Length { get { throw new NotSupportedException (); } }

		public override long Position {
			get { throw new NotSupportedException (); }
			set { throw new NotSupportedException (); }
		}
		
		public static Stream? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			return FromNative (handle, transfer);
		}

		internal static Stream? FromNative (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			var inst = (IJavaObject?) Java.Lang.Object.PeekObject (handle);

			if (inst == null)
				inst = (IJavaObject) Java.Interop.TypeManager.CreateInstance (handle, transfer);
			else
				JNIEnv.DeleteRef (handle, transfer);

			return new OutputStreamInvoker ((Java.IO.OutputStream)inst);
		}
	}
}
