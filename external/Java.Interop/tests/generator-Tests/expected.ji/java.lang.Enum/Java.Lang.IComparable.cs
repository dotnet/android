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

namespace Java.Lang {

	// Metadata.xml XPath interface reference: path="/api/package[@name='java.lang']/interface[@name='Comparable']"
	[global::Java.Interop.JniTypeSignature ("java/lang/Comparable", GenerateJavaPeer=false, InvokerType=typeof (Java.Lang.IComparableInvoker))]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T"})]
	public partial interface IComparable : IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='java.lang']/interface[@name='Comparable']/method[@name='compareTo' and count(parameter)=1 and parameter[1][@type='T']]"
		[global::Java.Interop.JniMethodSignature ("compareTo", "(Ljava/lang/Object;)I")]
		int CompareTo (global::Java.Lang.Object another);

	}

	[global::Java.Interop.JniTypeSignature ("java/lang/Comparable", GenerateJavaPeer=false)]
	internal partial class IComparableInvoker : global::Java.Lang.Object, IComparable {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_java_lang_Comparable; }
		}

		static readonly JniPeerMembers _members_java_lang_Comparable = new JniPeerMembers ("java/lang/Comparable", typeof (IComparableInvoker));

		public IComparableInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		public unsafe int CompareTo (global::Java.Lang.Object another)
		{
			const string __id = "compareTo.(Ljava/lang/Object;)I";
			var native_another = (another?.PeerReference ?? default);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_another);
				var __rm = _members_java_lang_Comparable.InstanceMethods.InvokeAbstractInt32Method (__id, this, __args);
				return __rm;
			} finally {
				global::System.GC.KeepAlive (another);
			}
		}

	}
}
