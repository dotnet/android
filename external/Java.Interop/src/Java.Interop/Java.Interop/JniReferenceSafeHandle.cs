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

		public JniGlobalReference NewGlobalRef ()
		{
			return JniEnvironment.Handles.NewGlobalRef (this);
		}

		public JniLocalReference NewLocalRef ()
		{
			return JniEnvironment.Handles.NewLocalRef (this);
		}

		public JniWeakGlobalReference NewWeakGlobalRef ()
		{
			return JniEnvironment.Handles.NewWeakGlobalRef (this);
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, handle.ToString ("x"));
		}
	}

	class JniInvocationHandle : JniReferenceSafeHandle {

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

