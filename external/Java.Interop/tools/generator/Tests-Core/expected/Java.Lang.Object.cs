using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='Object']"
	[global::Android.Runtime.Register ("java/lang/Object", DoNotGenerateAcw=true)]
	public partial class Object  {

		internal static IntPtr java_class_handle;
		internal static IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("java/lang/Object", ref java_class_handle);
			}
		}

	}
}
