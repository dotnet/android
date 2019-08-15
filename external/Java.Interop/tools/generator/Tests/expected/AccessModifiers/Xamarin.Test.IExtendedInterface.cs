using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath interface reference: path="/api/package[@name='xamarin.test']/interface[@name='ExtendedInterface']"
	[Register ("xamarin/test/ExtendedInterface", "", "Xamarin.Test.IExtendedInterfaceInvoker")]
	public partial interface IExtendedInterface : IJavaObject {

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/interface[@name='ExtendedInterface']/method[@name='extendedMethod' and count(parameter)=0]"
		[Register ("extendedMethod", "()V", "GetExtendedMethodHandler:Xamarin.Test.IExtendedInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		void ExtendedMethod ();

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/interface[@name='BaseInterface']/method[@name='baseMethod' and count(parameter)=0]"
		[Register ("baseMethod", "()V", "GetBaseMethodHandler:Xamarin.Test.IExtendedInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		void BaseMethod ();

	}

	[global::Android.Runtime.Register ("xamarin/test/ExtendedInterface", DoNotGenerateAcw=true)]
	internal partial class IExtendedInterfaceInvoker : global::Java.Lang.Object, IExtendedInterface {

		static IntPtr java_class_ref = JNIEnv.FindClass ("xamarin/test/ExtendedInterface");

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (IExtendedInterfaceInvoker); }
		}

		new IntPtr class_ref;

		public static IExtendedInterface GetObject (IntPtr handle, JniHandleOwnership transfer)
		{
			return global::Java.Lang.Object.GetObject<IExtendedInterface> (handle, transfer);
		}

		static IntPtr Validate (IntPtr handle)
		{
			if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
				throw new InvalidCastException (string.Format ("Unable to convert instance of type '{0}' to type '{1}'.",
							JNIEnv.GetClassNameFromInstance (handle), "xamarin.test.ExtendedInterface"));
			return handle;
		}

		protected override void Dispose (bool disposing)
		{
			if (this.class_ref != IntPtr.Zero)
				JNIEnv.DeleteGlobalRef (this.class_ref);
			this.class_ref = IntPtr.Zero;
			base.Dispose (disposing);
		}

		public IExtendedInterfaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
		{
			IntPtr local_ref = JNIEnv.GetObjectClass (((global::Java.Lang.Object) this).Handle);
			this.class_ref = JNIEnv.NewGlobalRef (local_ref);
			JNIEnv.DeleteLocalRef (local_ref);
		}

		static Delegate cb_extendedMethod;
#pragma warning disable 0169
		static Delegate GetExtendedMethodHandler ()
		{
			if (cb_extendedMethod == null)
				cb_extendedMethod = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr>) n_ExtendedMethod);
			return cb_extendedMethod;
		}

		static void n_ExtendedMethod (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.IExtendedInterface __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.IExtendedInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.ExtendedMethod ();
		}
#pragma warning restore 0169

		IntPtr id_extendedMethod;
		public unsafe void ExtendedMethod ()
		{
			if (id_extendedMethod == IntPtr.Zero)
				id_extendedMethod = JNIEnv.GetMethodID (class_ref, "extendedMethod", "()V");
			JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_extendedMethod);
		}

		static Delegate cb_baseMethod;
#pragma warning disable 0169
		static Delegate GetBaseMethodHandler ()
		{
			if (cb_baseMethod == null)
				cb_baseMethod = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr>) n_BaseMethod);
			return cb_baseMethod;
		}

		static void n_BaseMethod (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.IExtendedInterface __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.IExtendedInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.BaseMethod ();
		}
#pragma warning restore 0169

		IntPtr id_baseMethod;
		public unsafe void BaseMethod ()
		{
			if (id_baseMethod == IntPtr.Zero)
				id_baseMethod = JNIEnv.GetMethodID (class_ref, "baseMethod", "()V");
			JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_baseMethod);
		}

	}

}
