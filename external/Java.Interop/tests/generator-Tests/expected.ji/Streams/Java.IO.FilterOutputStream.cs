using System;
using System.Collections.Generic;
using Java.Interop;

namespace Java.IO {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.io']/class[@name='FilterOutputStream']"
	[global::Java.Interop.JniTypeSignature ("java/io/FilterOutputStream", GenerateJavaPeer=false)]
	public partial class FilterOutputStream : global::Java.IO.OutputStream {
		static readonly JniPeerMembers _members = new JniPeerMembers ("java/io/FilterOutputStream", typeof (FilterOutputStream));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected FilterOutputStream (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='FilterOutputStream']/method[@name='write' and count(parameter)=1 and parameter[1][@type='int']]"
		public override unsafe void Write (int oneByte)
		{
			const string __id = "write.(I)V";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (oneByte);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
			} finally {
			}
		}

	}
}
