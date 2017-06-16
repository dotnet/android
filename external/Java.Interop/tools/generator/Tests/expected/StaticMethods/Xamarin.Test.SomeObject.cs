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

		static IntPtr id_methodAsInt;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='methodAsInt' and count(parameter)=0]"
		[Register ("methodAsInt", "()I", "")]
		public static unsafe int MethodAsInt ()
		{
			if (id_methodAsInt == IntPtr.Zero)
				id_methodAsInt = JNIEnv.GetStaticMethodID (class_ref, "methodAsInt", "()I");
			try {
				return JNIEnv.CallStaticIntMethod  (class_ref, id_methodAsInt);
			} finally {
			}
		}

		static IntPtr id_methodAsString;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='methodAsString' and count(parameter)=0]"
		[Register ("methodAsString", "()Ljava/lang/String;", "")]
		public static unsafe string MethodAsString ()
		{
			if (id_methodAsString == IntPtr.Zero)
				id_methodAsString = JNIEnv.GetStaticMethodID (class_ref, "methodAsString", "()Ljava/lang/String;");
			try {
				return JNIEnv.GetString (JNIEnv.CallStaticObjectMethod  (class_ref, id_methodAsString), JniHandleOwnership.TransferLocalRef);
			} finally {
			}
		}

		static IntPtr id_Obsoletemethod;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='Obsoletemethod' and count(parameter)=0]"
		[Obsolete (@"Deprecated please use methodAsString")]
		[Register ("Obsoletemethod", "()Ljava/lang/String;", "")]
		public static unsafe string Obsoletemethod ()
		{
			if (id_Obsoletemethod == IntPtr.Zero)
				id_Obsoletemethod = JNIEnv.GetStaticMethodID (class_ref, "Obsoletemethod", "()Ljava/lang/String;");
			try {
				return JNIEnv.GetString (JNIEnv.CallStaticObjectMethod  (class_ref, id_Obsoletemethod), JniHandleOwnership.TransferLocalRef);
			} finally {
			}
		}

	}
}
