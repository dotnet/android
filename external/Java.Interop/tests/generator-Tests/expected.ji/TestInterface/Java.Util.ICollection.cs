// Java allows a type to redeclare a member that a base type or base interface already
// declares, including members that are hand-bound instead of generated.
#pragma warning disable 0108, 0114
// Java types may declare a `finalize()` method, which is bound as `Finalize()`.
#pragma warning disable 0465
// Java deprecates types and members independently of the APIs that use, declare or
// override them, so a binding that is not deprecated can still reference one that is.
#pragma warning disable 0618, 0672, 0809
// Java nullness annotations are not required to agree between a member and the member
// it overrides or implements, and are absent from much of the API surface.
#pragma warning disable 8603, 8604, 8625, 8764, 8765, 8766, 8767, 8768

using System;
using System.Collections.Generic;
using Java.Interop;

namespace Java.Util {

	// Metadata.xml XPath interface reference: path="/api/package[@name='java.util']/interface[@name='Collection']"
	[global::Java.Interop.JniTypeSignature ("java/util/Collection", GenerateJavaPeer=false, InvokerType=typeof (Java.Util.ICollectionInvoker))]
	[global::Java.Interop.JavaTypeParameters (new string [] {"E"})]
	public partial interface ICollection : IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='java.util']/interface[@name='Collection']/method[@name='add' and count(parameter)=1 and parameter[1][@type='E']]"
		[global::Java.Interop.JniMethodSignature ("add", "(Ljava/lang/Object;)Z")]
		bool Add (global::Java.Lang.Object e);

		// Metadata.xml XPath method reference: path="/api/package[@name='java.util']/interface[@name='Collection']/method[@name='clear' and count(parameter)=0]"
		[global::Java.Interop.JniMethodSignature ("clear", "()V")]
		void Clear ();

	}

	[global::Java.Interop.JniTypeSignature ("java/util/Collection", GenerateJavaPeer=false)]
	internal partial class ICollectionInvoker : global::Java.Lang.Object, ICollection {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_java_util_Collection; }
		}

		static readonly JniPeerMembers _members_java_util_Collection = new JniPeerMembers ("java/util/Collection", typeof (ICollectionInvoker));

		public ICollectionInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		public unsafe bool Add (global::Java.Lang.Object e)
		{
			const string __id = "add.(Ljava/lang/Object;)Z";
			var native_e = (e?.PeerReference ?? default);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_e);
				var __rm = _members_java_util_Collection.InstanceMethods.InvokeAbstractBooleanMethod (__id, this, __args);
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
