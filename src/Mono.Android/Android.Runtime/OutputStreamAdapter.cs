using System;
using System.IO;

namespace Android.Runtime
{
	[Register ("mono/android/runtime/OutputStreamAdapter")]
	public class OutputStreamAdapter : Java.IO.OutputStream
	{
		public Stream BaseStream {get; private set;}

		[Register (".ctor", "()V", "")]
		public OutputStreamAdapter (System.IO.Stream stream)
			: base (
					JNIEnv.StartCreateInstance ("mono/android/runtime/OutputStreamAdapter", "()V"),
					JniHandleOwnership.TransferLocalRef)
		{
			JNIEnv.FinishCreateInstance (Handle, "()V");

			this.BaseStream = stream;
		}

		public override void Close ()
		{
			BaseStream.Close ();
		}

		public override void Flush ()
		{
			BaseStream.Flush ();
		}

		public override void Write (byte[] buffer)
		{
			BaseStream.Write (buffer, 0, buffer.Length);
		}

		public override void Write (byte[] buffer, int offset, int length)
		{
			BaseStream.Write (buffer, offset, length);
		}

		public override void Write (int oneByte)
		{
			BaseStream.WriteByte ((byte)oneByte);
		}

		[Preserve (Conditional=true)]
		public static IntPtr ToLocalJniHandle (Stream value)
		{
			if (value == null)
				return IntPtr.Zero;

			var v = value as OutputStreamInvoker;
			if (v != null)
				return JNIEnv.ToLocalJniHandle (v.BaseOutputStream);

			return JNIEnv.ToLocalJniHandle (new OutputStreamAdapter (value));
		}
	}
}

