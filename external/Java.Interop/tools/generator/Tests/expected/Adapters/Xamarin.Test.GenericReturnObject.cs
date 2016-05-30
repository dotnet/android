using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='GenericReturnObject']"
	[global::Android.Runtime.Register ("xamarin/test/GenericReturnObject", DoNotGenerateAcw=true)]
	public partial class GenericReturnObject : global::Java.Lang.Object {

		internal static IntPtr java_class_handle;
		internal static IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/GenericReturnObject", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (GenericReturnObject); }
		}

		protected GenericReturnObject (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static Delegate cb_GenericReturn;
#pragma warning disable 0169
		static Delegate GetGenericReturnHandler ()
		{
			if (cb_GenericReturn == null)
				cb_GenericReturn = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr>) n_GenericReturn);
			return cb_GenericReturn;
		}

		static IntPtr n_GenericReturn (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.GenericReturnObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.GenericReturnObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToLocalJniHandle (__this.GenericReturn ());
		}
#pragma warning restore 0169

		static IntPtr id_GenericReturn;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='GenericReturnObject']/method[@name='GenericReturn' and count(parameter)=0]"
		[Register ("GenericReturn", "()Lxamarin/test/AdapterView;", "GetGenericReturnHandler")]
		public virtual unsafe global::Xamarin.Test.AdapterView GenericReturn ()
		{
			if (id_GenericReturn == IntPtr.Zero)
				id_GenericReturn = JNIEnv.GetMethodID (class_ref, "GenericReturn", "()Lxamarin/test/AdapterView;");
			try {

				if (GetType () == ThresholdType)
					return global::Java.Lang.Object.GetObject<global::Xamarin.Test.AdapterView> (JNIEnv.CallObjectMethod (((global::Java.Lang.Object) this).Handle, id_GenericReturn), JniHandleOwnership.TransferLocalRef);
				else
					return global::Java.Lang.Object.GetObject<global::Xamarin.Test.AdapterView> (JNIEnv.CallNonvirtualObjectMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "GenericReturn", "()Lxamarin/test/AdapterView;")), JniHandleOwnership.TransferLocalRef);
			} finally {
			}
		}

	}
}
