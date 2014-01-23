using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Java.Interop
{

	public class JniGlobalReference : JniReferenceSafeHandle {
		protected override bool ReleaseHandle ()
		{
			JniEnvironment.Current.JavaVM.LogDestroyGlobalRef (handle);
			JniEnvironment.Handles.DeleteGlobalRef (handle);
			return true;
		}
	}
	
}
