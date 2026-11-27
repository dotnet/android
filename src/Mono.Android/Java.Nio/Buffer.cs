using System;
using Android.Runtime;

namespace Java.Nio {

	public partial class Buffer {

		public IntPtr GetDirectBufferAddress ()
		{
			unsafe {
				// SAFETY: the live Buffer peer supplies the JNI handle, retained by GC.KeepAlive (this) across the JNI call;
				// obtaining its address does not dereference or extend the backing memory lifetime.
				if (IsDirect) {
					IntPtr address = JNIEnv.GetDirectBufferAddress (Handle);
					GC.KeepAlive (this);
					return address;
				}
				return IntPtr.Zero;
			}
		}
	}
}
