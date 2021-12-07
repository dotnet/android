#if !JAVA_INTEROP1
using System;
using System.IO;

namespace Android.Runtime {

	[Register ("mono/android/runtime/InputStreamAdapter")]
	public sealed class InputStreamAdapter : Java.IO.InputStream {

		public Stream BaseStream {get; private set;}

		public InputStreamAdapter (System.IO.Stream stream)
			: base (
					JNIEnv.StartCreateInstance ("mono/android/runtime/InputStreamAdapter", "()V"),
					JniHandleOwnership.TransferLocalRef)
		{
			throw new NotImplementedException ();
		}

		public override void Close ()
		{
			throw new NotImplementedException ();
		}

		public override int Read ()
		{
			throw new NotImplementedException ();
		}

		public override int Read (byte[] bytes)
		{
			throw new NotImplementedException ();
		}

		public override int Read (byte[] bytes, int offset, int length)
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
