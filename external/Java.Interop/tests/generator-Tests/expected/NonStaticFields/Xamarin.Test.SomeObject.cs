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
		public int Value {
			get {
				if (Value_jfieldId == IntPtr.Zero)
					Value_jfieldId = JNIEnv.GetFieldID (class_ref, "Value", "I");
				return JNIEnv.GetIntField (((global::Java.Lang.Object) this).Handle, Value_jfieldId);
			}
			set {
				if (Value_jfieldId == IntPtr.Zero)
					Value_jfieldId = JNIEnv.GetFieldID (class_ref, "Value", "I");
				try {
					JNIEnv.SetField (((global::Java.Lang.Object) this).Handle, Value_jfieldId, value);
				} finally {
				}
			}
		}
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

	}
}
