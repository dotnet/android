using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Java.Interop
{

	public class JniLocalReference : JniReferenceSafeHandle {
		JniLocalReference ()
		{
		}

		protected override bool ReleaseHandle ()
		{
			JavaVM.Current.LogDestroyLocalRef (handle);
			JniHandles._DeleteLocalRef (handle);
			return true;
		}
	}
}
