using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']"
	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	public partial class SomeObject : global::Java.Lang.Object {

		internal static IntPtr java_class_handle;
		internal static IntPtr class_ref {
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

		static Delegate cb_getSomeObjectProperty;
#pragma warning disable 0169
		static Delegate GetGetSomeObjectPropertyHandler ()
		{
			if (cb_getSomeObjectProperty == null)
				cb_getSomeObjectProperty = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, int>) n_GetSomeObjectProperty);
			return cb_getSomeObjectProperty;
		}

		static int n_GetSomeObjectProperty (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return (int) __this.SomeObjectProperty;
		}
#pragma warning restore 0169

		static Delegate cb_setSomeObjectProperty_I;
#pragma warning disable 0169
		static Delegate GetSetSomeObjectProperty_IHandler ()
		{
			if (cb_setSomeObjectProperty_I == null)
				cb_setSomeObjectProperty_I = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, int>) n_SetSomeObjectProperty_I);
			return cb_setSomeObjectProperty_I;
		}

		static void n_SetSomeObjectProperty_I (IntPtr jnienv, IntPtr native__this, int native_newvalue)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Xamarin.Test.SomeValues newvalue = (global::Xamarin.Test.SomeValues) native_newvalue;
			__this.SomeObjectProperty = newvalue;
		}
#pragma warning restore 0169

		static IntPtr id_getSomeObjectProperty;
		static IntPtr id_setSomeObjectProperty_I;
		public virtual unsafe global::Xamarin.Test.SomeValues SomeObjectProperty {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeObjectProperty' and count(parameter)=0]"
			[Register ("getSomeObjectProperty", "()I", "GetGetSomeObjectPropertyHandler")]
			get {
				if (id_getSomeObjectProperty == IntPtr.Zero)
					id_getSomeObjectProperty = JNIEnv.GetMethodID (class_ref, "getSomeObjectProperty", "()I");
				try {

					if (((object) this).GetType () == ThresholdType)
						return (global::Xamarin.Test.SomeValues) JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_getSomeObjectProperty);
					else
						return (global::Xamarin.Test.SomeValues) JNIEnv.CallNonvirtualIntMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "getSomeObjectProperty", "()I"));
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeObjectProperty' and count(parameter)=1 and parameter[1][@type='int']]"
			[Register ("setSomeObjectProperty", "(I)V", "GetSetSomeObjectProperty_IHandler")]
			set {
				if (id_setSomeObjectProperty_I == IntPtr.Zero)
					id_setSomeObjectProperty_I = JNIEnv.GetMethodID (class_ref, "setSomeObjectProperty", "(I)V");
				try {
					JValue* __args = stackalloc JValue [1];
					__args [0] = new JValue ((int) value);

					if (((object) this).GetType () == ThresholdType)
						JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_setSomeObjectProperty_I, __args);
					else
						JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "setSomeObjectProperty", "(I)V"), __args);
				} finally {
				}
			}
		}

	}
}
