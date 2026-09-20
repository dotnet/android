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

// Metadata.xml XPath interface reference: path="/api/package[@name='']/interface[@name='InterfaceWithoutNamespace']"
[global::Java.Interop.JniTypeSignature ("InterfaceWithoutNamespace", GenerateJavaPeer=false, InvokerType=typeof (IInterfaceWithoutNamespaceInvoker))]
public partial interface IInterfaceWithoutNamespace : IJavaPeerable {
	// Metadata.xml XPath method reference: path="/api/package[@name='']/interface[@name='InterfaceWithoutNamespace']/method[@name='Foo' and count(parameter)=0]"
	[global::Java.Interop.JniMethodSignature ("Foo", "()V")]
	void Foo ();

}

[global::Java.Interop.JniTypeSignature ("InterfaceWithoutNamespace", GenerateJavaPeer=false)]
internal partial class IInterfaceWithoutNamespaceInvoker : global::Java.Lang.Object, IInterfaceWithoutNamespace {
	[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
	[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
	public override global::Java.Interop.JniPeerMembers JniPeerMembers {
		get { return _members__InterfaceWithoutNamespace; }
	}

	static readonly JniPeerMembers _members__InterfaceWithoutNamespace = new JniPeerMembers ("InterfaceWithoutNamespace", typeof (IInterfaceWithoutNamespaceInvoker));

	public IInterfaceWithoutNamespaceInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
	{
	}

	public unsafe void Foo ()
	{
		const string __id = "Foo.()V";
		try {
			_members__InterfaceWithoutNamespace.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
		} finally {
		}
	}

}
