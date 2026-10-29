using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath interface reference: path="/api/package[@name='xamarin.test']/interface[@name='ExtendedInterface']"
	[Register ("xamarin/test/ExtendedInterface", "", "Xamarin.Test.IExtendedInterfaceInvoker")]
	public partial interface IExtendedInterface : IJavaObject, IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/interface[@name='ExtendedInterface']/method[@name='extendedMethod' and count(parameter)=0]"
		[Register ("extendedMethod", "()V", "GetExtendedMethodHandler:Xamarin.Test.IExtendedInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		void ExtendedMethod ();

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/interface[@name='ExtendedInterface']/method[@name='baseMethod' and count(parameter)=0]"
		[Register ("baseMethod", "()V", "GetBaseMethodHandler:Xamarin.Test.IExtendedInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		void BaseMethod ();

	}

	[global::Android.Runtime.Register ("xamarin/test/ExtendedInterface", DoNotGenerateAcw=true)]
	internal partial class IExtendedInterfaceInvoker : global::Java.Lang.Object, IExtendedInterface {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_xamarin_test_ExtendedInterface; }
		}

		static readonly JniPeerMembers _members_xamarin_test_BaseInterface = new JniPeerMembers ("xamarin/test/BaseInterface", typeof (IExtendedInterfaceInvoker));

		static readonly JniPeerMembers _members_xamarin_test_ExtendedInterface = new JniPeerMembers ("xamarin/test/ExtendedInterface", typeof (IExtendedInterfaceInvoker));

		public IExtendedInterfaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_extendedMethod_ExtendedMethod_V;
#pragma warning disable 0169
		static Delegate GetExtendedMethodHandler ()
		{
			unsafe {
				// SAFETY: The delegate matches the registered callback signature; JNI, not this connector, supplies its arguments.
				return cb_extendedMethod_ExtendedMethod_V ??= new _JniMarshal_PP_V (n_ExtendedMethod);
			}
		}

		/// <safety>
		/// Use the registered JNI ABI on jnienv's attached thread. Receiver/reference arguments
		/// must match the registered types and stay live without concurrent deletion for this call;
		/// permitted nulls stay null. JNI retains borrowed ownership. The helper reports managed
		/// failures and may return default; invalid addresses are not validated.
		/// </safety>
		static unsafe void n_ExtendedMethod (IntPtr jnienv, IntPtr native__this)
		{
			unsafe {
				// SAFETY: JNI supplies this thread's environment and live typed references; the static target pointer is synchronous.
				global::Java.Interop.JniMarshal.SafeInvokeAction (jnienv, native__this, &__n_ExtendedMethod);
			}
		}
		/// <safety>
		/// Requires the entry point's live, type-correct borrowed references in its active JNI
		/// frame/thread, without concurrent deletion. Copy-back occurs on normal return only;
		/// failures propagate to the marshal helper.
		/// </safety>
		private static unsafe void __n_ExtendedMethod (IntPtr jnienv, IntPtr native__this)
		{
			unsafe {
				// SAFETY: The marshal frame keeps typed JNI references live on this thread through conversion and normal-return copy-back.
				var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.IExtendedInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				__this.ExtendedMethod ();
			}
		}
#pragma warning restore 0169

		public void ExtendedMethod ()
		{
			const string __id = "extendedMethod.()V";
			unsafe {
				// SAFETY: __id fixes argument types/count; initialized stack arguments and JNI locals stay on this thread for the synchronous call.
				_members_xamarin_test_ExtendedInterface.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			}
		}

		static Delegate cb_baseMethod_BaseMethod_V;
#pragma warning disable 0169
		static Delegate GetBaseMethodHandler ()
		{
			unsafe {
				// SAFETY: The delegate matches the registered callback signature; JNI, not this connector, supplies its arguments.
				return cb_baseMethod_BaseMethod_V ??= new _JniMarshal_PP_V (n_BaseMethod);
			}
		}

		/// <safety>
		/// Use the registered JNI ABI on jnienv's attached thread. Receiver/reference arguments
		/// must match the registered types and stay live without concurrent deletion for this call;
		/// permitted nulls stay null. JNI retains borrowed ownership. The helper reports managed
		/// failures and may return default; invalid addresses are not validated.
		/// </safety>
		static unsafe void n_BaseMethod (IntPtr jnienv, IntPtr native__this)
		{
			unsafe {
				// SAFETY: JNI supplies this thread's environment and live typed references; the static target pointer is synchronous.
				global::Java.Interop.JniMarshal.SafeInvokeAction (jnienv, native__this, &__n_BaseMethod);
			}
		}
		/// <safety>
		/// Requires the entry point's live, type-correct borrowed references in its active JNI
		/// frame/thread, without concurrent deletion. Copy-back occurs on normal return only;
		/// failures propagate to the marshal helper.
		/// </safety>
		private static unsafe void __n_BaseMethod (IntPtr jnienv, IntPtr native__this)
		{
			unsafe {
				// SAFETY: The marshal frame keeps typed JNI references live on this thread through conversion and normal-return copy-back.
				var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.IExtendedInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				__this.BaseMethod ();
			}
		}
#pragma warning restore 0169

		public void BaseMethod ()
		{
			const string __id = "baseMethod.()V";
			unsafe {
				// SAFETY: __id fixes argument types/count; initialized stack arguments and JNI locals stay on this thread for the synchronous call.
				_members_xamarin_test_ExtendedInterface.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			}
		}

	}
}
