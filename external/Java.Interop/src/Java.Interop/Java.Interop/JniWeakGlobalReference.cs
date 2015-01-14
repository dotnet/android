using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Java.Interop
{

	public class JniWeakGlobalReference : JniReferenceSafeHandle {
		protected override bool ReleaseHandle ()
		{
			if (JniEnvironment.HasCurrent)
				JniEnvironment.Current.JavaVM.JniHandleManager.DeleteWeakGlobalReference (handle);
			return true;
		}
	}
	
}
