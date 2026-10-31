using System;
using System.IO;

namespace Android.Runtime {

	[Register ("mono/android/runtime/InputStreamAdapter")]
	public sealed class InputStreamAdapter : Java.IO.InputStream {

		public Stream BaseStream {get; private set;}

		public InputStreamAdapter (System.IO.Stream stream)
			: base (
					// SAFETY: The complete ASCII literal supplies stable, NUL-terminated JNI class-name bytes.
					unsafe (JNIEnv.AllocObject ("mono/android/runtime/InputStreamAdapter"u8)),
					JniHandleOwnership.TransferLocalRef)
		{
			unsafe {
				// SAFETY: the constructor uses the literal JNI signature with the matching argument order; this peer owns its newly allocated handle through initialization and existing cleanup.
				JNIEnv.FinishCreateInstance (Handle, "()V"u8);

				this.BaseStream = stream;
			}
		}

		public override void Close ()
		{
			BaseStream.Close ();
		}

		public override int Read ()
		{
			return BaseStream.ReadByte ();
		}

		public override int Read (byte[]? bytes)
		{
			ArgumentNullException.ThrowIfNull (bytes);
			return Read (bytes, 0, bytes.Length);
		}

		public override int Read (byte[]? bytes, int offset, int length)
		{
			ArgumentNullException.ThrowIfNull (bytes);
			int res = BaseStream.Read (bytes, offset, length);
			if (res == 0)
				return -1;
			return res;
		}

		public static IntPtr ToLocalJniHandle (Stream? value)
		{
			if (value == null)
				return IntPtr.Zero;

			var v = value as InputStreamInvoker;
			if (v != null)
				return JNIEnv.ToLocalJniHandle (v.BaseInputStream);

			return JNIEnv.ToLocalJniHandle (new InputStreamAdapter (value));
		}
	}
}

