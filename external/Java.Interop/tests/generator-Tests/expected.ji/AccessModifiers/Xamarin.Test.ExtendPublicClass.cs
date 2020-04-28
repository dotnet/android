using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='ExtendPublicClass']"
	[global::Android.Runtime.Register ("xamarin/test/ExtendPublicClass", DoNotGenerateAcw=true)]
	public partial class ExtendPublicClass : global::Java.Lang.Object {

		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/ExtendPublicClass", typeof (ExtendPublicClass));
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

		protected ExtendPublicClass (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='xamarin.test']/class[@name='ExtendPublicClass']/constructor[@name='ExtendPublicClass' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe ExtendPublicClass ()
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

		static Delegate cb_foo;
#pragma warning disable 0169
		static Delegate GetFooHandler ()
		{
			if (cb_foo == null)
				cb_foo = JNINativeWrapper.CreateDelegate ((_JniMarshal_PP_V) n_Foo);
			return cb_foo;
		}

		static void n_Foo (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.ExtendPublicClass> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Foo ();
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='ExtendPublicClass']/method[@name='foo' and count(parameter)=0]"
		[Register ("foo", "()V", "GetFooHandler")]
		public virtual unsafe void Foo ()
		{
			const string __id = "foo.()V";
			try {
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, null);
			} finally {
			}
		}

	}
}
