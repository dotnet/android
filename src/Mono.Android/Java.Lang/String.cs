using System;
using Android.Runtime;

namespace Java.Lang {

	public partial class String {

		[Register (".ctor", "()V", "")]
		public String ()
			: base (JNIEnv.NewString (""), JniHandleOwnership.TransferLocalRef)
		{
		}

		static char[] ValidateData (char[] data)
		{
			if (data == null)
				throw new ArgumentNullException ("data");
			return data;
		}

		static int GetLength (char[] data)
		{
			if (data == null)
				throw new ArgumentNullException ("data");
			return data.Length;
		}

		[Register (".ctor", "([C)V", "")]
		public String (char[] data)
			: base (JNIEnv.NewString (ValidateData (data), GetLength (data)), JniHandleOwnership.TransferLocalRef)
		{
		}

		[Register (".ctor", "([CII)V", "")]
		public String (char[] data, int offset, int charCount)
			: base (JNIEnv.NewString (new string (data, offset, charCount)), JniHandleOwnership.TransferLocalRef)
		{
		}

		[Register (".ctor", "(Ljava/lang/String;)V", "")]
		public String (string toCopy)
			: base (JNIEnv.NewString (toCopy), JniHandleOwnership.TransferLocalRef)
		{
		}

		public override string ToString ()
		{
			unsafe {
				// SAFETY: this String peer supplies the live CharSequence reference during ToString; the JNI result is marshaled synchronously.
				return JNIEnv.GetCharSequence (Handle, JniHandleOwnership.DoNotTransfer)!;
			}
		}
	}
}
