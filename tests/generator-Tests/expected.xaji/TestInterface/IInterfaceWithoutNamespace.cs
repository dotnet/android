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
	[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
	[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
	public override global::Java.Interop.JniPeerMembers JniPeerMembers {
		get { return _members__InterfaceWithoutNamespace; }
	}

	static readonly JniPeerMembers _members__InterfaceWithoutNamespace = new JniPeerMembers ("InterfaceWithoutNamespace", typeof (IInterfaceWithoutNamespaceInvoker));

	public IInterfaceWithoutNamespaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
	{
	}

	static Delegate cb_Foo_Foo_V;
#pragma warning disable 0169
	static Delegate GetFooHandler ()
	{
		unsafe {
			// SAFETY: The delegate matches the registered callback signature; JNI, not this connector, supplies its arguments.
			return cb_Foo_Foo_V ??= new _JniMarshal_PP_V (n_Foo);
		}
	}

	/// <safety>
	/// Use the registered JNI ABI on jnienv's attached thread. Receiver/reference arguments
	/// must match the registered types and stay live without concurrent deletion for this call;
	/// permitted nulls stay null. JNI retains borrowed ownership. The helper reports managed
	/// failures and may return default; invalid addresses are not validated.
	/// </safety>
	static unsafe void n_Foo (IntPtr jnienv, IntPtr native__this)
	{
		unsafe {
			// SAFETY: JNI supplies this thread's environment and live typed references; the static target pointer is synchronous.
			global::Java.Interop.JniMarshal.SafeInvokeAction (jnienv, native__this, &__n_Foo);
		}
	}
	/// <safety>
	/// Requires the entry point's live, type-correct borrowed references in its active JNI
	/// frame/thread, without concurrent deletion. Copy-back occurs on normal return only;
	/// failures propagate to the marshal helper.
	/// </safety>
	private static unsafe void __n_Foo (IntPtr jnienv, IntPtr native__this)
	{
		unsafe {
			// SAFETY: The marshal frame keeps typed JNI references live on this thread through conversion and normal-return copy-back.
			var __this = global::Java.Lang.Object.GetObject<IInterfaceWithoutNamespace> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Foo ();
		}
	}
#pragma warning restore 0169

	public void Foo ()
	{
		const string __id = "Foo.()V";
		unsafe {
			// SAFETY: __id fixes argument types/count; initialized stack arguments and JNI locals stay on this thread for the synchronous call.
			_members__InterfaceWithoutNamespace.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
		}
	}

}
