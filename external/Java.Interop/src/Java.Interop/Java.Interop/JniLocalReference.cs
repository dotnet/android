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
			JniEnvironment.Current.JavaVM.JniHandleManager.DeleteLocalReference (JniEnvironment.Current, handle);
			return true;
		}

		internal IntPtr ReturnToJniRef ()
		{
			return JniEnvironment.Current.JavaVM.JniHandleManager.ReleaseLocalReference (JniEnvironment.Current, this);
		}

		internal JniAllocObjectRef ToAllocObjectRef ()
		{
			var h   = handle;
			handle  = IntPtr.Zero;
			return new JniAllocObjectRef (h);
		}
	}
}
