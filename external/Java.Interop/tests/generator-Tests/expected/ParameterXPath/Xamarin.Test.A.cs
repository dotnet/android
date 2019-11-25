using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='A']"
	[global::Android.Runtime.Register ("xamarin/test/A", DoNotGenerateAcw=true)]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T extends java.lang.Object"})]
	public partial class A : global::Java.Lang.Object {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/A", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (A); }
		}

		protected A (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static Delegate cb_setA_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetSetA_Ljava_lang_Object_Handler ()
		{
			if (cb_setA_Ljava_lang_Object_ == null)
				cb_setA_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_SetA_Ljava_lang_Object_);
			return cb_setA_Ljava_lang_Object_;
		}

		static void n_SetA_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_adapter)
		{
			global::Xamarin.Test.A __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.A> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Java.Lang.Object adapter = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_adapter, JniHandleOwnership.DoNotTransfer);
			__this.SetA (adapter);
		}
#pragma warning restore 0169

		static IntPtr id_setA_Ljava_lang_Object_;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='A']/method[@name='setA' and count(parameter)=1 and parameter[1][@type='T']]"
		[Register ("setA", "(Ljava/lang/Object;)V", "GetSetA_Ljava_lang_Object_Handler")]
		public virtual unsafe void SetA (global::Java.Lang.Object adapter)
		{
			if (id_setA_Ljava_lang_Object_ == IntPtr.Zero)
				id_setA_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "setA", "(Ljava/lang/Object;)V");
			IntPtr native_adapter = JNIEnv.ToLocalJniHandle (adapter);
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native_adapter);

				if (((object) this).GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_setA_Ljava_lang_Object_, __args);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "setA", "(Ljava/lang/Object;)V"), __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_adapter);
			}
		}

		static Delegate cb_listTest_Ljava_util_List_;
#pragma warning disable 0169
		static Delegate GetListTest_Ljava_util_List_Handler ()
		{
			if (cb_listTest_Ljava_util_List_ == null)
				cb_listTest_Ljava_util_List_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_ListTest_Ljava_util_List_);
			return cb_listTest_Ljava_util_List_;
		}

		static void n_ListTest_Ljava_util_List_ (IntPtr jnienv, IntPtr native__this, IntPtr native_p0)
		{
			global::Xamarin.Test.A __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.A> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var p0 = global::Android.Runtime.JavaList<global::Java.Lang.Integer>.FromJniHandle (native_p0, JniHandleOwnership.DoNotTransfer);
			__this.ListTest (p0);
		}
#pragma warning restore 0169

		static IntPtr id_listTest_Ljava_util_List_;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='A']/method[@name='listTest' and count(parameter)=1 and parameter[1][@type='java.util.List&lt;java.lang.Integer&gt;']]"
		[Register ("listTest", "(Ljava/util/List;)V", "GetListTest_Ljava_util_List_Handler")]
		public virtual unsafe void ListTest (global::System.Collections.Generic.IList<global::Java.Lang.Integer> p0)
		{
			if (id_listTest_Ljava_util_List_ == IntPtr.Zero)
				id_listTest_Ljava_util_List_ = JNIEnv.GetMethodID (class_ref, "listTest", "(Ljava/util/List;)V");
			IntPtr native_p0 = global::Android.Runtime.JavaList<global::Java.Lang.Integer>.ToLocalJniHandle (p0);
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native_p0);

				if (((object) this).GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_listTest_Ljava_util_List_, __args);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "listTest", "(Ljava/util/List;)V"), __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_p0);
			}
		}

	}
}
