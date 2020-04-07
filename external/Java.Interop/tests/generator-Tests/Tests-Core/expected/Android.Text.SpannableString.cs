using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Android.Text {

	// Metadata.xml XPath class reference: path="/api/package[@name='android.text']/class[@name='SpannableString']"
	[global::Android.Runtime.Register ("android/text/SpannableString", DoNotGenerateAcw=true)]
	public partial class SpannableString : Android.Text.SpannableStringInternal, Android.Text.ISpannable {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("android/text/SpannableString", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (SpannableString); }
		}

		protected SpannableString (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static IntPtr id_ctor_Ljava_lang_CharSequence_;
		// Metadata.xml XPath constructor reference: path="/api/package[@name='android.text']/class[@name='SpannableString']/constructor[@name='SpannableString' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		[Register (".ctor", "(Ljava/lang/CharSequence;)V", "")]
		public unsafe SpannableString (Java.Lang.ICharSequence source)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			IntPtr native_source = CharSequence.ToLocalJniHandle (source);
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native_source);
				if (((object) this).GetType () != typeof (SpannableString)) {
					SetHandle (
							global::Android.Runtime.JNIEnv.StartCreateInstance (((object) this).GetType (), "(Ljava/lang/CharSequence;)V", __args),
							JniHandleOwnership.TransferLocalRef);
					global::Android.Runtime.JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, "(Ljava/lang/CharSequence;)V", __args);
					return;
				}

				if (id_ctor_Ljava_lang_CharSequence_ == IntPtr.Zero)
					id_ctor_Ljava_lang_CharSequence_ = JNIEnv.GetMethodID (class_ref, "<init>", "(Ljava/lang/CharSequence;)V");
				SetHandle (
						global::Android.Runtime.JNIEnv.StartCreateInstance (class_ref, id_ctor_Ljava_lang_CharSequence_, __args),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, class_ref, id_ctor_Ljava_lang_CharSequence_, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_source);
			}
		}

		[Register (".ctor", "(Ljava/lang/CharSequence;)V", "")]
		public unsafe SpannableString (string source)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			IntPtr native_source = CharSequence.ToLocalJniHandle (source);
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native_source);
				if (((object) this).GetType () != typeof (SpannableString)) {
					SetHandle (
							global::Android.Runtime.JNIEnv.StartCreateInstance (((object) this).GetType (), "(Ljava/lang/CharSequence;)V", __args),
							JniHandleOwnership.TransferLocalRef);
					global::Android.Runtime.JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, "(Ljava/lang/CharSequence;)V", __args);
					return;
				}

				if (id_ctor_Ljava_lang_CharSequence_ == IntPtr.Zero)
					id_ctor_Ljava_lang_CharSequence_ = JNIEnv.GetMethodID (class_ref, "<init>", "(Ljava/lang/CharSequence;)V");
				SetHandle (
						global::Android.Runtime.JNIEnv.StartCreateInstance (class_ref, id_ctor_Ljava_lang_CharSequence_, __args),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, class_ref, id_ctor_Ljava_lang_CharSequence_, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_source);
			}
		}

		static Delegate cb_getSpanFlags_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetGetSpanFlags_Ljava_lang_Object_Handler ()
		{
			if (cb_getSpanFlags_Ljava_lang_Object_ == null)
				cb_getSpanFlags_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr, int>) n_GetSpanFlags_Ljava_lang_Object_);
			return cb_getSpanFlags_Ljava_lang_Object_;
		}

		static int n_GetSpanFlags_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_what)
		{
			var __this = global::Java.Lang.Object.GetObject<Android.Text.SpannableString> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var what = global::Java.Lang.Object.GetObject<Java.Lang.Object> (native_what, JniHandleOwnership.DoNotTransfer);
			int __ret = (int) __this.GetSpanFlags (what);
			return __ret;
		}
#pragma warning restore 0169

		static IntPtr id_getSpanFlags_Ljava_lang_Object_;
		// Metadata.xml XPath method reference: path="/api/package[@name='android.text']/class[@name='SpannableString']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[Register ("getSpanFlags", "(Ljava/lang/Object;)I", "GetGetSpanFlags_Ljava_lang_Object_Handler")]
		public override unsafe Android.Text.SpanTypes GetSpanFlags (Java.Lang.Object what)
		{
			if (id_getSpanFlags_Ljava_lang_Object_ == IntPtr.Zero)
				id_getSpanFlags_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "getSpanFlags", "(Ljava/lang/Object;)I");
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (what);

				Android.Text.SpanTypes __ret;
				if (((object) this).GetType () == ThresholdType)
					__ret = (Android.Text.SpanTypes) JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_getSpanFlags_Ljava_lang_Object_, __args);
				else
					__ret = (Android.Text.SpanTypes) JNIEnv.CallNonvirtualIntMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "getSpanFlags", "(Ljava/lang/Object;)I"), __args);
				return __ret;
			} finally {
			}
		}

	}
}
