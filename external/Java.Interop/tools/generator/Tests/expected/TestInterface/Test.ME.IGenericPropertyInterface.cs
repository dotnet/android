using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Test.ME {

	// Metadata.xml XPath interface reference: path="/api/package[@name='test.me']/interface[@name='GenericPropertyInterface']"
	[Register ("test/me/GenericPropertyInterface", "", "Test.ME.IGenericPropertyInterfaceInvoker")]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T"})]
	public partial interface IGenericPropertyInterface : IJavaObject {

		global::Java.Lang.Object Object {
			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='GenericPropertyInterface']/method[@name='getObject' and count(parameter)=0]"
			[Register ("getObject", "()Ljava/lang/Object;", "GetGetObjectHandler:Test.ME.IGenericPropertyInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")] get;
			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='GenericPropertyInterface']/method[@name='setObject' and count(parameter)=1 and parameter[1][@type='T']]"
			[Register ("setObject", "(Ljava/lang/Object;)V", "GetSetObject_Ljava_lang_Object_Handler:Test.ME.IGenericPropertyInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")] set;
		}

	}

	[global::Android.Runtime.Register ("test/me/GenericPropertyInterface", DoNotGenerateAcw=true)]
	internal class IGenericPropertyInterfaceInvoker : global::Java.Lang.Object, IGenericPropertyInterface {

		static IntPtr java_class_ref = JNIEnv.FindClass ("test/me/GenericPropertyInterface");

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (IGenericPropertyInterfaceInvoker); }
		}

		IntPtr class_ref;

		public static IGenericPropertyInterface GetObject (IntPtr handle, JniHandleOwnership transfer)
		{
			return global::Java.Lang.Object.GetObject<IGenericPropertyInterface> (handle, transfer);
		}

		static IntPtr Validate (IntPtr handle)
		{
			if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
				throw new InvalidCastException (string.Format ("Unable to convert instance of type '{0}' to type '{1}'.",
							JNIEnv.GetClassNameFromInstance (handle), "test.me.GenericPropertyInterface"));
			return handle;
		}

		protected override void Dispose (bool disposing)
		{
			if (this.class_ref != IntPtr.Zero)
				JNIEnv.DeleteGlobalRef (this.class_ref);
			this.class_ref = IntPtr.Zero;
			base.Dispose (disposing);
		}

		public IGenericPropertyInterfaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
		{
			IntPtr local_ref = JNIEnv.GetObjectClass (((global::Java.Lang.Object) this).Handle);
			this.class_ref = JNIEnv.NewGlobalRef (local_ref);
			JNIEnv.DeleteLocalRef (local_ref);
		}

		static Delegate cb_getObject;
#pragma warning disable 0169
		static Delegate GetGetObjectHandler ()
		{
			if (cb_getObject == null)
				cb_getObject = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr>) n_GetObject);
			return cb_getObject;
		}

		static IntPtr n_GetObject (IntPtr jnienv, IntPtr native__this)
		{
			global::Test.ME.IGenericPropertyInterface __this = global::Java.Lang.Object.GetObject<global::Test.ME.IGenericPropertyInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToLocalJniHandle (__this.Object);
		}
#pragma warning restore 0169

		static Delegate cb_setObject_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetSetObject_Ljava_lang_Object_Handler ()
		{
			if (cb_setObject_Ljava_lang_Object_ == null)
				cb_setObject_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_SetObject_Ljava_lang_Object_);
			return cb_setObject_Ljava_lang_Object_;
		}

		static void n_SetObject_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			global::Test.ME.IGenericPropertyInterface __this = global::Java.Lang.Object.GetObject<global::Test.ME.IGenericPropertyInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Java.Lang.Object value = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_value, JniHandleOwnership.DoNotTransfer);
			__this.Object = value;
		}
#pragma warning restore 0169

		IntPtr id_getObject;
		IntPtr id_setObject_Ljava_lang_Object_;
		public unsafe global::Java.Lang.Object Object {
			get {
				if (id_getObject == IntPtr.Zero)
					id_getObject = JNIEnv.GetMethodID (class_ref, "getObject", "()Ljava/lang/Object;");
				return (Java.Lang.Object) global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (JNIEnv.CallObjectMethod (((global::Java.Lang.Object) this).Handle, id_getObject), JniHandleOwnership.TransferLocalRef);
			}
			set {
				if (id_setObject_Ljava_lang_Object_ == IntPtr.Zero)
					id_setObject_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "setObject", "(Ljava/lang/Object;)V");
				IntPtr native_value = JNIEnv.ToLocalJniHandle (value);
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native_value);
				JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_setObject_Ljava_lang_Object_, __args);
				JNIEnv.DeleteLocalRef (native_value);
			}
		}

	}

}
