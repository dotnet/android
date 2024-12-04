using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Test.ME {

	// Metadata.xml XPath interface reference: path="/api/package[@name='test.me']/interface[@name='GenericInterface']"
	[Register ("test/me/GenericInterface", "", "Test.ME.IGenericInterfaceInvoker")]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T"})]
	public partial interface IGenericInterface : IJavaObject, IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='GenericInterface']/method[@name='SetObject' and count(parameter)=1 and parameter[1][@type='T']]"
		[Register ("SetObject", "(Ljava/lang/Object;)V", "GetSetObject_Ljava_lang_Object_Handler:Test.ME.IGenericInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		void SetObject (global::Java.Lang.Object value);

	}

	[global::Android.Runtime.Register ("test/me/GenericInterface", DoNotGenerateAcw=true)]
	internal partial class IGenericInterfaceInvoker : global::Java.Lang.Object, IGenericInterface {
		static IntPtr java_class_ref {
			get { return _members_test_me_GenericInterface.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_test_me_GenericInterface; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members_test_me_GenericInterface.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members_test_me_GenericInterface.ManagedPeerType; }
		}

		static readonly JniPeerMembers _members_test_me_GenericInterface = new XAPeerMembers ("test/me/GenericInterface", typeof (IGenericInterfaceInvoker));

		public IGenericInterfaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_SetObject_SetObject_Ljava_lang_Object__V;
#pragma warning disable 0169
		static Delegate GetSetObject_Ljava_lang_Object_Handler ()
		{
			return cb_SetObject_SetObject_Ljava_lang_Object__V ??= new _JniMarshal_PPL_V (n_SetObject_Ljava_lang_Object_);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		static void n_SetObject_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				var __this = global::Java.Lang.Object.GetObject<global::Test.ME.IGenericInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var value = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_value, JniHandleOwnership.DoNotTransfer);
				__this.SetObject (value);
			} catch (global::System.Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}
#pragma warning restore 0169

		public unsafe void SetObject (global::Java.Lang.Object value)
		{
			const string __id = "SetObject.(Ljava/lang/Object;)V";
			IntPtr native_value = JNIEnv.ToLocalJniHandle (value);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_value);
				_members_test_me_GenericInterface.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_value);
				global::System.GC.KeepAlive (value);
			}
		}

	}
}
