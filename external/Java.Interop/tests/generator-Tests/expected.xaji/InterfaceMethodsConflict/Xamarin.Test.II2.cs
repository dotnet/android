using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath interface reference: path="/api/package[@name='xamarin.test']/interface[@name='I2']"
	[Register ("xamarin/test/I2", "", "Xamarin.Test.II2Invoker")]
	public partial interface II2 : IJavaObject, IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/interface[@name='I2']/method[@name='close' and count(parameter)=0]"
		[Register ("close", "()V", "GetCloseHandler:Xamarin.Test.II2Invoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		void Close ();

	}

	[global::Android.Runtime.Register ("xamarin/test/I2", DoNotGenerateAcw=true)]
	internal partial class II2Invoker : global::Java.Lang.Object, II2 {
		static IntPtr java_class_ref {
			get { return _members_xamarin_test_I2.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_xamarin_test_I2; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members_xamarin_test_I2.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members_xamarin_test_I2.ManagedPeerType; }
		}

		static readonly JniPeerMembers _members_xamarin_test_I2 = new XAPeerMembers ("xamarin/test/I2", typeof (II2Invoker));

		public II2Invoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_close;
#pragma warning disable 0169
		static Delegate GetCloseHandler ()
		{
			if (cb_close == null)
				cb_close = JNINativeWrapper.CreateDelegate (new _JniMarshal_PP_V (n_Close));
			return cb_close;
		}

		static void n_Close (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.II2> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Close ();
		}
#pragma warning restore 0169

		public unsafe void Close ()
		{
			const string __id = "close.()V";
			try {
				_members_xamarin_test_I2.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			} finally {
			}
		}

	}
}
