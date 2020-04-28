using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='PublicClass']"
	[global::Android.Runtime.Register ("xamarin/test/PublicClass", DoNotGenerateAcw=true)]
	public partial class PublicClass : global::Java.Lang.Object {

		// Metadata.xml XPath interface reference: path="/api/package[@name='xamarin.test']/interface[@name='PublicClass.ProtectedInterface']"
		[Register ("xamarin/test/PublicClass$ProtectedInterface", "", "Xamarin.Test.PublicClass/IProtectedInterfaceInvoker")]
		protected internal partial interface IProtectedInterface : IJavaObject {

			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/interface[@name='PublicClass.ProtectedInterface']/method[@name='foo' and count(parameter)=0]"
			[Register ("foo", "()V", "GetFooHandler:Xamarin.Test.PublicClass/IProtectedInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
			void Foo ();

		}

		[global::Android.Runtime.Register ("xamarin/test/PublicClass$ProtectedInterface", DoNotGenerateAcw=true)]
		internal partial class IProtectedInterfaceInvoker : global::Java.Lang.Object, IProtectedInterface {

			static IntPtr java_class_ref = JNIEnv.FindClass ("xamarin/test/PublicClass$ProtectedInterface");

			protected override IntPtr ThresholdClass {
				get { return class_ref; }
			}

			protected override global::System.Type ThresholdType {
				get { return typeof (IProtectedInterfaceInvoker); }
			}

			new IntPtr class_ref;

			public static IProtectedInterface GetObject (IntPtr handle, JniHandleOwnership transfer)
			{
				return global::Java.Lang.Object.GetObject<IProtectedInterface> (handle, transfer);
			}

			static IntPtr Validate (IntPtr handle)
			{
				if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
					throw new InvalidCastException (string.Format ("Unable to convert instance of type '{0}' to type '{1}'.",
								JNIEnv.GetClassNameFromInstance (handle), "xamarin.test.PublicClass.ProtectedInterface"));
				return handle;
			}

			protected override void Dispose (bool disposing)
			{
				if (this.class_ref != IntPtr.Zero)
					JNIEnv.DeleteGlobalRef (this.class_ref);
				this.class_ref = IntPtr.Zero;
				base.Dispose (disposing);
			}

			public IProtectedInterfaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
			{
				IntPtr local_ref = JNIEnv.GetObjectClass (((global::Java.Lang.Object) this).Handle);
				this.class_ref = JNIEnv.NewGlobalRef (local_ref);
				JNIEnv.DeleteLocalRef (local_ref);
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
				var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.PublicClass.IProtectedInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				__this.Foo ();
			}
#pragma warning restore 0169

			IntPtr id_foo;
			public unsafe void Foo ()
			{
				if (id_foo == IntPtr.Zero)
					id_foo = JNIEnv.GetMethodID (class_ref, "foo", "()V");
				JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_foo);
			}

		}


		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/PublicClass", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (PublicClass); }
		}

		protected PublicClass (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static IntPtr id_ctor;
		// Metadata.xml XPath constructor reference: path="/api/package[@name='xamarin.test']/class[@name='PublicClass']/constructor[@name='PublicClass' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe PublicClass ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				if (((object) this).GetType () != typeof (PublicClass)) {
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
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.PublicClass> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Foo ();
		}
#pragma warning restore 0169

		static IntPtr id_foo;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='PublicClass']/method[@name='foo' and count(parameter)=0]"
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
