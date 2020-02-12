using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='DeprecatedItems']"
	[global::Android.Runtime.Register ("xamarin/test/DeprecatedItems", DoNotGenerateAcw=true)]
	public partial class DeprecatedItems : global::Java.Lang.Object {


		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='DeprecatedItems']/field[@name='My_Test_Field']"
		[Register ("My_Test_Field")]
		[Obsolete ("Field is deprecated", error: true)]
		public const int MyTestField = (int) 1;
		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/DeprecatedItems", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (DeprecatedItems); }
		}

		protected DeprecatedItems (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

	}
}
