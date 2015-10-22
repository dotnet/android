using System;

namespace Java.Interop
{
	[Flags]
	public enum JniObjectReferenceOptions
	{
		Invalid                     = 0,
		CreateNewReference          = 1 << 0,   // DoNotTransfer
		DisposeSourceReference      = 1 << 1,   // Transfer

		/*
		Unregistered                = 4,
		 */
	}
}

