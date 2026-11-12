using System;
using Android.Runtime;

namespace Java.Nio {

	public partial class Buffer {

		public IntPtr GetDirectBufferAddress ()
		{
			unsafe {
				// SAFETY: the live Buffer peer supplies the JNI handle; obtaining its address does not dereference or extend the backing memory lifetime.
#if ANDROID_9
				if (IsDirect)
					return JNIEnv.GetDirectBufferAddress (Handle);
#endif
				return IntPtr.Zero;
			}
		}
	}
}
