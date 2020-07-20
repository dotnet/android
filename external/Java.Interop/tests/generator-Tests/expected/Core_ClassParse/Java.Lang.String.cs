using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='String']"
	[global::Android.Runtime.Register ("java/lang/String", DoNotGenerateAcw=true)]
	public partial class String : global::Java.Lang.Object {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("java/lang/String", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (String); }
		}

		protected String (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

	}
}
