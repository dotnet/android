using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='PublicFinalClass']"
	[global::Android.Runtime.Register ("xamarin/test/PublicFinalClass", DoNotGenerateAcw=true)]
	public sealed partial class PublicFinalClass : global::Xamarin.Test.BasePublicClass {

		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/PublicFinalClass", typeof (PublicFinalClass));
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

		internal PublicFinalClass (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='PublicFinalClass']/method[@name='publicMethod' and count(parameter)=0]"
		[Register ("publicMethod", "()V", "")]
		public unsafe void PublicMethod ()
		{
			const string __id = "publicMethod.()V";
			try {
				_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='PackageClassB']/method[@name='packageMethodB' and count(parameter)=0]"
		[Register ("packageMethodB", "()V", "")]
		public unsafe void PackageMethodB ()
		{
			const string __id = "packageMethodB.()V";
			try {
				_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='PackageClassA']/method[@name='packageMethodA' and count(parameter)=0]"
		[Register ("packageMethodA", "()V", "")]
		public unsafe void PackageMethodA ()
		{
			const string __id = "packageMethodA.()V";
			try {
				_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			} finally {
			}
		}

	}
}
