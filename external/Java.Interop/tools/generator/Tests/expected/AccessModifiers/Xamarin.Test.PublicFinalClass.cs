using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='PublicFinalClass']"
	[global::Android.Runtime.Register ("xamarin/test/PublicFinalClass", DoNotGenerateAcw=true)]
	public sealed partial class PublicFinalClass : global::Xamarin.Test.BasePublicClass {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/PublicFinalClass", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (PublicFinalClass); }
		}

		internal PublicFinalClass (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static IntPtr id_publicMethod;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='PublicFinalClass']/method[@name='publicMethod' and count(parameter)=0]"
		[Register ("publicMethod", "()V", "")]
		public unsafe void PublicMethod ()
		{
			if (id_publicMethod == IntPtr.Zero)
				id_publicMethod = JNIEnv.GetMethodID (class_ref, "publicMethod", "()V");
			try {
				JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_publicMethod);
			} finally {
			}
		}

		static IntPtr id_packageMethodB;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='PackageClassB']/method[@name='packageMethodB' and count(parameter)=0]"
		[Register ("packageMethodB", "()V", "")]
		public unsafe void PackageMethodB ()
		{
			if (id_packageMethodB == IntPtr.Zero)
				id_packageMethodB = JNIEnv.GetMethodID (class_ref, "packageMethodB", "()V");
			try {
				JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_packageMethodB);
			} finally {
			}
		}

		static IntPtr id_packageMethodA;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='PackageClassA']/method[@name='packageMethodA' and count(parameter)=0]"
		[Register ("packageMethodA", "()V", "")]
		public unsafe void PackageMethodA ()
		{
			if (id_packageMethodA == IntPtr.Zero)
				id_packageMethodA = JNIEnv.GetMethodID (class_ref, "packageMethodA", "()V");
			try {
				JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_packageMethodA);
			} finally {
			}
		}

	}
}
