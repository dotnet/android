using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Java.Util {

	// Metadata.xml XPath interface reference: path="/api/package[@name='java.util']/interface[@name='List']"
	[Register ("java/util/List", "", "Java.Util.IListInvoker")]
	[global::Java.Interop.JavaTypeParameters (new string [] {"E"})]
	public partial interface IList : IJavaObject, IJavaPeerable {
	}

	[global::Android.Runtime.Register ("java/util/List", DoNotGenerateAcw=true)]
	internal partial class IListInvoker : global::Java.Lang.Object, IList {
		static IntPtr java_class_ref {
			get { return _members_java_util_List.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_java_util_List; }
		}

		static readonly JniPeerMembers _members_java_util_List = new JniPeerMembers ("java/util/List", typeof (IListInvoker));

		public IListInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

	}
}
