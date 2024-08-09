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
		static IntPtr java_class_ref {
			get { return _members_xamarin_test_I1.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_xamarin_test_I1; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members_xamarin_test_I1.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members_xamarin_test_I1.ManagedPeerType; }
		}

		static readonly JniPeerMembers _members_xamarin_test_I1 = new XAPeerMembers ("xamarin/test/I1", typeof (II1Invoker));

		public II1Invoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_close_Close_V;
#pragma warning disable 0169
		static Delegate GetCloseHandler ()
		{
			if (cb_close_Close_V == null)
				cb_close_Close_V = JNINativeWrapper.CreateDelegate (new _JniMarshal_PP_V (n_Close));
			return cb_close_Close_V;
		}

		static void n_Close (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.II1> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Close ();
		}
#pragma warning restore 0169

		public unsafe void Close ()
		{
			const string __id = "close.()V";
			try {
				_members_xamarin_test_I1.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			} finally {
			}
		}

	}
}
