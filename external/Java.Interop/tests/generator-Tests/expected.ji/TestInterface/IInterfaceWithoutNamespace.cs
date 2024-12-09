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
