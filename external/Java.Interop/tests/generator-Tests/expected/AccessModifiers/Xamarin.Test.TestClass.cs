using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='TestClass']"
	[global::Android.Runtime.Register ("xamarin/test/TestClass", DoNotGenerateAcw=true)]
	public partial class TestClass : global::Java.Lang.Object {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/TestClass", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (TestClass); }
		}

		protected TestClass (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static IntPtr id_ctor;
		// Metadata.xml XPath constructor reference: path="/api/package[@name='xamarin.test']/class[@name='TestClass']/constructor[@name='TestClass' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe TestClass ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				if (((object) this).GetType () != typeof (TestClass)) {
					SetHandle (
							global::Android.Runtime.JNIEnv.StartCreateInstance (((object) this).GetType (), "()V"),
							JniHandleOwnership.TransferLocalRef);
					global::Android.Runtime.JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, "()V");
					return;
				}

				if (id_ctor == IntPtr.Zero)
					id_ctor = JNIEnv.GetMethodID (class_ref, "<init>", "()V");
				SetHandle (
						global::Android.Runtime.JNIEnv.StartCreateInstance (class_ref, id_ctor),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, class_ref, id_ctor);
			} finally {
			}
		}

		static Delegate cb_baseMethod;
#pragma warning disable 0169
		static Delegate GetBaseMethodHandler ()
		{
			if (cb_baseMethod == null)
				cb_baseMethod = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr>) n_BaseMethod);
			return cb_baseMethod;
		}

		static void n_BaseMethod (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.TestClass __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.TestClass> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.BaseMethod ();
		}
#pragma warning restore 0169

		static IntPtr id_baseMethod;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='TestClass']/method[@name='baseMethod' and count(parameter)=0]"
		[Register ("baseMethod", "()V", "GetBaseMethodHandler")]
		public virtual unsafe void BaseMethod ()
		{
			if (id_baseMethod == IntPtr.Zero)
				id_baseMethod = JNIEnv.GetMethodID (class_ref, "baseMethod", "()V");
			try {

				if (((object) this).GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_baseMethod);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "baseMethod", "()V"));
			} finally {
			}
		}

	}
}
