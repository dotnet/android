#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Java.Interop {

	readonly ref struct JniUtf8String {
		readonly IntPtr owned;

		/// <safety>
		/// The address must only be consumed while this wrapper is alive.
		/// </safety>
		internal unsafe IntPtr Pointer => owned;

		internal JniUtf8String (string value) => owned = Marshal.StringToCoTaskMemUTF8 (value);

		public void Dispose ()
		{
			unsafe {
				// SAFETY: owned was allocated by StringToCoTaskMemUTF8 and is freed once after its last use.
				Marshal.ZeroFreeCoTaskMemUTF8 (owned);
			}
		}
	}
}
