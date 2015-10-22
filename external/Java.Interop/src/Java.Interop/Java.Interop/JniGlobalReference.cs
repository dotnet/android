using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

#if FEATURE_HANDLES_ARE_SAFE_HANDLES

namespace Java.Interop
{
	sealed class JniGlobalReference : JniReferenceSafeHandle {

		public JniGlobalReference ()
		{
		}

		public JniGlobalReference (IntPtr handle)
		{
			SetHandle (handle);
		}

		protected override bool ReleaseHandle ()
		{
			if (JniEnvironment.HasCurrent) {
				var r = new JniObjectReference (this, JniObjectReferenceType.Global);
				JniEnvironment.Current.JavaVM.JniObjectReferenceManager.DeleteGlobalReference (ref r);
			}
			return true;
		}
	}
}

#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES
