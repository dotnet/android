#nullable enable

using System;
using System.Diagnostics;

namespace Java.Interop
{
	partial class JniEnvironment {

		partial class Strings {

			public static JniObjectReference NewString (string? value)
			{
				if (value == null)
					return new JniObjectReference ();
				unsafe {
					// SAFETY: Pin the managed string for the synchronous copy of exactly value.Length UTF-16 characters.
					fixed (char* s = value)
						return NewString (s, value.Length);
				}
			}

			public static string? ToString (IntPtr reference)
			{
				return ToString (new JniObjectReference (reference));
			}

			internal static string? ToString (ref JniObjectReference reference, JniObjectReferenceOptions transfer, Type targetType)
			{
				Debug.Assert (targetType == typeof (string), "Expected targetType==typeof(string); was: " + targetType);
				return ToString (ref reference, transfer);
			}

			public static string? ToString (JniObjectReference value)
			{
				return ToString (ref value, JniObjectReferenceOptions.Copy);
			}

			public static string? ToString (ref JniObjectReference value, JniObjectReferenceOptions transfer)
			{
				if (!value.IsValid)
					return null;
				unsafe {
					// SAFETY: JNI owns the returned len-character buffer until ReleaseStringChars in finally.
					// Copy it before release and preserve the existing reference-transfer cleanup on failure.
					int len = JniEnvironment.Strings.GetStringLength (value);
					var p   = JniEnvironment.Strings.GetStringChars (value, null);
					try {
						return new string ((char*) p, 0, len);
					} finally {
						JniEnvironment.Strings.ReleaseStringChars (value, p);
						JniObjectReference.Dispose (ref value, transfer);
					}
				}
			}
		}
	}
}
