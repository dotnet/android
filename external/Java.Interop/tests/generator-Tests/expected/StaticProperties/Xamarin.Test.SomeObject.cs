using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']"
	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	public partial class SomeObject : global::Java.Lang.Object {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
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

		static IntPtr id_getSomeInteger;
		static IntPtr id_setSomeInteger_I;
		public static unsafe int SomeInteger {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeInteger' and count(parameter)=0]"
			[Register ("getSomeInteger", "()I", "")]
			get {
				if (id_getSomeInteger == IntPtr.Zero)
					id_getSomeInteger = JNIEnv.GetStaticMethodID (class_ref, "getSomeInteger", "()I");
				try {
					return JNIEnv.CallStaticIntMethod  (class_ref, id_getSomeInteger);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeInteger' and count(parameter)=1 and parameter[1][@type='int']]"
			[Register ("setSomeInteger", "(I)V", "")]
			set {
				if (id_setSomeInteger_I == IntPtr.Zero)
					id_setSomeInteger_I = JNIEnv.GetStaticMethodID (class_ref, "setSomeInteger", "(I)V");
				try {
					JValue* __args = stackalloc JValue [1];
					__args [0] = new JValue (value);
					JNIEnv.CallStaticVoidMethod  (class_ref, id_setSomeInteger_I, __args);
				} finally {
				}
			}
		}

		static IntPtr id_getSomeString;
		static IntPtr id_setSomeString_Ljava_lang_String_;
		public static unsafe string SomeString {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeString' and count(parameter)=0]"
			[Register ("getSomeString", "()Ljava/lang/String;", "")]
			get {
				if (id_getSomeString == IntPtr.Zero)
					id_getSomeString = JNIEnv.GetStaticMethodID (class_ref, "getSomeString", "()Ljava/lang/String;");
				try {
					return JNIEnv.GetString (JNIEnv.CallStaticObjectMethod  (class_ref, id_getSomeString), JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeString' and count(parameter)=1 and parameter[1][@type='java.lang.String']]"
			[Register ("setSomeString", "(Ljava/lang/String;)V", "")]
			set {
				if (id_setSomeString_Ljava_lang_String_ == IntPtr.Zero)
					id_setSomeString_Ljava_lang_String_ = JNIEnv.GetStaticMethodID (class_ref, "setSomeString", "(Ljava/lang/String;)V");
				IntPtr native_value = JNIEnv.NewString (value);
				try {
					JValue* __args = stackalloc JValue [1];
					__args [0] = new JValue (native_value);
					JNIEnv.CallStaticVoidMethod  (class_ref, id_setSomeString_Ljava_lang_String_, __args);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		static IntPtr id_getSomeObject;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeObject' and count(parameter)=0]"
		[Register ("getSomeObject", "()Ljava/lang/Object;", "")]
		public static unsafe global::Java.Lang.Object GetSomeObject ()
		{
			if (id_getSomeObject == IntPtr.Zero)
				id_getSomeObject = JNIEnv.GetStaticMethodID (class_ref, "getSomeObject", "()Ljava/lang/Object;");
			try {
				return global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (JNIEnv.CallStaticObjectMethod  (class_ref, id_getSomeObject), JniHandleOwnership.TransferLocalRef);
			} finally {
			}
		}

		static IntPtr id_setSomeObject_Ljava_lang_Object_;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeObject' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[Register ("setSomeObject", "(Ljava/lang/Object;)V", "")]
		public static unsafe void SetSomeObject (global::Java.Lang.Object newvalue)
		{
			if (id_setSomeObject_Ljava_lang_Object_ == IntPtr.Zero)
				id_setSomeObject_Ljava_lang_Object_ = JNIEnv.GetStaticMethodID (class_ref, "setSomeObject", "(Ljava/lang/Object;)V");
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (newvalue);
				JNIEnv.CallStaticVoidMethod  (class_ref, id_setSomeObject_Ljava_lang_Object_, __args);
			} finally {
			}
		}

	}
}
