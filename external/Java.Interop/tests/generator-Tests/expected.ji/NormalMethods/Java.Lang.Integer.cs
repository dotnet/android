using System;
using System.Collections.Generic;
using Java.Interop;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='Integer']"
	[global::Java.Interop.JniTypeSignature ("java/lang/Integer", GenerateJavaPeer=false)]
	public partial class Integer : global::Java.Lang.Object {
		static readonly JniPeerMembers _members = new JniPeerMembers ("java/lang/Integer", typeof (Integer));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected Integer (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

	}
}
