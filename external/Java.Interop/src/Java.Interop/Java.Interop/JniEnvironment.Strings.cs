using System;

namespace Java.Interop
{
	partial class JniEnvironment {

		partial class Strings {

			public static unsafe JniLocalReference NewString (string value)
			{
				fixed (char* s = value)
					return NewString ((IntPtr) s, value.Length);
			}

			public static string ToString (IntPtr handle)
			{
				using (var r = new JniInvocationHandle (handle))
					return ToString (r);
			}

			public static unsafe string ToString (JniReferenceSafeHandle value, JniHandleOwnership transfer = JniHandleOwnership.DoNotTransfer)
			{
				if (value == null || value.IsInvalid)
					return null;
				int len = JniEnvironment.Strings.GetStringLength (value);
				var p   = JniEnvironment.Strings.GetStringChars (value, IntPtr.Zero);
				try {
					return new string ((char*) p, 0, len);
				} finally {
					JniEnvironment.Strings.ReleaseStringChars (value, p);
					JniEnvironment.Handles.Dispose (value, transfer);
				}
			}
		}
	}
}

