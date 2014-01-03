using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Java.Interop
{

	public class JniLocalReference : JniReferenceSafeHandle {
		JniLocalReference ()
		{
			Debug.WriteLine ("# JniLocalReference..ctor: handle=0x{0}", handle.ToString ("x"));
		}

		protected override bool ReleaseHandle ()
		{
			Debug.WriteLine ("# JniLocalReference.ReleaseHandle: handle=0x{0}", handle.ToString ("x"));
			JniHandles._DeleteLocalRef (handle);
			return true;
		}
	}
}
