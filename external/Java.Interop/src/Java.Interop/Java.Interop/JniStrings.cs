using System;

namespace Java.Interop
{
	static partial class JniStrings {

		public static unsafe string ToString (JniReferenceSafeHandle value)
		{
			if (value == null || value.IsInvalid)
				return null;
			int len = JniStrings.GetStringLength (value);
			var p   = JniStrings.GetStringChars (value, IntPtr.Zero);
			try {
				return new string ((char*) p, 0, len);
			} finally {
				JniStrings.ReleaseStringChars (value, p);
			}
		}
	}
}

