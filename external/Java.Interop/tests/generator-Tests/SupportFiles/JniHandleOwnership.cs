#if !JAVA_INTEROP1

using System;

namespace Android.Runtime
{
	public enum JniHandleOwnership {
		DoNotTransfer         = 0,
		TransferLocalRef      = 1,
		TransferGlobalRef     = 2,
	}
}

#endif  // !JAVA_INTEROP1
