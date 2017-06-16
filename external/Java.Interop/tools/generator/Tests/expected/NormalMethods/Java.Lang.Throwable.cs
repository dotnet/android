using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='Throwable']"
	[global::Android.Runtime.Register ("java/lang/Throwable", DoNotGenerateAcw=true)]
	public partial class Throwable  {

		internal static IntPtr java_class_handle;
		internal static IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("java/lang/Throwable", ref java_class_handle);
			}
		}

	}
}
