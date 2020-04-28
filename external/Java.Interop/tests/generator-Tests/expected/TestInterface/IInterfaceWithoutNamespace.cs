using System;
using System.Collections.Generic;
using Android.Runtime;

// Metadata.xml XPath interface reference: path="/api/package[@name='']/interface[@name='InterfaceWithoutNamespace']"
[Register ("InterfaceWithoutNamespace", "", "IInterfaceWithoutNamespaceInvoker")]
public partial interface IInterfaceWithoutNamespace : IJavaObject {

	// Metadata.xml XPath method reference: path="/api/package[@name='']/interface[@name='InterfaceWithoutNamespace']/method[@name='Foo' and count(parameter)=0]"
	[Register ("Foo", "()V", "GetFooHandler:IInterfaceWithoutNamespaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
	void Foo ();

}

[global::Android.Runtime.Register ("InterfaceWithoutNamespace", DoNotGenerateAcw=true)]
internal partial class IInterfaceWithoutNamespaceInvoker : global::Java.Lang.Object, IInterfaceWithoutNamespace {

	static IntPtr java_class_ref = JNIEnv.FindClass ("InterfaceWithoutNamespace");

	protected override IntPtr ThresholdClass {
		get { return class_ref; }
	}

	protected override global::System.Type ThresholdType {
		get { return typeof (IInterfaceWithoutNamespaceInvoker); }
	}

	new IntPtr class_ref;

	public static IInterfaceWithoutNamespace GetObject (IntPtr handle, JniHandleOwnership transfer)
	{
		return global::Java.Lang.Object.GetObject<IInterfaceWithoutNamespace> (handle, transfer);
	}

	static IntPtr Validate (IntPtr handle)
	{
		if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
			throw new InvalidCastException (string.Format ("Unable to convert instance of type '{0}' to type '{1}'.",
						JNIEnv.GetClassNameFromInstance (handle), "InterfaceWithoutNamespace"));
		return handle;
	}

	protected override void Dispose (bool disposing)
	{
		if (this.class_ref != IntPtr.Zero)
			JNIEnv.DeleteGlobalRef (this.class_ref);
		this.class_ref = IntPtr.Zero;
		base.Dispose (disposing);
	}

	public IInterfaceWithoutNamespaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
	{
		IntPtr local_ref = JNIEnv.GetObjectClass (((global::Java.Lang.Object) this).Handle);
		this.class_ref = JNIEnv.NewGlobalRef (local_ref);
		JNIEnv.DeleteLocalRef (local_ref);
	}

	static Delegate cb_Foo;
#pragma warning disable 0169
	static Delegate GetFooHandler ()
	{
		if (cb_Foo == null)
			cb_Foo = JNINativeWrapper.CreateDelegate ((_JniMarshal_PP_V) n_Foo);
		return cb_Foo;
	}

	static void n_Foo (IntPtr jnienv, IntPtr native__this)
	{
		var __this = global::Java.Lang.Object.GetObject<IInterfaceWithoutNamespace> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
		__this.Foo ();
	}
#pragma warning restore 0169

	IntPtr id_Foo;
	public unsafe void Foo ()
	{
		if (id_Foo == IntPtr.Zero)
			id_Foo = JNIEnv.GetMethodID (class_ref, "Foo", "()V");
		JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_Foo);
	}

}

