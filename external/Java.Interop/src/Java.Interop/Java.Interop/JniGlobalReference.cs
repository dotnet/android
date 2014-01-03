using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Java.Interop
{

	public class JniGlobalReference : JniReferenceSafeHandle {
		protected override bool ReleaseHandle ()
		{
			JniHandles._DeleteGlobalRef (handle);
			return true;
		}
	}
	
}
