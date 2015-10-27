using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
namespace Java.Interop
{
	class JniLocalReference : JniReferenceSafeHandle {

		internal JniLocalReference ()
		{
			JniEnvironment.Current.LocalReferences.Add (this);
		}

		public JniLocalReference (IntPtr handle)
		{
			SetHandle (handle);
		}

		protected override bool ReleaseHandle ()
		{
			JniEnvironment.Current.DeleteLocalReference (this, handle);
			return true;
		}

		internal IntPtr ReturnToJniRef ()
		{
			var r = new JniObjectReference (this, JniObjectReferenceType.Local);
			return JniEnvironment.Current.JavaVM.ObjectReferenceManager.ReleaseLocalReference (JniEnvironment.Current, ref r);
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
