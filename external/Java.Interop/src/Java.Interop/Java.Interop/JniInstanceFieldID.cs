using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public class JniInstanceFieldID : SafeHandle
	{
		JniInstanceFieldID ()
			: base (IntPtr.Zero, ownsHandle:false)
		{
		}

		protected override bool ReleaseHandle ()
		{
			return true;
		}

		public override bool IsInvalid {
			get {
				return handle == IntPtr.Zero;
			}
		}
	}
}

