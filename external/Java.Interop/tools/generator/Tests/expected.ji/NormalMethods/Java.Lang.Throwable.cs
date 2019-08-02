using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='Throwable']"
	[global::Android.Runtime.Register ("java/lang/Throwable", DoNotGenerateAcw=true)]
	public partial class Throwable  {

		internal static readonly JniPeerMembers _members = new JniPeerMembers ("java/lang/Throwable", typeof (Throwable));
		internal static IntPtr class_ref {
			get {
				return _members.JniPeerType.PeerReference.Handle;
			}
		}

	}
}
