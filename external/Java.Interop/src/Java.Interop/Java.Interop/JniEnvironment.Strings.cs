using System;
using System.Diagnostics;

namespace Java.Interop
{
	partial class JniEnvironment {

		partial class Strings {

			public static unsafe JniObjectReference NewString (string value)
			{
				if (value == null)
					return new JniObjectReference ();
				fixed (char* s = value)
					return NewString ((IntPtr) s, value.Length);
			}

#if !XA_INTEGRATION
			internal static JniObjectReference NewString (object value)
			{
				Debug.Assert (value == null || (value is string), "Expected value==null or string; was: " + (value ?? string.Empty).GetType ().FullName);
				return NewString ((string) value);
			}

			public static string ToString (IntPtr reference)
			{
				return ToString (new JniObjectReference (reference));
			}

			internal static unsafe string ToString (ref JniObjectReference reference, JniObjectReferenceOptions transfer, Type targetType)
			{
				Debug.Assert (targetType == typeof (string), "Expected targetType==typeof(string); was: " + targetType);
				return ToString (ref reference, transfer);
			}
#endif  // !XA_INTEGRATION

			public static unsafe string ToString (JniObjectReference value)
			{
				return ToString (ref value, JniObjectReferenceOptions.CreateNewReference);
			}

			public static unsafe string ToString (ref JniObjectReference value, JniObjectReferenceOptions transfer)
			{
				if (!value.IsValid)
					return null;
				int len = JniEnvironment.Strings.GetStringLength (value);
				var p   = JniEnvironment.Strings.GetStringChars (value, IntPtr.Zero);
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

