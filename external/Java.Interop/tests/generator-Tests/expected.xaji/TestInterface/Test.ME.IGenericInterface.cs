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
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_test_me_GenericInterface; }
		}

		static readonly JniPeerMembers _members_test_me_GenericInterface = new JniPeerMembers ("test/me/GenericInterface", typeof (IGenericInterfaceInvoker));

		/// <safety>
		/// handle must be zero or a live JNI object implementing this interface. Use an attached
		/// thread (locals: creating thread), with no concurrent deletion through aliases during
		/// construction. transfer must match ownership; transferred references cannot be used again,
		/// even if construction throws.
		/// </safety>
		/// <remarks>
		/// This constructs a JNI peer, not an opaque-address container. RequiresUnsafe enforcement
		/// is deferred to preserve safe zero/DoNotTransfer base initializers; the raw-reference
		/// obligations still apply.
		/// </remarks>
		public IGenericInterfaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_SetObject_SetObject_Ljava_lang_Object__V;
#pragma warning disable 0169
		static Delegate GetSetObject_Ljava_lang_Object_Handler ()
		{
			unsafe {
				// SAFETY: The delegate matches the registered callback signature; JNI, not this connector, supplies its arguments.
				return cb_SetObject_SetObject_Ljava_lang_Object__V ??= new _JniMarshal_PPL_V (n_SetObject_Ljava_lang_Object_);
			}
		}

		/// <safety>
		/// Use the registered JNI ABI on jnienv's attached thread. Receiver/reference arguments
		/// must match the registered types and stay live without concurrent deletion for this call;
		/// permitted nulls stay null. JNI retains borrowed ownership. The helper reports managed
		/// failures and may return default; invalid addresses are not validated.
		/// </safety>
		static unsafe void n_SetObject_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			unsafe {
				// SAFETY: JNI supplies this thread's environment and live typed references; the static target pointer is synchronous.
				global::Java.Interop.JniMarshal.SafeInvokeAction (jnienv, native__this, native_value, &__n_SetObject_Ljava_lang_Object_);
			}
		}
		/// <safety>
		/// Requires the entry point's live, type-correct borrowed references in its active JNI
		/// frame/thread, without concurrent deletion. Copy-back occurs on normal return only;
		/// failures propagate to the marshal helper.
		/// </safety>
		private static unsafe void __n_SetObject_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			unsafe {
				// SAFETY: The marshal frame keeps typed JNI references live on this thread through conversion and normal-return copy-back.
				var __this = global::Java.Lang.Object.GetObject<global::Test.ME.IGenericInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var value = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_value, JniHandleOwnership.DoNotTransfer);
				__this.SetObject (value);
			}
		}
#pragma warning restore 0169

		public void SetObject (global::Java.Lang.Object value)
		{
			const string __id = "SetObject.(Ljava/lang/Object;)V";
			unsafe {
				// SAFETY: __id fixes argument types/count; initialized stack arguments and JNI locals stay on this thread for the synchronous call.
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
}
