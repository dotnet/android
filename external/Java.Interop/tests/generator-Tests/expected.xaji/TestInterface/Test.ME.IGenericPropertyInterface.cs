// Java allows a type to redeclare a member that a base type or base interface already
// declares, including members that are hand-bound instead of generated.
#pragma warning disable 0108, 0114
// Java types may declare a `finalize()` method, which is bound as `Finalize()`.
#pragma warning disable 0465
// Java deprecates types and members independently of the APIs that use, declare or
// override them, so a binding that is not deprecated can still reference one that is.
#pragma warning disable 0618, 0672, 0809
// Java nullness annotations are not required to agree between a member and the member
// it overrides or implements, and are absent from much of the API surface.
#pragma warning disable 8603, 8604, 8625, 8764, 8765, 8766, 8767, 8768

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

		static IntPtr n_GetObject (IntPtr jnienv, IntPtr native__this)
		{
			unsafe {
				return global::Java.Interop.JniMarshal.SafeInvokeFunc (jnienv, native__this, &__n_GetObject);
			}
		}
		private static IntPtr __n_GetObject (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Test.ME.IGenericPropertyInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToLocalJniHandle (__this.Object);
		}
#pragma warning restore 0169

		static Delegate cb_setObject_SetObject_Ljava_lang_Object__V;
#pragma warning disable 0169
		static Delegate GetSetObject_Ljava_lang_Object_Handler ()
		{
			return cb_setObject_SetObject_Ljava_lang_Object__V ??= new _JniMarshal_PPL_V (n_SetObject_Ljava_lang_Object_);
		}

		static void n_SetObject_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native__object)
		{
			unsafe {
				global::Java.Interop.JniMarshal.SafeInvokeAction (jnienv, native__this, native__object, &__n_SetObject_Ljava_lang_Object_);
			}
		}
		private static void __n_SetObject_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native__object)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Test.ME.IGenericPropertyInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var @object = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native__object, JniHandleOwnership.DoNotTransfer);
			__this.Object = @object;
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
