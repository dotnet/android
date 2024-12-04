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

	[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
	static void n_Foo (IntPtr jnienv, IntPtr native__this)
	{
		if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
			return;

		try {
			var __this = global::Java.Lang.Object.GetObject<IInterfaceWithoutNamespace> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Foo ();
		} catch (global::System.Exception __e) {
			__r.OnUserUnhandledException (ref __envp, __e);
		} finally {
			global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
		}
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
