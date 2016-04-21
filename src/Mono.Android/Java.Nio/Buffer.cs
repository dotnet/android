using System;
using Android.Runtime;

namespace Java.Nio {

	public partial class Buffer {

		public IntPtr GetDirectBufferAddress ()
		{
#if ANDROID_9
			if (IsDirect)
				return JNIEnv.GetDirectBufferAddress (Handle);
#endif
			return IntPtr.Zero;
		}
	}
}
