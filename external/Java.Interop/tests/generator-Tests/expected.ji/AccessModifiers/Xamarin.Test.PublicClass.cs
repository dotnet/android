using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='PublicClass']"
	[global::Android.Runtime.Register ("xamarin/test/PublicClass", DoNotGenerateAcw=true)]
	public partial class PublicClass : global::Java.Lang.Object {

		internal static new readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/PublicClass", typeof (PublicClass));
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

		protected PublicClass (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='xamarin.test']/class[@name='PublicClass']/constructor[@name='PublicClass' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe PublicClass ()
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
				cb_foo = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr>) n_Foo);
			return cb_foo;
		}

		static void n_Foo (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.PublicClass __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.PublicClass> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Foo ();
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='PublicClass']/method[@name='foo' and count(parameter)=0]"
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
