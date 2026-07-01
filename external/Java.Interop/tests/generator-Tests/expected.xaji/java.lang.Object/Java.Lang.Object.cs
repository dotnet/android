using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='Object']"
	[global::Android.Runtime.Register ("java/lang/Object", DoNotGenerateAcw=true)]
	public partial class Object {
		static readonly JniPeerMembers _members = new JniPeerMembers ("java/lang/Object", typeof (Object));

		internal static IntPtr class_ref {
			get { return _members.JniPeerType.PeerReference.Handle; }
		}

	}
}
