using System;

namespace Android.Runtime
{
	[Flags]
	public enum JniHandleOwnership {
		DoNotTransfer         = 0,
		TransferLocalRef      = 1,
		TransferGlobalRef     = 2,
		DoNotRegister         = 0x10,
	}
}

