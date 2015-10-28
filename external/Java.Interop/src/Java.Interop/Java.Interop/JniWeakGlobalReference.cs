using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES

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
			var r = new JniObjectReference (this, JniObjectReferenceType.WeakGlobal);
			JniEnvironment.Runtime.ObjectReferenceManager.DeleteWeakGlobalReference (ref r);
			return true;
		}
	}
	
}

#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
