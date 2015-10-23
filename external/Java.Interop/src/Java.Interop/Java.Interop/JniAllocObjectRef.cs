using System;

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
namespace Java.Interop {

	sealed class JniAllocObjectRef : JniLocalReference
	{
		public JniAllocObjectRef (IntPtr handle)
		{
			SetHandle (handle);
		}
	}
}
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
