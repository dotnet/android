using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test.Invalidnames {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test.invalidnames']/class[@name='in']"
	[global::Android.Runtime.Register ("xamarin/test/invalidnames/in", DoNotGenerateAcw=true)]
	public partial class In : global::Java.Lang.Object {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/invalidnames/in", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (In); }
		}

		protected In (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

	}
}
