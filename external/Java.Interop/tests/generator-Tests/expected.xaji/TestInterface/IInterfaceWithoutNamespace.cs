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
using Android.Runtime;
using Java.Interop;

// Metadata.xml XPath interface reference: path="/api/package[@name='']/interface[@name='InterfaceWithoutNamespace']"
[Register ("InterfaceWithoutNamespace", "", "IInterfaceWithoutNamespaceInvoker")]
public partial interface IInterfaceWithoutNamespace : IJavaObject, IJavaPeerable {
	// Metadata.xml XPath method reference: path="/api/package[@name='']/interface[@name='InterfaceWithoutNamespace']/method[@name='Foo' and count(parameter)=0]"
	[Register ("Foo", "()V", "GetFooHandler:IInterfaceWithoutNamespaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
	void Foo ();

}

[global::Android.Runtime.Register ("InterfaceWithoutNamespace", DoNotGenerateAcw=true)]
internal partial class IInterfaceWithoutNamespaceInvoker : global::Java.Lang.Object, IInterfaceWithoutNamespace {
	static IntPtr java_class_ref {
		get { return _members__InterfaceWithoutNamespace.JniPeerType.PeerReference.Handle; }
	}

	[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
	[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
	public override global::Java.Interop.JniPeerMembers JniPeerMembers {
		get { return _members__InterfaceWithoutNamespace; }
	}

	[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
	[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
	protected override IntPtr ThresholdClass {
		get { return _members__InterfaceWithoutNamespace.JniPeerType.PeerReference.Handle; }
	}

	[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
	[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
	protected override global::System.Type ThresholdType {
		get { return _members__InterfaceWithoutNamespace.ManagedPeerType; }
	}

	static readonly JniPeerMembers _members__InterfaceWithoutNamespace = new XAPeerMembers ("InterfaceWithoutNamespace", typeof (IInterfaceWithoutNamespaceInvoker));

	public IInterfaceWithoutNamespaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
	{
	}

	static Delegate cb_Foo_Foo_V;
#pragma warning disable 0169
	static Delegate GetFooHandler ()
	{
		return cb_Foo_Foo_V ??= new _JniMarshal_PP_V (n_Foo);
	}

	static void n_Foo (IntPtr jnienv, IntPtr native__this)
	{
		unsafe {
			global::Java.Interop.JniMarshal.SafeInvokeAction (jnienv, native__this, &__n_Foo);
		}
	}
	private static void __n_Foo (IntPtr jnienv, IntPtr native__this)
	{
		var __this = global::Java.Lang.Object.GetObject<IInterfaceWithoutNamespace> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
		__this.Foo ();
	}
#pragma warning restore 0169

	public unsafe void Foo ()
	{
		const string __id = "Foo.()V";
		try {
			_members__InterfaceWithoutNamespace.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
		} finally {
		}
	}

}
