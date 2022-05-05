using System;
using System.Collections.Generic;
using Java.Interop;

namespace Java.IO {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.io']/class[@name='IOException']"
	[global::Java.Interop.JniTypeSignature ("java/io/IOException", GenerateJavaPeer=false)]
	public abstract partial class IOException : global::Java.Lang.Throwable {
		static readonly JniPeerMembers _members = new JniPeerMembers ("java/io/IOException", typeof (IOException));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected IOException (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='IOException']/method[@name='printStackTrace' and count(parameter)=0]"
		[global::Java.Interop.JniMethodSignature ("printStackTrace", "()V")]
		public virtual unsafe void PrintStackTrace ()
		{
			const string __id = "printStackTrace.()V";
			try {
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, null);
			} finally {
			}
		}

	}

	[global::Java.Interop.JniTypeSignature ("java/io/IOException", GenerateJavaPeer=false)]
	internal partial class IOExceptionInvoker : IOException {
		public IOExceptionInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		static readonly JniPeerMembers _members = new JniPeerMembers ("java/io/IOException", typeof (IOExceptionInvoker));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

	}
}
