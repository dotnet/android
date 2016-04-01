using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='A']"
	[global::Android.Runtime.Register ("xamarin/test/A", DoNotGenerateAcw=true)]
	public partial class A : global::Java.Lang.Object {

		// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='A.B']"
		[global::Android.Runtime.Register ("xamarin/test/A$B", DoNotGenerateAcw=true)]
		[global::Java.Interop.JavaTypeParameters (new string [] {"T extends xamarin.test.A.B"})]
		public partial class B : global::Java.Lang.Object {

			internal static IntPtr java_class_handle;
			internal static IntPtr class_ref {
				get {
					return JNIEnv.FindClass ("xamarin/test/A$B", ref java_class_handle);
				}
			}

			protected override IntPtr ThresholdClass {
				get { return class_ref; }
			}

			protected override global::System.Type ThresholdType {
				get { return typeof (B); }
			}

			protected B (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

			static Delegate cb_setCustomDimension_I;
#pragma warning disable 0169
			static Delegate GetSetCustomDimension_IHandler ()
			{
				if (cb_setCustomDimension_I == null)
					cb_setCustomDimension_I = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, int, IntPtr>) n_SetCustomDimension_I);
				return cb_setCustomDimension_I;
			}

			static IntPtr n_SetCustomDimension_I (IntPtr jnienv, IntPtr native__this, int index)
			{
				global::Xamarin.Test.A.B __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.A.B> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				return JNIEnv.ToLocalJniHandle (__this.SetCustomDimension (index));
			}
#pragma warning restore 0169

			static IntPtr id_setCustomDimension_I;
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='A.B']/method[@name='setCustomDimension' and count(parameter)=1 and parameter[1][@type='int']]"
			[Register ("setCustomDimension", "(I)Lxamarin/test/A$B;", "GetSetCustomDimension_IHandler")]
			public virtual unsafe global::Java.Lang.Object SetCustomDimension (int index)
			{
				if (id_setCustomDimension_I == IntPtr.Zero)
					id_setCustomDimension_I = JNIEnv.GetMethodID (class_ref, "setCustomDimension", "(I)Lxamarin/test/A$B;");
				try {
					JValue* __args = stackalloc JValue [1];
					__args [0] = new JValue (index);

					if (GetType () == ThresholdType)
						return (Java.Lang.Object) global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (JNIEnv.CallObjectMethod  (Handle, id_setCustomDimension_I, __args), JniHandleOwnership.TransferLocalRef);
					else
						return (Java.Lang.Object) global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (JNIEnv.CallNonvirtualObjectMethod  (Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "setCustomDimension", "(I)Lxamarin/test/A$B;"), __args), JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}

		}

		protected A (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

	}
}
