﻿using System;
using System.IO;

namespace Android.Runtime
{
	public class InputStreamInvoker : Stream
	{
		public Java.IO.InputStream BaseInputStream {get; private set;}

		public InputStreamInvoker (Java.IO.InputStream stream)
		{
			if (stream == null)
				throw new ArgumentNullException (nameof (stream));

			this.BaseInputStream = stream;
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
					BaseInputStream.Close ();
					BaseInputStream.Dispose ();
					BaseInputStream = null;
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
			try {
				BaseInputStream.Close ();
			} catch (Java.IO.IOException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new IOException (ex.Message, ex);
			}
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
