using System;

namespace Java.Interop
{
	[Flags]
	public enum JniObjectReferenceOptions
	{
		None                        = 0,
		CreateNewReference          = 1 << 0,   // DoNotTransfer
		DisposeSourceReference      = 1 << 1,   // Transfer
		DoNotRegisterWithRuntime    = 1 << 2,
	}
}

