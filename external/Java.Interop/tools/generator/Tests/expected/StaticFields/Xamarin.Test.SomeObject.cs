using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']"
	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	public partial class SomeObject : global::Java.Lang.Object {


		static IntPtr Value_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='Value']"
		[Register ("Value")]
		public static int Value {
			get {
				if (Value_jfieldId == IntPtr.Zero)
					Value_jfieldId = JNIEnv.GetStaticFieldID (class_ref, "Value", "I");
				return JNIEnv.GetStaticIntField (class_ref, Value_jfieldId);
			}
		}

		static IntPtr Value2_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='Value2']"
		[Register ("Value2")]
		public static int Value2 {
			get {
				if (Value2_jfieldId == IntPtr.Zero)
					Value2_jfieldId = JNIEnv.GetStaticFieldID (class_ref, "Value2", "I");
				return JNIEnv.GetStaticIntField (class_ref, Value2_jfieldId);
			}
			set {
				if (Value2_jfieldId == IntPtr.Zero)
					Value2_jfieldId = JNIEnv.GetStaticFieldID (class_ref, "Value2", "I");
				try {
					JNIEnv.SetStaticField (class_ref, Value2_jfieldId, value);
				} finally {
				}
			}
		}
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

	}
}
