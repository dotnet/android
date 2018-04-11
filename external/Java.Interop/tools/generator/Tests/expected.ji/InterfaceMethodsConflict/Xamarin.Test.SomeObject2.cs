using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject2']"
	[global::Android.Runtime.Register ("xamarin/test/SomeObject2", DoNotGenerateAcw=true)]
	public abstract partial class SomeObject2 : global::Java.Lang.Object, global::Xamarin.Test.II1, global::Xamarin.Test.II2 {

		internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("xamarin/test/SomeObject2", typeof (SomeObject2));
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

		protected SomeObject2 (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static Delegate cb_irrelevant;
#pragma warning disable 0169
		static Delegate GetIrrelevantHandler ()
		{
			if (cb_irrelevant == null)
				cb_irrelevant = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr>) n_Irrelevant);
			return cb_irrelevant;
		}

		static void n_Irrelevant (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.SomeObject2 __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject2> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Irrelevant ();
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject2']/method[@name='irrelevant' and count(parameter)=0]"
		[Register ("irrelevant", "()V", "GetIrrelevantHandler")]
		public virtual unsafe void Irrelevant ()
		{
			const string __id = "irrelevant.()V";
			try {
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, null);
			} finally {
			}
		}

		static Delegate cb_close;
#pragma warning disable 0169
		static Delegate GetCloseHandler ()
		{
			if (cb_close == null)
				cb_close = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr>) n_Close);
			return cb_close;
		}

		static void n_Close (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.SomeObject2 __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject2> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Close ();
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/interface[@name='I1']/method[@name='close' and count(parameter)=0]"
		[Register ("close", "()V", "GetCloseHandler")]
		public abstract void Close ();

	}

	[global::Android.Runtime.Register ("xamarin/test/SomeObject2", DoNotGenerateAcw=true)]
	internal partial class SomeObject2Invoker : SomeObject2 {

		public SomeObject2Invoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("xamarin/test/SomeObject2", typeof (SomeObject2Invoker));

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/interface[@name='I1']/method[@name='close' and count(parameter)=0]"
		[Register ("close", "()V", "GetCloseHandler")]
		public override unsafe void Close ()
		{
			const string __id = "close.()V";
			try {
				_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			} finally {
			}
		}

	}

}
