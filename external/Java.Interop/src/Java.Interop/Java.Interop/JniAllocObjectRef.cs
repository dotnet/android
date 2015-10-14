using System;

namespace Java.Interop {

	sealed class JniAllocObjectRef : JniLocalReference
	{
		public JniAllocObjectRef (IntPtr handle)
		{
			SetHandle (handle);
		}
	}
}

