using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject2']"
	[global::Android.Runtime.Register ("xamarin/test/SomeObject2", DoNotGenerateAcw=true)]
	public partial class SomeObject2 : global::Xamarin.Test.SomeObject {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/SomeObject2", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (SomeObject2); }
		}

		protected SomeObject2 (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

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
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject2> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
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
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject2> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var newvalue = (global::Xamarin.Test.SomeValues) native_newvalue;
			__this.SomeObjectProperty = newvalue;
		}
#pragma warning restore 0169

		static IntPtr id_getSomeObjectProperty;
		static IntPtr id_setSomeObjectProperty_I;
		public override unsafe global::Xamarin.Test.SomeValues SomeObjectProperty {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject2']/method[@name='getSomeObjectProperty' and count(parameter)=0]"
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
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject2']/method[@name='setSomeObjectProperty' and count(parameter)=1 and parameter[1][@type='int']]"
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

		static Delegate cb_getSomeObjectPropertyArray;
#pragma warning disable 0169
		static Delegate GetGetSomeObjectPropertyArrayHandler ()
		{
			if (cb_getSomeObjectPropertyArray == null)
				cb_getSomeObjectPropertyArray = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr>) n_GetSomeObjectPropertyArray);
			return cb_getSomeObjectPropertyArray;
		}

		static IntPtr n_GetSomeObjectPropertyArray (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject2> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.NewArray (__this.GetSomeObjectPropertyArray ());
		}
#pragma warning restore 0169

		static IntPtr id_getSomeObjectPropertyArray;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject2']/method[@name='getSomeObjectPropertyArray' and count(parameter)=0]"
		[return:global::Android.Runtime.GeneratedEnum]
		[Register ("getSomeObjectPropertyArray", "()[I", "GetGetSomeObjectPropertyArrayHandler")]
		public virtual unsafe global::Xamarin.Test.SomeValues[] GetSomeObjectPropertyArray ()
		{
			if (id_getSomeObjectPropertyArray == IntPtr.Zero)
				id_getSomeObjectPropertyArray = JNIEnv.GetMethodID (class_ref, "getSomeObjectPropertyArray", "()[I");
			try {

				if (((object) this).GetType () == ThresholdType)
					return (global::Xamarin.Test.SomeValues[]) JNIEnv.GetArray (JNIEnv.CallObjectMethod (((global::Java.Lang.Object) this).Handle, id_getSomeObjectPropertyArray), JniHandleOwnership.TransferLocalRef, typeof (global::Xamarin.Test.SomeValues));
				else
					return (global::Xamarin.Test.SomeValues[]) JNIEnv.GetArray (JNIEnv.CallNonvirtualObjectMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "getSomeObjectPropertyArray", "()[I")), JniHandleOwnership.TransferLocalRef, typeof (global::Xamarin.Test.SomeValues));
			} finally {
			}
		}

		static Delegate cb_setSomeObjectPropertyArray_arrayI;
#pragma warning disable 0169
		static Delegate GetSetSomeObjectPropertyArray_arrayIHandler ()
		{
			if (cb_setSomeObjectPropertyArray_arrayI == null)
				cb_setSomeObjectPropertyArray_arrayI = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_SetSomeObjectPropertyArray_arrayI);
			return cb_setSomeObjectPropertyArray_arrayI;
		}

		static void n_SetSomeObjectPropertyArray_arrayI (IntPtr jnienv, IntPtr native__this, IntPtr native_newvalue)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject2> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var newvalue = (global::Xamarin.Test.SomeValues[]) JNIEnv.GetArray (native_newvalue, JniHandleOwnership.DoNotTransfer, typeof (global::Xamarin.Test.SomeValues));
			__this.SetSomeObjectPropertyArray (newvalue);
			if (newvalue != null)
				JNIEnv.CopyArray (newvalue, native_newvalue);
		}
#pragma warning restore 0169

		static IntPtr id_setSomeObjectPropertyArray_arrayI;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject2']/method[@name='setSomeObjectPropertyArray' and count(parameter)=1 and parameter[1][@type='int[]']]"
		[Register ("setSomeObjectPropertyArray", "([I)V", "GetSetSomeObjectPropertyArray_arrayIHandler")]
		public virtual unsafe void SetSomeObjectPropertyArray ([global::Android.Runtime.GeneratedEnum] global::Xamarin.Test.SomeValues[] newvalue)
		{
			if (id_setSomeObjectPropertyArray_arrayI == IntPtr.Zero)
				id_setSomeObjectPropertyArray_arrayI = JNIEnv.GetMethodID (class_ref, "setSomeObjectPropertyArray", "([I)V");
			IntPtr native_newvalue = JNIEnv.NewArray (newvalue);
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native_newvalue);

				if (((object) this).GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_setSomeObjectPropertyArray_arrayI, __args);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "setSomeObjectPropertyArray", "([I)V"), __args);
			} finally {
				if (newvalue != null) {
					JNIEnv.CopyArray (native_newvalue, newvalue);
					JNIEnv.DeleteLocalRef (native_newvalue);
				}
			}
		}

	}
}
