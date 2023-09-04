using System;
using Android.Runtime;

namespace Android.Content {

	public sealed partial class ContentValues {

		static IntPtr id_getAsBoolean_Ljava_lang_String_;
		[Register ("getAsBoolean", "(Ljava/lang/String;)Ljava/lang/Boolean;", "")]
		public bool GetAsBoolean (string key)
		{
			if (id_getAsBoolean_Ljava_lang_String_ == IntPtr.Zero)
				id_getAsBoolean_Ljava_lang_String_ = JNIEnv.GetMethodID (class_ref, "getAsBoolean", "(Ljava/lang/String;)Ljava/lang/Boolean;");
			IntPtr jkey = JNIEnv.NewString (key);
			try {
				using (var ret = new Java.Lang.Boolean (JNIEnv.CallObjectMethod (Handle, id_getAsBoolean_Ljava_lang_String_, new JValue (jkey)),
						JniHandleOwnership.TransferLocalRef | JniHandleOwnership.DoNotRegister))
					return (bool) ret;
			} finally {
				JNIEnv.DeleteLocalRef (jkey);
			}
		}

		static IntPtr id_getAsByte_Ljava_lang_String_;
		[Register ("getAsByte", "(Ljava/lang/String;)Ljava/lang/Byte;", "")]
		public sbyte GetAsByte (string key)
		{
			if (id_getAsByte_Ljava_lang_String_ == IntPtr.Zero)
				id_getAsByte_Ljava_lang_String_ = JNIEnv.GetMethodID (class_ref, "getAsByte", "(Ljava/lang/String;)Ljava/lang/Byte;");
			IntPtr jkey = JNIEnv.NewString (key);
			try {
				using (var ret = new Java.Lang.Byte (JNIEnv.CallObjectMethod (Handle, id_getAsByte_Ljava_lang_String_, new JValue (jkey)),
						JniHandleOwnership.TransferLocalRef | JniHandleOwnership.DoNotRegister))
					return (sbyte) ret;
			} finally {
				JNIEnv.DeleteLocalRef (jkey);
			}
		}

		static IntPtr id_getAsDouble_Ljava_lang_String_;
		[Register ("getAsDouble", "(Ljava/lang/String;)Ljava/lang/Double;", "")]
		public double GetAsDouble (string key)
		{
			if (id_getAsDouble_Ljava_lang_String_ == IntPtr.Zero)
				id_getAsDouble_Ljava_lang_String_ = JNIEnv.GetMethodID (class_ref, "getAsDouble", "(Ljava/lang/String;)Ljava/lang/Double;");
			IntPtr jkey = JNIEnv.NewString (key);
			try {
				using (var ret = new Java.Lang.Double (JNIEnv.CallObjectMethod (Handle, id_getAsDouble_Ljava_lang_String_, new JValue (jkey)),
						JniHandleOwnership.TransferLocalRef | JniHandleOwnership.DoNotRegister))
					return (double) ret;
			} finally {
				JNIEnv.DeleteLocalRef (jkey);
			}
		}

		static IntPtr id_getAsFloat_Ljava_lang_String_;
		[Register ("getAsFloat", "(Ljava/lang/String;)Ljava/lang/Float;", "")]
		public float GetAsFloat (string key)
		{
			if (id_getAsFloat_Ljava_lang_String_ == IntPtr.Zero)
				id_getAsFloat_Ljava_lang_String_ = JNIEnv.GetMethodID (class_ref, "getAsFloat", "(Ljava/lang/String;)Ljava/lang/Float;");
			IntPtr jkey = JNIEnv.NewString (key);
			try {
				using (var ret = new Java.Lang.Float (JNIEnv.CallObjectMethod (Handle, id_getAsFloat_Ljava_lang_String_, new JValue (jkey)),
						JniHandleOwnership.TransferLocalRef | JniHandleOwnership.DoNotRegister))
					return  (float) ret;
			} finally {
				JNIEnv.DeleteLocalRef (jkey);
			}
		}

		static IntPtr id_getAsInteger_Ljava_lang_String_;
		[Register ("getAsInteger", "(Ljava/lang/String;)Ljava/lang/Integer;", "")]
		public int GetAsInteger (string key)
		{
			if (id_getAsInteger_Ljava_lang_String_ == IntPtr.Zero)
				id_getAsInteger_Ljava_lang_String_ = JNIEnv.GetMethodID (class_ref, "getAsInteger", "(Ljava/lang/String;)Ljava/lang/Integer;");
			IntPtr jkey = JNIEnv.NewString (key);
			try {
				using (var ret = new Java.Lang.Integer (JNIEnv.CallObjectMethod (Handle, id_getAsInteger_Ljava_lang_String_, new JValue (jkey)),
						JniHandleOwnership.TransferLocalRef | JniHandleOwnership.DoNotRegister))
					return (int) ret;
			} finally {
				JNIEnv.DeleteLocalRef (jkey);
			}
		}

		static IntPtr id_getAsLong_Ljava_lang_String_;
		[Register ("getAsLong", "(Ljava/lang/String;)Ljava/lang/Long;", "")]
		public long GetAsLong (string key)
		{
			if (id_getAsLong_Ljava_lang_String_ == IntPtr.Zero)
				id_getAsLong_Ljava_lang_String_ = JNIEnv.GetMethodID (class_ref, "getAsLong", "(Ljava/lang/String;)Ljava/lang/Long;");
			IntPtr jkey = JNIEnv.NewString (key);
			try {
				using (var ret = new Java.Lang.Long (JNIEnv.CallObjectMethod (Handle, id_getAsLong_Ljava_lang_String_, new JValue (jkey)),
						JniHandleOwnership.TransferLocalRef | JniHandleOwnership.DoNotRegister))
					return (long) ret;
			} finally {
				JNIEnv.DeleteLocalRef (jkey);
			}
		}

		static IntPtr id_getAsShort_Ljava_lang_String_;
		[Register ("getAsShort", "(Ljava/lang/String;)Ljava/lang/Short;", "")]
		public short GetAsShort (string key)
		{
			if (id_getAsShort_Ljava_lang_String_ == IntPtr.Zero)
				id_getAsShort_Ljava_lang_String_ = JNIEnv.GetMethodID (class_ref, "getAsShort", "(Ljava/lang/String;)Ljava/lang/Short;");
			IntPtr jkey = JNIEnv.NewString (key);
			try {
				using (var ret = new Java.Lang.Short (JNIEnv.CallObjectMethod (Handle, id_getAsShort_Ljava_lang_String_, new JValue (jkey)),
						JniHandleOwnership.TransferLocalRef | JniHandleOwnership.DoNotRegister))
					return (short) ret;
			} finally {
				JNIEnv.DeleteLocalRef (jkey);
			}
		}

		static IntPtr id_put_Ljava_lang_String_Ljava_lang_Boolean_;
		[Register ("put", "(Ljava/lang/String;Ljava/lang/Boolean;)V", "")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public void Put (string key, bool value)
		{
			if (id_put_Ljava_lang_String_Ljava_lang_Boolean_ == IntPtr.Zero)
				id_put_Ljava_lang_String_Ljava_lang_Boolean_ = JNIEnv.GetMethodID (class_ref, "put", "(Ljava/lang/String;Ljava/lang/Boolean;)V");
			IntPtr jkey = JNIEnv.NewString (key);
			try {
				using (var val = new Java.Lang.Boolean (value))
					JNIEnv.CallVoidMethod (Handle, id_put_Ljava_lang_String_Ljava_lang_Boolean_, new JValue (jkey), new JValue (val));
			} finally {
				JNIEnv.DeleteLocalRef (jkey);
			}
		}

		static IntPtr id_put_Ljava_lang_String_Ljava_lang_Byte_;
		[Register ("put", "(Ljava/lang/String;Ljava/lang/Byte;)V", "")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public void Put (string key, sbyte value)
		{
			if (id_put_Ljava_lang_String_Ljava_lang_Byte_ == IntPtr.Zero)
				id_put_Ljava_lang_String_Ljava_lang_Byte_ = JNIEnv.GetMethodID (class_ref, "put", "(Ljava/lang/String;Ljava/lang/Byte;)V");
			IntPtr jkey = JNIEnv.NewString (key);
			try {
				using (var val = new Java.Lang.Byte (value))
					JNIEnv.CallVoidMethod (Handle, id_put_Ljava_lang_String_Ljava_lang_Byte_, new JValue (jkey), new JValue (val));
			} finally {
				JNIEnv.DeleteLocalRef (jkey);
			}
		}

		static IntPtr id_put_Ljava_lang_String_Ljava_lang_Short_;
		[Register ("put", "(Ljava/lang/String;Ljava/lang/Short;)V", "")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public void Put (string key, short value)
		{
			if (id_put_Ljava_lang_String_Ljava_lang_Short_ == IntPtr.Zero)
				id_put_Ljava_lang_String_Ljava_lang_Short_ = JNIEnv.GetMethodID (class_ref, "put", "(Ljava/lang/String;Ljava/lang/Short;)V");
			IntPtr jkey = JNIEnv.NewString (key);
			try {
				using (var val = new Java.Lang.Short (value))
					JNIEnv.CallVoidMethod (Handle, id_put_Ljava_lang_String_Ljava_lang_Short_, new JValue (jkey), new JValue (val));
			} finally {
				JNIEnv.DeleteLocalRef (jkey);
			}
		}

		static IntPtr id_put_Ljava_lang_String_Ljava_lang_Integer_;
		[Register ("put", "(Ljava/lang/String;Ljava/lang/Integer;)V", "")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public void Put (string key, int value)
		{
			if (id_put_Ljava_lang_String_Ljava_lang_Integer_ == IntPtr.Zero)
				id_put_Ljava_lang_String_Ljava_lang_Integer_ = JNIEnv.GetMethodID (class_ref, "put", "(Ljava/lang/String;Ljava/lang/Integer;)V");
			IntPtr jkey = JNIEnv.NewString (key);
			try {
				using (var val = new Java.Lang.Integer (value))
					JNIEnv.CallVoidMethod (Handle, id_put_Ljava_lang_String_Ljava_lang_Integer_, new JValue (jkey), new JValue (val));
			} finally {
				JNIEnv.DeleteLocalRef (jkey);
			}
		}

		static IntPtr id_put_Ljava_lang_String_Ljava_lang_Long_;
		[Register ("put", "(Ljava/lang/String;Ljava/lang/Long;)V", "")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public void Put (string key, long value)
		{
			if (id_put_Ljava_lang_String_Ljava_lang_Long_ == IntPtr.Zero)
				id_put_Ljava_lang_String_Ljava_lang_Long_ = JNIEnv.GetMethodID (class_ref, "put", "(Ljava/lang/String;Ljava/lang/Long;)V");
			IntPtr jkey = JNIEnv.NewString (key);
			try {
				using (var val = new Java.Lang.Long (value))
					JNIEnv.CallVoidMethod (Handle, id_put_Ljava_lang_String_Ljava_lang_Long_, new JValue (jkey), new JValue (val));
			} finally {
				JNIEnv.DeleteLocalRef (jkey);
			}
		}

		static IntPtr id_put_Ljava_lang_String_Ljava_lang_Float_;
		[Register ("put", "(Ljava/lang/String;Ljava/lang/Float;)V", "")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public void Put (string key, float value)
		{
			if (id_put_Ljava_lang_String_Ljava_lang_Float_ == IntPtr.Zero)
				id_put_Ljava_lang_String_Ljava_lang_Float_ = JNIEnv.GetMethodID (class_ref, "put", "(Ljava/lang/String;Ljava/lang/Float;)V");
			IntPtr jkey = JNIEnv.NewString (key);
			try {
				using (var val = new Java.Lang.Float (value))
					JNIEnv.CallVoidMethod (Handle, id_put_Ljava_lang_String_Ljava_lang_Float_, new JValue (jkey), new JValue (val));
			} finally {
				JNIEnv.DeleteLocalRef (jkey);
			}
		}

		static IntPtr id_put_Ljava_lang_String_Ljava_lang_Double_;
		[Register ("put", "(Ljava/lang/String;Ljava/lang/Double;)V", "")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Interoperability", "CA1422:Validate platform compatibility", Justification = "Suggested replacement uses instance sharing")]
		public void Put (string key, double value)
		{
			if (id_put_Ljava_lang_String_Ljava_lang_Double_ == IntPtr.Zero)
				id_put_Ljava_lang_String_Ljava_lang_Double_ = JNIEnv.GetMethodID (class_ref, "put", "(Ljava/lang/String;Ljava/lang/Double;)V");
			IntPtr jkey = JNIEnv.NewString (key);
			try {
				using (var val = new Java.Lang.Double (value))
					JNIEnv.CallVoidMethod (Handle, id_put_Ljava_lang_String_Ljava_lang_Double_, new JValue (jkey), new JValue (val));
			} finally {
				JNIEnv.DeleteLocalRef (jkey);
			}
		}
	}
}
