using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Test.ME {

	// Metadata.xml XPath class reference: path="/api/package[@name='test.me']/class[@name='GenericStringImplementation']"
	[global::Android.Runtime.Register ("test/me/GenericStringImplementation", DoNotGenerateAcw=true)]
	public partial class GenericStringImplementation : global::Java.Lang.Object, global::Test.ME.IGenericInterface {

		internal            static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("test/me/GenericStringImplementation", typeof (GenericStringImplementation));
		internal static IntPtr class_ref {
			get {
				return _members.JniPeerType.PeerReference.Handle;
			}
		}

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override IntPtr ThresholdClass {
			get { return _members.JniPeerType.PeerReference.Handle; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

		protected GenericStringImplementation (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='test.me']/class[@name='GenericStringImplementation']/constructor[@name='GenericStringImplementation' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe GenericStringImplementation ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string __id = "()V";

			if (Handle != IntPtr.Zero)
				return;

			try {
				var __r = _members.InstanceMethods.StartCreateInstance (__id, GetType (), null);
				SetHandle (__r.Handle, JniHandleOwnership.TransferLocalRef);
				_members.InstanceMethods.FinishCreateInstance (__id, this, null);
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

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='GenericStringImplementation']/method[@name='SetObject' and count(parameter)=1 and parameter[1][@type='java.lang.String[]']]"
		[Register ("SetObject", "([Ljava/lang/String;)V", "GetSetObject_arrayLjava_lang_String_Handler")]
		public virtual unsafe void SetObject (string[] value)
		{
			const string __id = "SetObject.([Ljava/lang/String;)V";
			IntPtr native_value = JNIEnv.NewArray (value);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_value);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
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
