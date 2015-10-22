using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

#if FEATURE_HANDLES_ARE_SAFE_HANDLES

namespace Java.Interop
{
	sealed class JniWeakGlobalReference : JniReferenceSafeHandle {

		public JniWeakGlobalReference ()
		{
		}

		public JniWeakGlobalReference (IntPtr handle)
		{
			SetHandle (handle);
		}

		protected override bool ReleaseHandle ()
		{
			if (JniEnvironment.HasCurrent) {
				var r = new JniObjectReference (this, JniObjectReferenceType.WeakGlobal);
				JniEnvironment.Current.JavaVM.JniObjectReferenceManager.DeleteWeakGlobalReference (ref r);
			}
			return true;
		}
	}
	
}

#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES
