using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Test.ME {

	// Metadata.xml XPath interface reference: path="/api/package[@name='test.me']/interface[@name='GenericInterface']"
	[Register ("test/me/GenericInterface", "", "Test.ME.IGenericInterfaceInvoker")]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T"})]
	public partial interface IGenericInterface : IJavaObject {

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='GenericInterface']/method[@name='SetObject' and count(parameter)=1 and parameter[1][@type='T']]"
		[Register ("SetObject", "(Ljava/lang/Object;)V", "GetSetObject_Ljava_lang_Object_Handler:Test.ME.IGenericInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		void SetObject (global::Java.Lang.Object value);

	}

	[global::Android.Runtime.Register ("test/me/GenericInterface", DoNotGenerateAcw=true)]
	internal class IGenericInterfaceInvoker : global::Java.Lang.Object, IGenericInterface {

		internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("test/me/GenericInterface", typeof (IGenericInterfaceInvoker));

		static IntPtr java_class_ref {
			get { return _members.JniPeerType.PeerReference.Handle; }
		}

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

		IntPtr class_ref;

		public static IGenericInterface GetObject (IntPtr handle, JniHandleOwnership transfer)
		{
			return global::Java.Lang.Object.GetObject<IGenericInterface> (handle, transfer);
		}

		static IntPtr Validate (IntPtr handle)
		{
			if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
				throw new InvalidCastException (string.Format ("Unable to convert instance of type '{0}' to type '{1}'.",
							JNIEnv.GetClassNameFromInstance (handle), "test.me.GenericInterface"));
			return handle;
		}

		protected override void Dispose (bool disposing)
		{
			if (this.class_ref != IntPtr.Zero)
				JNIEnv.DeleteGlobalRef (this.class_ref);
			this.class_ref = IntPtr.Zero;
			base.Dispose (disposing);
		}

		public IGenericInterfaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
		{
			IntPtr local_ref = JNIEnv.GetObjectClass (((global::Java.Lang.Object) this).Handle);
			this.class_ref = JNIEnv.NewGlobalRef (local_ref);
			JNIEnv.DeleteLocalRef (local_ref);
		}

		static Delegate cb_SetObject_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetSetObject_Ljava_lang_Object_Handler ()
		{
			if (cb_SetObject_Ljava_lang_Object_ == null)
				cb_SetObject_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_SetObject_Ljava_lang_Object_);
			return cb_SetObject_Ljava_lang_Object_;
		}

		static void n_SetObject_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			global::Test.ME.IGenericInterface __this = global::Java.Lang.Object.GetObject<global::Test.ME.IGenericInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Java.Lang.Object value = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_value, JniHandleOwnership.DoNotTransfer);
			__this.SetObject (value);
		}
#pragma warning restore 0169

		IntPtr id_SetObject_Ljava_lang_Object_;
		public unsafe void SetObject (global::Java.Lang.Object value)
		{
			if (id_SetObject_Ljava_lang_Object_ == IntPtr.Zero)
				id_SetObject_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "SetObject", "(Ljava/lang/Object;)V");
			IntPtr native_value = JNIEnv.ToLocalJniHandle (value);
			JValue* __args = stackalloc JValue [1];
			__args [0] = new JValue (native_value);
			JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_SetObject_Ljava_lang_Object_, __args);
			JNIEnv.DeleteLocalRef (native_value);
		}

	}

}
