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
			JniEnvironment.Current.LogDestroyLocalRef (handle);
			JniEnvironment.Handles.DeleteLocalRef (handle);
			return true;
		}
	}
}
