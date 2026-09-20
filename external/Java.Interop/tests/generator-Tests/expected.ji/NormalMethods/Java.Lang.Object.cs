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

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='Object']"
	[global::Java.Interop.JniTypeSignature ("java/lang/Object", GenerateJavaPeer=false)]
	public partial class Object {
		static readonly JniPeerMembers _members = new JniPeerMembers ("java/lang/Object", typeof (Object));

	}
}
