using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Test.ME {

	// Metadata.xml XPath interface reference: path="/api/package[@name='test.me']/interface[@name='GenericPropertyInterface']"
	[Register ("test/me/GenericPropertyInterface", "", "Test.ME.IGenericPropertyInterfaceInvoker")]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T"})]
	public partial interface IGenericPropertyInterface : IJavaObject, IJavaPeerable {
		global::Java.Lang.Object Object {
			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='GenericPropertyInterface']/method[@name='getObject' and count(parameter)=0]"
			[Register ("getObject", "()Ljava/lang/Object;", "GetGetObjectHandler:Test.ME.IGenericPropertyInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
			get; 

			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='GenericPropertyInterface']/method[@name='setObject' and count(parameter)=1 and parameter[1][@type='T']]"
			[Register ("setObject", "(Ljava/lang/Object;)V", "GetSetObject_Ljava_lang_Object_Handler:Test.ME.IGenericPropertyInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
			set; 
		}

	}

	[global::Android.Runtime.Register ("test/me/GenericPropertyInterface", DoNotGenerateAcw=true)]
	internal partial class IGenericPropertyInterfaceInvoker : global::Java.Lang.Object, IGenericPropertyInterface {
		static IntPtr java_class_ref {
			get { return _members_test_me_GenericPropertyInterface.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_test_me_GenericPropertyInterface; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members_test_me_GenericPropertyInterface.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members_test_me_GenericPropertyInterface.ManagedPeerType; }
		}

		static readonly JniPeerMembers _members_test_me_GenericPropertyInterface = new XAPeerMembers ("test/me/GenericPropertyInterface", typeof (IGenericPropertyInterfaceInvoker));

		public IGenericPropertyInterfaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_getObject_GetObject_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetGetObjectHandler ()
		{
			return cb_getObject_GetObject_Ljava_lang_Object_ ??= new _JniMarshal_PP_L (n_GetObject);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		static IntPtr n_GetObject (IntPtr jnienv, IntPtr native__this)
		{
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				var __this = global::Java.Lang.Object.GetObject<global::Test.ME.IGenericPropertyInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				return JNIEnv.ToLocalJniHandle (__this.Object);
			} catch (global::System.Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}
#pragma warning restore 0169

		static Delegate cb_setObject_SetObject_Ljava_lang_Object__V;
#pragma warning disable 0169
		static Delegate GetSetObject_Ljava_lang_Object_Handler ()
		{
			return cb_setObject_SetObject_Ljava_lang_Object__V ??= new _JniMarshal_PPL_V (n_SetObject_Ljava_lang_Object_);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		static void n_SetObject_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native__object)
		{
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				var __this = global::Java.Lang.Object.GetObject<global::Test.ME.IGenericPropertyInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var @object = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native__object, JniHandleOwnership.DoNotTransfer);
				__this.Object = @object;
			} catch (global::System.Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}
#pragma warning restore 0169

		public unsafe global::Java.Lang.Object Object {
			get {
				const string __id = "getObject.()Ljava/lang/Object;";
				try {
					var __rm = _members_test_me_GenericPropertyInterface.InstanceMethods.InvokeAbstractObjectMethod (__id, this, null);
					return (global::Java.Lang.Object) global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (__rm.Handle, JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
			set {
				const string __id = "setObject.(Ljava/lang/Object;)V";
				IntPtr native_value = JNIEnv.ToLocalJniHandle (value);
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (native_value);
					_members_test_me_GenericPropertyInterface.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
					global::System.GC.KeepAlive (value);
				}
			}
		}

	}
}
