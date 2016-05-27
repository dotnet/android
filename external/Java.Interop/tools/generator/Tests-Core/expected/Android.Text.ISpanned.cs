using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Android.Text {

	// Metadata.xml XPath interface reference: path="/api/package[@name='android.text']/interface[@name='Spanned']"
	[Register ("android/text/Spanned", "", "Android.Text.ISpannedInvoker")]
	public partial interface ISpanned : IJavaObject {

		// Metadata.xml XPath method reference: path="/api/package[@name='android.text']/interface[@name='Spanned']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[return:global::Android.Runtime.GeneratedEnum]
		[Register ("getSpanFlags", "(Ljava/lang/Object;)I", "GetGetSpanFlags_Ljava_lang_Object_Handler:Android.Text.ISpannedInvoker, ")]
		Android.Text.SpanTypes GetSpanFlags (Java.Lang.Object tag);

	}

	[global::Android.Runtime.Register ("android/text/Spanned", DoNotGenerateAcw=true)]
	internal class ISpannedInvoker : global::Java.Lang.Object, ISpanned {

		static IntPtr java_class_ref = JNIEnv.FindClass ("android/text/Spanned");

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (ISpannedInvoker); }
		}

		IntPtr class_ref;

		public static ISpanned GetObject (IntPtr handle, JniHandleOwnership transfer)
		{
			return global::Java.Lang.Object.GetObject<ISpanned> (handle, transfer);
		}

		static IntPtr Validate (IntPtr handle)
		{
			if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
				throw new InvalidCastException (string.Format ("Unable to convert instance of type '{0}' to type '{1}'.",
							JNIEnv.GetClassNameFromInstance (handle), "android.text.Spanned"));
			return handle;
		}

		protected override void Dispose (bool disposing)
		{
			if (this.class_ref != IntPtr.Zero)
				JNIEnv.DeleteGlobalRef (this.class_ref);
			this.class_ref = IntPtr.Zero;
			base.Dispose (disposing);
		}

		public ISpannedInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
		{
			IntPtr local_ref = JNIEnv.GetObjectClass (((global::Java.Lang.Object) this).Handle);
			this.class_ref = JNIEnv.NewGlobalRef (local_ref);
			JNIEnv.DeleteLocalRef (local_ref);
		}

		static Delegate cb_getSpanFlags_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetGetSpanFlags_Ljava_lang_Object_Handler ()
		{
			if (cb_getSpanFlags_Ljava_lang_Object_ == null)
				cb_getSpanFlags_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr, int>) n_GetSpanFlags_Ljava_lang_Object_);
			return cb_getSpanFlags_Ljava_lang_Object_;
		}

		static int n_GetSpanFlags_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_tag)
		{
			Android.Text.ISpanned __this = global::Java.Lang.Object.GetObject<Android.Text.ISpanned> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			Java.Lang.Object tag = global::Java.Lang.Object.GetObject<Java.Lang.Object> (native_tag, JniHandleOwnership.DoNotTransfer);
			int __ret = (int) __this.GetSpanFlags (tag);
			return __ret;
		}
#pragma warning restore 0169

		IntPtr id_getSpanFlags_Ljava_lang_Object_;
		public unsafe Android.Text.SpanTypes GetSpanFlags (Java.Lang.Object tag)
		{
			if (id_getSpanFlags_Ljava_lang_Object_ == IntPtr.Zero)
				id_getSpanFlags_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "getSpanFlags", "(Ljava/lang/Object;)I");
			JValue* __args = stackalloc JValue [1];
			__args [0] = new JValue (tag);
			Android.Text.SpanTypes __ret = (Android.Text.SpanTypes) JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_getSpanFlags_Ljava_lang_Object_, __args);
			return __ret;
		}

	}

}
