using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Android.Runtime {

	public static class CharSequence {

		[return: NotNullIfNotNull (parameterName: "val")]
		public static Java.Lang.ICharSequence[]? ArrayFromStringArray (string[]? val)
		{
			if (val == null)
				return null;

			Java.Lang.ICharSequence[] ret = new Java.Lang.ICharSequence [val.Length];
			for (int i = 0; i < val.Length; i++)
				ret [i] = new Java.Lang.String (val [i]);

			return ret;
		}

		[return: NotNullIfNotNull (parameterName: "val")]
		public static string[]? ArrayToStringArray (Java.Lang.ICharSequence[]? val)
		{
			if (val == null)
				return null;

			string[] ret = new string [val.Length];
			for (int i = 0; i < val.Length; i++)
				ret [i] = val [i].ToString ();

			return ret;
		}

		public static IntPtr ToLocalJniHandle (string? value)
		{
			return JNIEnv.NewString (value);
		}

		public static IntPtr ToLocalJniHandle (Java.Lang.ICharSequence? value)
		{
			return value == null ? IntPtr.Zero : JNIEnv.ToLocalJniHandle (value);
		}

		public static IntPtr ToLocalJniHandle (IEnumerable<char>? value)
		{
			if (value == null) {
				return IntPtr.Zero;
			} else if (value is string s) {
				return JNIEnv.NewString (s);
			} else if (value is Java.Lang.ICharSequence) {
				return JNIEnv.ToLocalJniHandle ((Java.Lang.ICharSequence) value);
			} else {
				return ToLocalJniHandle (string.Concat (value));
			}
		}

	}
}

