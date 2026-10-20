using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath interface reference: path="/api/package[@name='xamarin.test']/interface[@name='I1']"
	[Register ("xamarin/test/I1", "", "Xamarin.Test.II1Invoker")]
	public partial interface II1 : IJavaObject, IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/interface[@name='I1']/method[@name='close' and count(parameter)=0]"
		[Register ("close", "()V", "GetCloseHandler:Xamarin.Test.II1Invoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		void Close ();

	}

	[global::Android.Runtime.Register ("xamarin/test/I1", DoNotGenerateAcw=true)]
	internal partial class II1Invoker : global::Java.Lang.Object, II1 {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_xamarin_test_I1; }
		}

		static readonly JniPeerMembers _members_xamarin_test_I1 = new JniPeerMembers ("xamarin/test/I1", typeof (II1Invoker));

		/// <safety>handle must be zero or a live JNI object implementing this interface. Use an attached thread (locals: creating thread), with no concurrent deletion through aliases during construction. transfer must match ownership; transferred references cannot be used again, even if construction throws.</safety>
		/// <remarks>This constructs a JNI peer, not an opaque-address container. RequiresUnsafe enforcement is deferred to preserve safe zero/DoNotTransfer base initializers; the raw-reference obligations still apply.</remarks>
		public II1Invoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_close_Close_V;
#pragma warning disable 0169
		static Delegate GetCloseHandler ()
		{
			unsafe {
				// SAFETY: The delegate matches the registered callback signature; JNI, not this connector, supplies its arguments.
				return cb_close_Close_V ??= new _JniMarshal_PP_V (n_Close);
			}
		}

		/// <safety>Use the registered JNI ABI on jnienv's attached thread. Receiver/reference arguments must match the registered types and stay live without concurrent deletion for this call; permitted nulls stay null. JNI retains borrowed ownership. The helper reports managed failures and may return default; invalid addresses are not validated.</safety>
		static unsafe void n_Close (IntPtr jnienv, IntPtr native__this)
		{
			unsafe {
				// SAFETY: JNI supplies this thread's environment and live typed references; the static target pointer is synchronous.
				global::Java.Interop.JniMarshal.SafeInvokeAction (jnienv, native__this, &__n_Close);
			}
		}
		/// <safety>Requires the entry point's live, type-correct borrowed references in its active JNI frame/thread, without concurrent deletion. Copy-back occurs on normal return only; failures propagate to the marshal helper.</safety>
		private static unsafe void __n_Close (IntPtr jnienv, IntPtr native__this)
		{
			unsafe {
				// SAFETY: The marshal frame keeps typed JNI references live on this thread through conversion and normal-return copy-back.
				var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.II1> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				__this.Close ();
			}
		}
#pragma warning restore 0169

		public void Close ()
		{
			const string __id = "close.()V";
			unsafe {
				// SAFETY: __id fixes argument types/count; initialized stack arguments and JNI locals stay on this thread for the synchronous call.
				_members_xamarin_test_I1.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			}
		}

	}
}
