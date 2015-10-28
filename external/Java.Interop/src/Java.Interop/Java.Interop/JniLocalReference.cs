using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
namespace Java.Interop
{
	class JniLocalReference : JniReferenceSafeHandle {

		internal JniLocalReference ()
		{
			JniEnvironment.AddLocalReference (this);
		}

		public JniLocalReference (IntPtr handle)
		{
			SetHandle (handle);
		}

		protected override bool ReleaseHandle ()
		{
			JniEnvironment.DeleteLocalReference (this, handle);
			return true;
		}

		internal IntPtr ReturnToJniRef ()
		{
			var r = new JniObjectReference (this, JniObjectReferenceType.Local);
			return JniEnvironment.Runtime.ObjectReferenceManager.ReleaseLocalReference (JniEnvironment.CurrentInfo, ref r);
		}

		internal JniAllocObjectRef ToAllocObjectRef ()
		{
			var h   = handle;
			handle  = IntPtr.Zero;
			return new JniAllocObjectRef (h);
		}
	}
}
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
