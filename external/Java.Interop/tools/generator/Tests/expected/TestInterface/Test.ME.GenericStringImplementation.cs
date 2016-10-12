using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Test.ME {

	// Metadata.xml XPath class reference: path="/api/package[@name='test.me']/class[@name='GenericStringImplementation']"
	[global::Android.Runtime.Register ("test/me/GenericStringImplementation", DoNotGenerateAcw=true)]
	public partial class GenericStringImplementation : global::Java.Lang.Object, global::Test.ME.IGenericInterface {

		internal static IntPtr java_class_handle;
		internal static IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("test/me/GenericStringImplementation", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (GenericStringImplementation); }
		}

		protected GenericStringImplementation (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static IntPtr id_ctor;
		// Metadata.xml XPath constructor reference: path="/api/package[@name='test.me']/class[@name='GenericStringImplementation']/constructor[@name='GenericStringImplementation' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe GenericStringImplementation ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				if (((object) this).GetType () != typeof (GenericStringImplementation)) {
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

		static Delegate cb_SetObject_arrayLjava_lang_String_;
#pragma warning disable 0169
		static Delegate GetSetObject_arrayLjava_lang_String_Handler ()
		{
			if (cb_SetObject_arrayLjava_lang_String_ == null)
				cb_SetObject_arrayLjava_lang_String_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_SetObject_arrayLjava_lang_String_);
			return cb_SetObject_arrayLjava_lang_String_;
		}

		static void n_SetObject_arrayLjava_lang_String_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			global::Test.ME.GenericStringImplementation __this = global::Java.Lang.Object.GetObject<global::Test.ME.GenericStringImplementation> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			string[] value = (string[]) JNIEnv.GetArray (native_value, JniHandleOwnership.DoNotTransfer, typeof (string));
			__this.SetObject (value);
			if (value != null)
				JNIEnv.CopyArray (value, native_value);
		}
#pragma warning restore 0169

		static IntPtr id_SetObject_arrayLjava_lang_String_;
		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='GenericStringImplementation']/method[@name='SetObject' and count(parameter)=1 and parameter[1][@type='java.lang.String[]']]"
		[Register ("SetObject", "([Ljava/lang/String;)V", "GetSetObject_arrayLjava_lang_String_Handler")]
		public virtual unsafe void SetObject (string[] value)
		{
			if (id_SetObject_arrayLjava_lang_String_ == IntPtr.Zero)
				id_SetObject_arrayLjava_lang_String_ = JNIEnv.GetMethodID (class_ref, "SetObject", "([Ljava/lang/String;)V");
			IntPtr native_value = JNIEnv.NewArray (value);
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native_value);

				if (((object) this).GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_SetObject_arrayLjava_lang_String_, __args);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "SetObject", "([Ljava/lang/String;)V"), __args);
			} finally {
				if (value != null) {
					JNIEnv.CopyArray (native_value, value);
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		// This method is explicitly implemented as a member of an instantiated Test.ME.IGenericInterface
		void global::Test.ME.IGenericInterface.SetObject (global::Java.Lang.Object value)
		{
			SetObject (value.ToArray<string> ());
		}

	}
}
