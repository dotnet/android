using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Java.Interop
{

	public class JniLocalReference : JniReferenceSafeHandle {

		internal JniLocalReference ()
		{
		}

		protected override bool ReleaseHandle ()
		{
			JniEnvironment.Current.JavaVM.LogDestroyLocalRef (handle);
			JniHandles._DeleteLocalRef (handle);
			return true;
		}
	}
}
