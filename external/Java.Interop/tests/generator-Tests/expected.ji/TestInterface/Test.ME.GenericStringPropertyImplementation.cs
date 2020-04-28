using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Test.ME {

	// Metadata.xml XPath class reference: path="/api/package[@name='test.me']/class[@name='GenericStringPropertyImplementation']"
	[global::Android.Runtime.Register ("test/me/GenericStringPropertyImplementation", DoNotGenerateAcw=true)]
	public partial class GenericStringPropertyImplementation : global::Java.Lang.Object, global::Test.ME.IGenericPropertyInterface {

		static readonly JniPeerMembers _members = new JniPeerMembers ("test/me/GenericStringPropertyImplementation", typeof (GenericStringPropertyImplementation));
		internal static new IntPtr class_ref {
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

		protected GenericStringPropertyImplementation (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='test.me']/class[@name='GenericStringPropertyImplementation']/constructor[@name='GenericStringPropertyImplementation' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe GenericStringPropertyImplementation ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string __id = "()V";

			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), null);
				SetHandle (__r.Handle, JniHandleOwnership.TransferLocalRef);
				_members.InstanceMethods.FinishCreateInstance (__id, this, null);
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

		public virtual unsafe string Object {
			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='GenericStringPropertyImplementation']/method[@name='getObject' and count(parameter)=0]"
			[Register ("getObject", "()Ljava/lang/String;", "GetGetObjectHandler")]
			get {
				const string __id = "getObject.()Ljava/lang/String;";
				try {
					var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
					return JNIEnv.GetString (__rm.Handle, JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='GenericStringPropertyImplementation']/method[@name='SetObject' and count(parameter)=1 and parameter[1][@type='java.lang.String']]"
			[Register ("SetObject", "(Ljava/lang/String;)V", "GetSetObject_Ljava_lang_String_Handler")]
			set {
				const string __id = "SetObject.(Ljava/lang/String;)V";
				IntPtr native_value = JNIEnv.NewString (value);
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (native_value);
					_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
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
