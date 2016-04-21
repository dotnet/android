using System;
using Android.Runtime;

namespace Java.Lang {

	public partial class String {

		[Register (".ctor", "()V", "")]
		public unsafe String ()
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
		public unsafe String (char[] data)
			: base (JNIEnv.NewString (ValidateData (data), GetLength (data)), JniHandleOwnership.TransferLocalRef)
		{
		}

		[Register (".ctor", "([CII)V", "")]
		public unsafe String (char[] data, int offset, int charCount)
			: base (JNIEnv.NewString (new string (data, offset, charCount)), JniHandleOwnership.TransferLocalRef)
		{
		}

		[Register (".ctor", "(Ljava/lang/String;)V", "")]
		public unsafe String (string toCopy)
			: base (JNIEnv.NewString (toCopy), JniHandleOwnership.TransferLocalRef)
		{
		}

		public override string ToString ()
		{
			return JNIEnv.GetCharSequence (Handle, JniHandleOwnership.DoNotTransfer);
		}
	}
}
