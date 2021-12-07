using System;
using System.Collections.Generic;
using Java.Interop;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='Class']"
	[global::Java.Interop.JniTypeSignature ("java/lang/Class", GenerateJavaPeer=false)]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T"})]
	public partial class Class : global::Java.Lang.Object {
		static readonly JniPeerMembers _members = new JniPeerMembers ("java/lang/Class", typeof (Class));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected Class (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

	}
}
