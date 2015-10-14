using System;

namespace Java.Interop
{
	[Flags]
	public enum JniHandleOwnership // JniObjectReferenceOptions
	{
		DoNotTransfer  = 0,
		Transfer       = 1,
		Invalid        = 2,

		/*
		Invalid                     = 0,
		CreateNewReference          = 1, // DoNotTransfer
		DisposeSourceReference      = 2, // Transfer
		Unregistered                = 4,
		 */
	}
}

