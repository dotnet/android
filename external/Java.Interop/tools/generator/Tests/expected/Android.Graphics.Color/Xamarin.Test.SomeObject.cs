using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']"
	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	public abstract partial class SomeObject : global::Java.Lang.Object {


		static IntPtr backColor_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/field[@name='backColor']"
		[Register ("backColor")]
		public global::Android.Graphics.Color BackColor {
			get {
				if (backColor_jfieldId == IntPtr.Zero)
					backColor_jfieldId = JNIEnv.GetFieldID (class_ref, "backColor", "I");
				int __ret = JNIEnv.GetIntField (Handle, backColor_jfieldId);
				return new global::Android.Graphics.Color (__ret);
			}
			set {
				if (backColor_jfieldId == IntPtr.Zero)
					backColor_jfieldId = JNIEnv.GetFieldID (class_ref, "backColor", "I");
				try {
					JNIEnv.SetField (Handle, backColor_jfieldId, value.ToArgb ());
				} finally {
				}
			}
		}
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

		static Delegate cb_getSomeColor;
#pragma warning disable 0169
		static Delegate GetGetSomeColorHandler ()
		{
			if (cb_getSomeColor == null)
				cb_getSomeColor = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, int>) n_GetSomeColor);
			return cb_getSomeColor;
		}

		static int n_GetSomeColor (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.SomeColor.ToArgb ();
		}
#pragma warning restore 0169

		static Delegate cb_setSomeColor_I;
#pragma warning disable 0169
		static Delegate GetSetSomeColor_IHandler ()
		{
			if (cb_setSomeColor_I == null)
				cb_setSomeColor_I = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, int>) n_SetSomeColor_I);
			return cb_setSomeColor_I;
		}

		static void n_SetSomeColor_I (IntPtr jnienv, IntPtr native__this, int native_newvalue)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Android.Graphics.Color newvalue = new global::Android.Graphics.Color (native_newvalue);
			__this.SomeColor = newvalue;
		}
#pragma warning restore 0169

		public abstract global::Android.Graphics.Color SomeColor {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeColor' and count(parameter)=0]"
			[Register ("getSomeColor", "()I", "GetGetSomeColorHandler")] get;
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeColor' and count(parameter)=1 and parameter[1][@type='Android.Graphics.Color']]"
			[Register ("setSomeColor", "(I)V", "GetSetSomeColor_IHandler")] set;
		}

	}

	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	internal partial class SomeObjectInvoker : SomeObject {

		public SomeObjectInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		protected override global::System.Type ThresholdType {
			get { return typeof (SomeObjectInvoker); }
		}

		static IntPtr id_getSomeColor;
		static IntPtr id_setSomeColor_I;
		public override unsafe global::Android.Graphics.Color SomeColor {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeColor' and count(parameter)=0]"
			[Register ("getSomeColor", "()I", "GetGetSomeColorHandler")]
			get {
				if (id_getSomeColor == IntPtr.Zero)
					id_getSomeColor = JNIEnv.GetMethodID (class_ref, "getSomeColor", "()I");
				try {
					return new global::Android.Graphics.Color (JNIEnv.CallIntMethod  (Handle, id_getSomeColor));
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeColor' and count(parameter)=1 and parameter[1][@type='Android.Graphics.Color']]"
			[Register ("setSomeColor", "(I)V", "GetSetSomeColor_IHandler")]
			set {
				if (id_setSomeColor_I == IntPtr.Zero)
					id_setSomeColor_I = JNIEnv.GetMethodID (class_ref, "setSomeColor", "(I)V");
				try {
					JValue* __args = stackalloc JValue [1];
					__args [0] = new JValue (value.ToArgb ());
					JNIEnv.CallVoidMethod  (Handle, id_setSomeColor_I, __args);
				} finally {
				}
			}
		}

	}

}
