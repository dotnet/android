using System;

#if FEATURE_HANDLES_ARE_SAFE_HANDLES
namespace Java.Interop {

	sealed class JniAllocObjectRef : JniLocalReference
	{
		public JniAllocObjectRef (IntPtr handle)
		{
			SetHandle (handle);
		}
	}
}
#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES
