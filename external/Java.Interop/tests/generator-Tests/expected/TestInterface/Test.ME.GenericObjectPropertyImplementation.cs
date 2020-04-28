using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Test.ME {

	// Metadata.xml XPath class reference: path="/api/package[@name='test.me']/class[@name='GenericObjectPropertyImplementation']"
	[global::Android.Runtime.Register ("test/me/GenericObjectPropertyImplementation", DoNotGenerateAcw=true)]
	public partial class GenericObjectPropertyImplementation : global::Java.Lang.Object, global::Test.ME.IGenericPropertyInterface {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("test/me/GenericObjectPropertyImplementation", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (GenericObjectPropertyImplementation); }
		}

		protected GenericObjectPropertyImplementation (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static IntPtr id_ctor;
		// Metadata.xml XPath constructor reference: path="/api/package[@name='test.me']/class[@name='GenericObjectPropertyImplementation']/constructor[@name='GenericObjectPropertyImplementation' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe GenericObjectPropertyImplementation ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				if (((object) this).GetType () != typeof (GenericObjectPropertyImplementation)) {
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

		static Delegate cb_getObject;
#pragma warning disable 0169
		static Delegate GetGetObjectHandler ()
		{
			if (cb_getObject == null)
				cb_getObject = JNINativeWrapper.CreateDelegate ((_JniMarshal_PP_L) n_GetObject);
			return cb_getObject;
		}

		static IntPtr n_GetObject (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Test.ME.GenericObjectPropertyImplementation> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToLocalJniHandle (__this.Object);
		}
#pragma warning restore 0169

		static Delegate cb_setObject_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetSetObject_Ljava_lang_Object_Handler ()
		{
			if (cb_setObject_Ljava_lang_Object_ == null)
				cb_setObject_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPL_V) n_SetObject_Ljava_lang_Object_);
			return cb_setObject_Ljava_lang_Object_;
		}

		static void n_SetObject_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Test.ME.GenericObjectPropertyImplementation> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var value = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_value, JniHandleOwnership.DoNotTransfer);
			__this.Object = value;
		}
#pragma warning restore 0169

		static IntPtr id_getObject;
		static IntPtr id_setObject_Ljava_lang_Object_;
		public virtual unsafe global::Java.Lang.Object Object {
			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='GenericObjectPropertyImplementation']/method[@name='getObject' and count(parameter)=0]"
			[Register ("getObject", "()Ljava/lang/Object;", "GetGetObjectHandler")]
			get {
				if (id_getObject == IntPtr.Zero)
					id_getObject = JNIEnv.GetMethodID (class_ref, "getObject", "()Ljava/lang/Object;");
				try {

					if (((object) this).GetType () == ThresholdType)
						return global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (JNIEnv.CallObjectMethod (((global::Java.Lang.Object) this).Handle, id_getObject), JniHandleOwnership.TransferLocalRef);
					else
						return global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (JNIEnv.CallNonvirtualObjectMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "getObject", "()Ljava/lang/Object;")), JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='GenericObjectPropertyImplementation']/method[@name='setObject' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
			[Register ("setObject", "(Ljava/lang/Object;)V", "GetSetObject_Ljava_lang_Object_Handler")]
			set {
				if (id_setObject_Ljava_lang_Object_ == IntPtr.Zero)
					id_setObject_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "setObject", "(Ljava/lang/Object;)V");
				try {
					JValue* __args = stackalloc JValue [1];
					__args [0] = new JValue (value);

					if (((object) this).GetType () == ThresholdType)
						JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_setObject_Ljava_lang_Object_, __args);
					else
						JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "setObject", "(Ljava/lang/Object;)V"), __args);
				} finally {
				}
			}
		}

	}
}
