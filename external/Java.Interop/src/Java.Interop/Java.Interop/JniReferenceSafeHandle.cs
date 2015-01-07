using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public abstract class JniReferenceSafeHandle : SafeHandle
	{
		public static readonly JniReferenceSafeHandle Null = new JniLocalReference ();

		protected JniReferenceSafeHandle ()
			: this (ownsHandle:true)
		{
		}

		internal JniReferenceSafeHandle (bool ownsHandle)
			: base (IntPtr.Zero, ownsHandle)
		{
		}

		public override bool IsInvalid {
			get {return base.handle == IntPtr.Zero;}
		}

		public JniReferenceType ReferenceType {
			get {
				if (IsInvalid)
					throw new ObjectDisposedException (GetType ().FullName);
				return JniEnvironment.Handles.GetObjectRefType (this);
			}
		}

		internal IntPtr _GetAndClearHandle ()
		{
			var h   = handle;
			handle  = IntPtr.Zero;
			return h;
		}

		public JniGlobalReference NewGlobalRef ()
		{
			return JniEnvironment.Current.JavaVM.JniHandleManager.CreateGlobalReference (this);
		}

		public JniLocalReference NewLocalRef ()
		{
			return JniEnvironment.Current.JavaVM.JniHandleManager.CreateLocalReference (JniEnvironment.Current, this);
		}

		public JniWeakGlobalReference NewWeakGlobalRef ()
		{
			return JniEnvironment.Current.JavaVM.JniHandleManager.CreateWeakGlobalReference (this);
		}

		internal string GetJniTypeName ()
		{
			return JniEnvironment.Types.GetJniTypeNameFromInstance (this);
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, handle.ToString ("x"));
		}
	}

	public class JniInvocationHandle : JniReferenceSafeHandle {

		public JniInvocationHandle (IntPtr handle)
			: base (ownsHandle:false)
		{
			SetHandle (handle);
		}

		protected override bool ReleaseHandle ()
		{
			return true;
		}
	}
}

