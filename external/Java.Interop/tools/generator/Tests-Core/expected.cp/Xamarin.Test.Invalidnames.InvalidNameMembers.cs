using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test.Invalidnames {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test.invalidnames']/class[@name='InvalidNameMembers']"
	[global::Android.Runtime.Register ("xamarin/test/invalidnames/InvalidNameMembers", DoNotGenerateAcw=true)]
	public partial class InvalidNameMembers : Java.Lang.Object {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/invalidnames/InvalidNameMembers", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (InvalidNameMembers); }
		}

		protected InvalidNameMembers (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

	}
}
