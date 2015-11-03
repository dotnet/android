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

			internal static JniObjectReference NewString (object value)
			{
				Debug.Assert (value == null || (value is string), "Expected value==null or string; was: " + (value ?? "").GetType ().FullName);
				return NewString ((string) value);
			}

			public static string ToString (IntPtr handle)
			{
				return ToString (new JniObjectReference (handle));
			}

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

			internal static unsafe string ToString (ref JniObjectReference value, JniObjectReferenceOptions transfer, Type targetType)
			{
				Debug.Assert (targetType == typeof (string), "Expected targetType==typeof(string); was: " + targetType);
				return ToString (ref value, transfer);
			}
		}
	}
}

