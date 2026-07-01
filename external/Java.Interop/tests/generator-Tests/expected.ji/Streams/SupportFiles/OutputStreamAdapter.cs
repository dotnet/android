#if !JAVA_INTEROP1
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
			throw new NotImplementedException ();
		}

		public override void Close ()
		{
			throw new NotImplementedException ();
		}

		public override void Flush ()
		{
			throw new NotImplementedException ();
		}

		public override void Write (byte[] buffer)
		{
			throw new NotImplementedException ();
		}

		public override void Write (byte[] buffer, int offset, int length)
		{
			throw new NotImplementedException ();
		}

		public override void Write (int oneByte)
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static IntPtr ToLocalJniHandle (Stream value)
		{
			throw new NotImplementedException ();
		}
	}
}
#endif // !JAVA_INTEROP1
