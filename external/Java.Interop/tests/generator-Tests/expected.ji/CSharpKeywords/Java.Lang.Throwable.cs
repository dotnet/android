using System;
using System.Collections.Generic;
using Java.Interop;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='Throwable']"
	[global::Java.Interop.JniTypeSignature ("java/lang/Throwable", GenerateJavaPeer=false)]
	public partial class Throwable {
		static readonly JniPeerMembers _members = new JniPeerMembers ("java/lang/Throwable", typeof (Throwable));

		public new virtual unsafe string Message {
			// Metadata.xml XPath method reference: path="/api/package[@name='java.lang']/class[@name='Throwable']/method[@name='getMessage' and count(parameter)=0]"
			get {
				const string __id = "getMessage.()Ljava/lang/String;";
				try {
					var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
					return global::Java.Interop.JniEnvironment.Strings.ToString (ref __rm, JniObjectReferenceOptions.CopyAndDispose);
				} finally {
				}
			}
		}

	}
}
