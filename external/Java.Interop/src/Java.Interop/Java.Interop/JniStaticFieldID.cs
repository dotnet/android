using System;
using System.Runtime.InteropServices;

namespace Java.Interop {

	public class JniStaticFieldID : SafeHandle
	{
		JniStaticFieldID ()
			: base (IntPtr.Zero, ownsHandle:false)
		{
			JavaVM.Current.Track (this);
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

