using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='String']"
	[global::Android.Runtime.Register ("java/lang/String", DoNotGenerateAcw=true)]
	public partial class String : global::Java.Lang.Object {

		static readonly JniPeerMembers _members = new JniPeerMembers ("java/lang/String", typeof (String));
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

		protected String (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

	}
}
