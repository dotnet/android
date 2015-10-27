using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES

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
				JniEnvironment.Current.JavaVM.ObjectReferenceManager.DeleteGlobalReference (ref r);
			}
			return true;
		}
	}
}

#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
