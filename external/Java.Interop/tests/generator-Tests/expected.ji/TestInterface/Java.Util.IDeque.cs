using System;
using System.Collections.Generic;
using Java.Interop;

namespace Java.Util {

	// Metadata.xml XPath interface reference: path="/api/package[@name='java.util']/interface[@name='Deque']"
	[global::Java.Interop.JniTypeSignature ("java/util/Deque", GenerateJavaPeer=false, InvokerType=typeof (Java.Util.IDequeInvoker))]
	[global::Java.Interop.JavaTypeParameters (new string [] {"E"})]
	public partial interface IDeque : global::Java.Util.IQueue {
		// Metadata.xml XPath method reference: path="/api/package[@name='java.util']/interface[@name='Deque']/method[@name='add' and count(parameter)=1 and parameter[1][@type='E']]"
		[global::Java.Interop.JniMethodSignature ("add", "(Ljava/lang/Object;)Z")]
		bool Add (global::Java.Lang.Object e);

	}

	[global::Java.Interop.JniTypeSignature ("java/util/Deque", GenerateJavaPeer=false)]
	internal partial class IDequeInvoker : global::Java.Lang.Object, IDeque {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_java_util_Deque; }
		}

		static readonly JniPeerMembers _members_java_util_Collection = new JniPeerMembers ("java/util/Collection", typeof (IDequeInvoker));

		static readonly JniPeerMembers _members_java_util_Deque = new JniPeerMembers ("java/util/Deque", typeof (IDequeInvoker));

		static readonly JniPeerMembers _members_java_util_Queue = new JniPeerMembers ("java/util/Queue", typeof (IDequeInvoker));

		public IDequeInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		public unsafe bool Add (global::Java.Lang.Object e)
		{
			const string __id = "add.(Ljava/lang/Object;)Z";
			var native_e = (e?.PeerReference ?? default);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_e);
				var __rm = _members_java_util_Deque.InstanceMethods.InvokeAbstractBooleanMethod (__id, this, __args);
				return __rm;
			} finally {
				global::System.GC.KeepAlive (e);
			}
		}

		public unsafe void Clear ()
		{
			const string __id = "clear.()V";
			try {
				_members_java_util_Collection.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			} finally {
			}
		}

	}
}
