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

		internal IntPtr ReturnToJniRef ()
		{
			var h = handle;
			base.handle = IntPtr.Zero;
			// Tehnically we're not destroying it; we're just passing 'ownership' to the JVM.
			// We "destroy" it here so that, accounting-wise, we don't keep counting it.
			JniEnvironment.Current.LogDestroyLocalRef (h);
			return h;
		}
	}
}
