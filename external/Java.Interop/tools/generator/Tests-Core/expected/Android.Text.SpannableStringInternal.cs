using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Android.Text {

	// Metadata.xml XPath class reference: path="/api/package[@name='android.text']/class[@name='SpannableStringInternal']"
	[global::Android.Runtime.Register ("android/text/SpannableStringInternal", DoNotGenerateAcw=true)]
	public abstract partial class SpannableStringInternal : Java.Lang.Object {

		internal static IntPtr java_class_handle;
		internal static IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("android/text/SpannableStringInternal", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (SpannableStringInternal); }
		}

		protected SpannableStringInternal (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static Delegate cb_getSpanFlags_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetGetSpanFlags_Ljava_lang_Object_Handler ()
		{
			if (cb_getSpanFlags_Ljava_lang_Object_ == null)
				cb_getSpanFlags_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr, int>) n_GetSpanFlags_Ljava_lang_Object_);
			return cb_getSpanFlags_Ljava_lang_Object_;
		}

		static int n_GetSpanFlags_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_p0)
		{
			Android.Text.SpannableStringInternal __this = global::Java.Lang.Object.GetObject<Android.Text.SpannableStringInternal> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			Java.Lang.Object p0 = global::Java.Lang.Object.GetObject<Java.Lang.Object> (native_p0, JniHandleOwnership.DoNotTransfer);
			int __ret = (int) __this.GetSpanFlags (p0);
			return __ret;
		}
#pragma warning restore 0169

		static IntPtr id_getSpanFlags_Ljava_lang_Object_;
		// Metadata.xml XPath method reference: path="/api/package[@name='android.text']/class[@name='SpannableStringInternal']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[return:global::Android.Runtime.GeneratedEnum]
		[Register ("getSpanFlags", "(Ljava/lang/Object;)I", "GetGetSpanFlags_Ljava_lang_Object_Handler")]
		public virtual unsafe Android.Text.SpanTypes GetSpanFlags (Java.Lang.Object p0)
		{
			if (id_getSpanFlags_Ljava_lang_Object_ == IntPtr.Zero)
				id_getSpanFlags_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "getSpanFlags", "(Ljava/lang/Object;)I");
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (p0);

				Android.Text.SpanTypes __ret;
				if (GetType () == ThresholdType)
					__ret = (Android.Text.SpanTypes) JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_getSpanFlags_Ljava_lang_Object_, __args);
				else
					__ret = (Android.Text.SpanTypes) JNIEnv.CallNonvirtualIntMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "getSpanFlags", "(Ljava/lang/Object;)I"), __args);
				return __ret;
			} finally {
			}
		}

	}

	[global::Android.Runtime.Register ("android/text/SpannableStringInternal", DoNotGenerateAcw=true)]
	internal partial class SpannableStringInternalInvoker : SpannableStringInternal {

		public SpannableStringInternalInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		protected override global::System.Type ThresholdType {
			get { return typeof (SpannableStringInternalInvoker); }
		}

	}

}
