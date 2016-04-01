using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']"
	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	public abstract partial class SomeObject : global::Java.Lang.Object {

		internal static IntPtr java_class_handle;
		internal static IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/SomeObject", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (SomeObject); }
		}

		protected SomeObject (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static Delegate cb_getSomeInteger;
#pragma warning disable 0169
		static Delegate GetGetSomeIntegerHandler ()
		{
			if (cb_getSomeInteger == null)
				cb_getSomeInteger = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, int>) n_GetSomeInteger);
			return cb_getSomeInteger;
		}

		static int n_GetSomeInteger (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.SomeInteger;
		}
#pragma warning restore 0169

		static Delegate cb_setSomeInteger_I;
#pragma warning disable 0169
		static Delegate GetSetSomeInteger_IHandler ()
		{
			if (cb_setSomeInteger_I == null)
				cb_setSomeInteger_I = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, int>) n_SetSomeInteger_I);
			return cb_setSomeInteger_I;
		}

		static void n_SetSomeInteger_I (IntPtr jnienv, IntPtr native__this, int newvalue)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.SomeInteger = newvalue;
		}
#pragma warning restore 0169

		public abstract int SomeInteger {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeInteger' and count(parameter)=0]"
			[Register ("getSomeInteger", "()I", "GetGetSomeIntegerHandler")] get;
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeInteger' and count(parameter)=1 and parameter[1][@type='int']]"
			[Register ("setSomeInteger", "(I)V", "GetSetSomeInteger_IHandler")] set;
		}

		static Delegate cb_getSomeObjectProperty;
#pragma warning disable 0169
		static Delegate GetGetSomeObjectPropertyHandler ()
		{
			if (cb_getSomeObjectProperty == null)
				cb_getSomeObjectProperty = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr>) n_GetSomeObjectProperty);
			return cb_getSomeObjectProperty;
		}

		static IntPtr n_GetSomeObjectProperty (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToLocalJniHandle (__this.SomeObjectProperty);
		}
#pragma warning restore 0169

		static Delegate cb_setSomeObjectProperty_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetSetSomeObjectProperty_Ljava_lang_Object_Handler ()
		{
			if (cb_setSomeObjectProperty_Ljava_lang_Object_ == null)
				cb_setSomeObjectProperty_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_SetSomeObjectProperty_Ljava_lang_Object_);
			return cb_setSomeObjectProperty_Ljava_lang_Object_;
		}

		static void n_SetSomeObjectProperty_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_newvalue)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Java.Lang.Object newvalue = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_newvalue, JniHandleOwnership.DoNotTransfer);
			__this.SomeObjectProperty = newvalue;
		}
#pragma warning restore 0169

		public abstract global::Java.Lang.Object SomeObjectProperty {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeObjectProperty' and count(parameter)=0]"
			[Register ("getSomeObjectProperty", "()Ljava/lang/Object;", "GetGetSomeObjectPropertyHandler")] get;
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeObjectProperty' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
			[Register ("setSomeObjectProperty", "(Ljava/lang/Object;)V", "GetSetSomeObjectProperty_Ljava_lang_Object_Handler")] set;
		}

		static Delegate cb_getSomeString;
#pragma warning disable 0169
		static Delegate GetGetSomeStringHandler ()
		{
			if (cb_getSomeString == null)
				cb_getSomeString = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr>) n_GetSomeString);
			return cb_getSomeString;
		}

		static IntPtr n_GetSomeString (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.NewString (__this.SomeString);
		}
#pragma warning restore 0169

		static Delegate cb_setSomeString_Ljava_lang_String_;
#pragma warning disable 0169
		static Delegate GetSetSomeString_Ljava_lang_String_Handler ()
		{
			if (cb_setSomeString_Ljava_lang_String_ == null)
				cb_setSomeString_Ljava_lang_String_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_SetSomeString_Ljava_lang_String_);
			return cb_setSomeString_Ljava_lang_String_;
		}

		static void n_SetSomeString_Ljava_lang_String_ (IntPtr jnienv, IntPtr native__this, IntPtr native_newvalue)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			string newvalue = JNIEnv.GetString (native_newvalue, JniHandleOwnership.DoNotTransfer);
			__this.SomeString = newvalue;
		}
#pragma warning restore 0169

		public abstract string SomeString {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeString' and count(parameter)=0]"
			[Register ("getSomeString", "()Ljava/lang/String;", "GetGetSomeStringHandler")] get;
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeString' and count(parameter)=1 and parameter[1][@type='java.lang.String']]"
			[Register ("setSomeString", "(Ljava/lang/String;)V", "GetSetSomeString_Ljava_lang_String_Handler")] set;
		}

	}

	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	internal partial class SomeObjectInvoker : SomeObject {

		public SomeObjectInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		protected override global::System.Type ThresholdType {
			get { return typeof (SomeObjectInvoker); }
		}

		static IntPtr id_getSomeInteger;
		static IntPtr id_setSomeInteger_I;
		public override unsafe int SomeInteger {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeInteger' and count(parameter)=0]"
			[Register ("getSomeInteger", "()I", "GetGetSomeIntegerHandler")]
			get {
				if (id_getSomeInteger == IntPtr.Zero)
					id_getSomeInteger = JNIEnv.GetMethodID (class_ref, "getSomeInteger", "()I");
				try {
					return JNIEnv.CallIntMethod  (Handle, id_getSomeInteger);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeInteger' and count(parameter)=1 and parameter[1][@type='int']]"
			[Register ("setSomeInteger", "(I)V", "GetSetSomeInteger_IHandler")]
			set {
				if (id_setSomeInteger_I == IntPtr.Zero)
					id_setSomeInteger_I = JNIEnv.GetMethodID (class_ref, "setSomeInteger", "(I)V");
				try {
					JValue* __args = stackalloc JValue [1];
					__args [0] = new JValue (value);
					JNIEnv.CallVoidMethod  (Handle, id_setSomeInteger_I, __args);
				} finally {
				}
			}
		}

		static IntPtr id_getSomeObjectProperty;
		static IntPtr id_setSomeObjectProperty_Ljava_lang_Object_;
		public override unsafe global::Java.Lang.Object SomeObjectProperty {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeObjectProperty' and count(parameter)=0]"
			[Register ("getSomeObjectProperty", "()Ljava/lang/Object;", "GetGetSomeObjectPropertyHandler")]
			get {
				if (id_getSomeObjectProperty == IntPtr.Zero)
					id_getSomeObjectProperty = JNIEnv.GetMethodID (class_ref, "getSomeObjectProperty", "()Ljava/lang/Object;");
				try {
					return global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (JNIEnv.CallObjectMethod  (Handle, id_getSomeObjectProperty), JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeObjectProperty' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
			[Register ("setSomeObjectProperty", "(Ljava/lang/Object;)V", "GetSetSomeObjectProperty_Ljava_lang_Object_Handler")]
			set {
				if (id_setSomeObjectProperty_Ljava_lang_Object_ == IntPtr.Zero)
					id_setSomeObjectProperty_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "setSomeObjectProperty", "(Ljava/lang/Object;)V");
				try {
					JValue* __args = stackalloc JValue [1];
					__args [0] = new JValue (value);
					JNIEnv.CallVoidMethod  (Handle, id_setSomeObjectProperty_Ljava_lang_Object_, __args);
				} finally {
				}
			}
		}

		static IntPtr id_getSomeString;
		static IntPtr id_setSomeString_Ljava_lang_String_;
		public override unsafe string SomeString {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeString' and count(parameter)=0]"
			[Register ("getSomeString", "()Ljava/lang/String;", "GetGetSomeStringHandler")]
			get {
				if (id_getSomeString == IntPtr.Zero)
					id_getSomeString = JNIEnv.GetMethodID (class_ref, "getSomeString", "()Ljava/lang/String;");
				try {
					return JNIEnv.GetString (JNIEnv.CallObjectMethod  (Handle, id_getSomeString), JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeString' and count(parameter)=1 and parameter[1][@type='java.lang.String']]"
			[Register ("setSomeString", "(Ljava/lang/String;)V", "GetSetSomeString_Ljava_lang_String_Handler")]
			set {
				if (id_setSomeString_Ljava_lang_String_ == IntPtr.Zero)
					id_setSomeString_Ljava_lang_String_ = JNIEnv.GetMethodID (class_ref, "setSomeString", "(Ljava/lang/String;)V");
				IntPtr native_value = JNIEnv.NewString (value);
				try {
					JValue* __args = stackalloc JValue [1];
					__args [0] = new JValue (native_value);
					JNIEnv.CallVoidMethod  (Handle, id_setSomeString_Ljava_lang_String_, __args);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

	}

}
