using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Test.ME {

	// Metadata.xml XPath class reference: path="/api/package[@name='test.me']/class[@name='GenericStringPropertyImplementation']"
	[global::Android.Runtime.Register ("test/me/GenericStringPropertyImplementation", DoNotGenerateAcw=true)]
	public partial class GenericStringPropertyImplementation : global::Java.Lang.Object, global::Test.ME.IGenericPropertyInterface {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("test/me/GenericStringPropertyImplementation", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (GenericStringPropertyImplementation); }
		}

		protected GenericStringPropertyImplementation (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static IntPtr id_ctor;
		// Metadata.xml XPath constructor reference: path="/api/package[@name='test.me']/class[@name='GenericStringPropertyImplementation']/constructor[@name='GenericStringPropertyImplementation' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe GenericStringPropertyImplementation ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				if (((object) this).GetType () != typeof (GenericStringPropertyImplementation)) {
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
			var __this = global::Java.Lang.Object.GetObject<global::Test.ME.GenericStringPropertyImplementation> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.NewString (__this.Object);
		}
#pragma warning restore 0169

		static Delegate cb_SetObject_Ljava_lang_String_;
#pragma warning disable 0169
		static Delegate GetSetObject_Ljava_lang_String_Handler ()
		{
			if (cb_SetObject_Ljava_lang_String_ == null)
				cb_SetObject_Ljava_lang_String_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPL_V) n_SetObject_Ljava_lang_String_);
			return cb_SetObject_Ljava_lang_String_;
		}

		static void n_SetObject_Ljava_lang_String_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Test.ME.GenericStringPropertyImplementation> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var value = JNIEnv.GetString (native_value, JniHandleOwnership.DoNotTransfer);
			__this.Object = value;
		}
#pragma warning restore 0169

		static IntPtr id_getObject;
		static IntPtr id_SetObject_Ljava_lang_String_;
		public virtual unsafe string Object {
			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='GenericStringPropertyImplementation']/method[@name='getObject' and count(parameter)=0]"
			[Register ("getObject", "()Ljava/lang/String;", "GetGetObjectHandler")]
			get {
				if (id_getObject == IntPtr.Zero)
					id_getObject = JNIEnv.GetMethodID (class_ref, "getObject", "()Ljava/lang/String;");
				try {

					if (((object) this).GetType () == ThresholdType)
						return JNIEnv.GetString (JNIEnv.CallObjectMethod (((global::Java.Lang.Object) this).Handle, id_getObject), JniHandleOwnership.TransferLocalRef);
					else
						return JNIEnv.GetString (JNIEnv.CallNonvirtualObjectMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "getObject", "()Ljava/lang/String;")), JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='GenericStringPropertyImplementation']/method[@name='SetObject' and count(parameter)=1 and parameter[1][@type='java.lang.String']]"
			[Register ("SetObject", "(Ljava/lang/String;)V", "GetSetObject_Ljava_lang_String_Handler")]
			set {
				if (id_SetObject_Ljava_lang_String_ == IntPtr.Zero)
					id_SetObject_Ljava_lang_String_ = JNIEnv.GetMethodID (class_ref, "SetObject", "(Ljava/lang/String;)V");
				IntPtr native_value = JNIEnv.NewString (value);
				try {
					JValue* __args = stackalloc JValue [1];
					__args [0] = new JValue (native_value);

					if (((object) this).GetType () == ThresholdType)
						JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_SetObject_Ljava_lang_String_, __args);
					else
						JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "SetObject", "(Ljava/lang/String;)V"), __args);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		// This method is explicitly implemented as a member of an instantiated Test.ME.IGenericPropertyInterface
		global::Java.Lang.Object global::Test.ME.IGenericPropertyInterface.Object {
			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='GenericPropertyInterface']/method[@name='getObject' and count(parameter)=0]"
			[Register ("getObject", "()Ljava/lang/Object;", "GetGetObjectHandler:Test.ME.IGenericPropertyInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")] get {
				return Object;
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='GenericPropertyInterface']/method[@name='setObject' and count(parameter)=1 and parameter[1][@type='T']]"
			[Register ("setObject", "(Ljava/lang/Object;)V", "GetSetObject_Ljava_lang_Object_Handler:Test.ME.IGenericPropertyInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")] set {
				Object = value.ToString ();
			}
		}

	}
}
