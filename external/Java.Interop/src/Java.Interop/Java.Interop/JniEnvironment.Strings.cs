#nullable enable

using System;
using System.Diagnostics;

namespace Java.Interop
{
	partial class JniEnvironment {

		partial class Strings {

			public static unsafe JniObjectReference NewString (string? value)
			{
				if (value == null)
					return new JniObjectReference ();
				fixed (char* s = value)
					return NewString (s, value.Length);
			}

			public static string? ToString (IntPtr reference)
			{
				return ToString (new JniObjectReference (reference));
			}

			internal static unsafe string? ToString (ref JniObjectReference reference, JniObjectReferenceOptions transfer, Type targetType)
			{
				Debug.Assert (targetType == typeof (string), "Expected targetType==typeof(string); was: " + targetType);
				return ToString (ref reference, transfer);
			}

			public static unsafe string? ToString (JniObjectReference value)
			{
				return ToString (ref value, JniObjectReferenceOptions.Copy);
			}

			public static unsafe string? ToString (ref JniObjectReference value, JniObjectReferenceOptions transfer)
			{
				if (!value.IsValid)
					return null;
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

