using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='ExtendPublicClass']"
	[global::Android.Runtime.Register ("xamarin/test/ExtendPublicClass", DoNotGenerateAcw=true)]
	public partial class ExtendPublicClass : global::Java.Lang.Object {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/ExtendPublicClass", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (ExtendPublicClass); }
		}

		protected ExtendPublicClass (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static IntPtr id_ctor;
		// Metadata.xml XPath constructor reference: path="/api/package[@name='xamarin.test']/class[@name='ExtendPublicClass']/constructor[@name='ExtendPublicClass' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe ExtendPublicClass ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				if (((object) this).GetType () != typeof (ExtendPublicClass)) {
					SetHandle (
							global::Android.Runtime.JNIEnv.StartCreateInstance (((object) this).GetType (), "()V"),
							JniHandleOwnership.TransferLocalRef);
					global::Android.Runtime.JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, "()V");
					return;
				}

				if (id_ctor == IntPtr.Zero)
					id_ctor = JNIEnv.GetMethodID (class_ref, "<init>", "()V");
				SetHandle (
						global::Android.Runtime.JNIEnv.StartCreateInstance (class_ref, id_ctor),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, class_ref, id_ctor);
			} finally {
			}
		}

		static Delegate cb_foo;
#pragma warning disable 0169
		static Delegate GetFooHandler ()
		{
			if (cb_foo == null)
				cb_foo = JNINativeWrapper.CreateDelegate ((_JniMarshal_PP_V) n_Foo);
			return cb_foo;
		}

		static void n_Foo (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.ExtendPublicClass> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Foo ();
		}
#pragma warning restore 0169

		static IntPtr id_foo;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='ExtendPublicClass']/method[@name='foo' and count(parameter)=0]"
		[Register ("foo", "()V", "GetFooHandler")]
		public virtual unsafe void Foo ()
		{
			if (id_foo == IntPtr.Zero)
				id_foo = JNIEnv.GetMethodID (class_ref, "foo", "()V");
			try {

				if (((object) this).GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_foo);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "foo", "()V"));
			} finally {
			}
		}

	}
}
