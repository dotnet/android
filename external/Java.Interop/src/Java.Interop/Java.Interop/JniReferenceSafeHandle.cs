using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public abstract class JniReferenceSafeHandle : SafeHandle
	{
		protected JniReferenceSafeHandle ()
			: base (IntPtr.Zero, true)
		{
		}

		public override bool IsInvalid {
			get {return base.handle == IntPtr.Zero;}
		}

		public JObjectRefType RefType {
			get {
				if (IsInvalid)
					throw new ObjectDisposedException (GetType ().FullName);
				return JniHandles.GetObjectRefType (this);
			}
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, handle.ToString ("x"));
		}
	}
}

