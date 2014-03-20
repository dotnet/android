using System;

namespace Java.Interop {

	class JniAllocObjectRef : JniLocalReference
	{
		public JniAllocObjectRef (IntPtr handle)
		{
			SetHandle (handle);
		}
	}
}

