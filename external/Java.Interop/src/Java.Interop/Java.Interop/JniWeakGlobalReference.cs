using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Java.Interop
{

	public class JniWeakGlobalReference : JniReferenceSafeHandle {
		protected override bool ReleaseHandle ()
		{
			JavaVM.Current.LogDestroyWeakGlobalRef (handle);
			JniHandles._DeleteWeakGlobalRef (handle);
			return true;
		}
	}
	
}
