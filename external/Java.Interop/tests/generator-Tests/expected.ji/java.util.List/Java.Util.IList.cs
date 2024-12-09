using System;
using System.Collections.Generic;
using Java.Interop;

namespace Java.Util {

	// Metadata.xml XPath interface reference: path="/api/package[@name='java.util']/interface[@name='List']"
	[global::Java.Interop.JniTypeSignature ("java/util/List", GenerateJavaPeer=false, InvokerType=typeof (Java.Util.IListInvoker))]
	[global::Java.Interop.JavaTypeParameters (new string [] {"E"})]
	public partial interface IList : IJavaPeerable {
	}

	[global::Java.Interop.JniTypeSignature ("java/util/List", GenerateJavaPeer=false)]
	internal partial class IListInvoker : global::Java.Lang.Object, IList {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_java_util_List; }
		}

		static readonly JniPeerMembers _members_java_util_List = new JniPeerMembers ("java/util/List", typeof (IListInvoker));

		public IListInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

	}
}
