using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='NotificationCompatBase']"
	[global::Android.Runtime.Register ("xamarin/test/NotificationCompatBase", DoNotGenerateAcw=true)]
	public partial class NotificationCompatBase : global::Java.Lang.Object {

		// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='NotificationCompatBase.Action']"
		[global::Android.Runtime.Register ("xamarin/test/NotificationCompatBase$Action", DoNotGenerateAcw=true)]
		public abstract partial class Action : global::Java.Lang.Object {

			// Metadata.xml XPath interface reference: path="/api/package[@name='xamarin.test']/interface[@name='NotificationCompatBase.Action.Factory']"
			[Register ("xamarin/test/NotificationCompatBase$Action$Factory", "", "Xamarin.Test.NotificationCompatBase/Action/IFactoryInvoker")]
			public partial interface IFactory : IJavaObject {

				// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/interface[@name='NotificationCompatBase.Action.Factory']/method[@name='build' and count(parameter)=1 and parameter[1][@type='int']]"
				[Register ("build", "(I)Lxamarin/test/NotificationCompatBase$Action;", "GetBuild_IHandler:Xamarin.Test.NotificationCompatBase/Action/IFactoryInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
				global::Xamarin.Test.NotificationCompatBase.Action Build (int p0);

			}

			[global::Android.Runtime.Register ("xamarin/test/NotificationCompatBase$Action$Factory", DoNotGenerateAcw=true)]
			internal class IFactoryInvoker : global::Java.Lang.Object, IFactory {

				static IntPtr java_class_ref = JNIEnv.FindClass ("xamarin/test/NotificationCompatBase$Action$Factory");

				protected override IntPtr ThresholdClass {
					get { return class_ref; }
				}

				protected override global::System.Type ThresholdType {
					get { return typeof (IFactoryInvoker); }
				}

				IntPtr class_ref;

				public static IFactory GetObject (IntPtr handle, JniHandleOwnership transfer)
				{
					return global::Java.Lang.Object.GetObject<IFactory> (handle, transfer);
				}

				static IntPtr Validate (IntPtr handle)
				{
					if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
						throw new InvalidCastException (string.Format ("Unable to convert instance of type '{0}' to type '{1}'.",
									JNIEnv.GetClassNameFromInstance (handle), "xamarin.test.NotificationCompatBase.Action.Factory"));
					return handle;
				}

				protected override void Dispose (bool disposing)
				{
					if (this.class_ref != IntPtr.Zero)
						JNIEnv.DeleteGlobalRef (this.class_ref);
					this.class_ref = IntPtr.Zero;
					base.Dispose (disposing);
				}

				public IFactoryInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
				{
					IntPtr local_ref = JNIEnv.GetObjectClass (((global::Java.Lang.Object) this).Handle);
					this.class_ref = JNIEnv.NewGlobalRef (local_ref);
					JNIEnv.DeleteLocalRef (local_ref);
				}

				static Delegate cb_build_I;
#pragma warning disable 0169
				static Delegate GetBuild_IHandler ()
				{
					if (cb_build_I == null)
						cb_build_I = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, int, IntPtr>) n_Build_I);
					return cb_build_I;
				}

				static IntPtr n_Build_I (IntPtr jnienv, IntPtr native__this, int p0)
				{
					global::Xamarin.Test.NotificationCompatBase.Action.IFactory __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.NotificationCompatBase.Action.IFactory> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
					return JNIEnv.ToLocalJniHandle (__this.Build (p0));
				}
#pragma warning restore 0169

				IntPtr id_build_I;
				public unsafe global::Xamarin.Test.NotificationCompatBase.Action Build (int p0)
				{
					if (id_build_I == IntPtr.Zero)
						id_build_I = JNIEnv.GetMethodID (class_ref, "build", "(I)Lxamarin/test/NotificationCompatBase$Action;");
					JValue* __args = stackalloc JValue [1];
					__args [0] = new JValue (p0);
					return global::Java.Lang.Object.GetObject<global::Xamarin.Test.NotificationCompatBase.Action> (JNIEnv.CallObjectMethod (((global::Java.Lang.Object) this).Handle, id_build_I, __args), JniHandleOwnership.TransferLocalRef);
				}

			}


			internal static new IntPtr java_class_handle;
			internal static new IntPtr class_ref {
				get {
					return JNIEnv.FindClass ("xamarin/test/NotificationCompatBase$Action", ref java_class_handle);
				}
			}

			protected override IntPtr ThresholdClass {
				get { return class_ref; }
			}

			protected override global::System.Type ThresholdType {
				get { return typeof (Action); }
			}

			protected Action (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		}

		[global::Android.Runtime.Register ("xamarin/test/NotificationCompatBase$Action", DoNotGenerateAcw=true)]
		internal partial class ActionInvoker : Action {

			public ActionInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

			protected override global::System.Type ThresholdType {
				get { return typeof (ActionInvoker); }
			}

		}


		// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='NotificationCompatBase.InstanceInner']"
		[global::Android.Runtime.Register ("xamarin/test/NotificationCompatBase$InstanceInner", DoNotGenerateAcw=true)]
		public abstract partial class InstanceInner : global::Java.Lang.Object {

			internal static new IntPtr java_class_handle;
			internal static new IntPtr class_ref {
				get {
					return JNIEnv.FindClass ("xamarin/test/NotificationCompatBase$InstanceInner", ref java_class_handle);
				}
			}

			protected override IntPtr ThresholdClass {
				get { return class_ref; }
			}

			protected override global::System.Type ThresholdType {
				get { return typeof (InstanceInner); }
			}

			protected InstanceInner (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

			static IntPtr id_ctor_Lxamarin_test_NotificationCompatBase_;
			// Metadata.xml XPath constructor reference: path="/api/package[@name='xamarin.test']/class[@name='NotificationCompatBase.InstanceInner']/constructor[@name='NotificationCompatBase.InstanceInner' and count(parameter)=1 and parameter[1][@type='xamarin.test.NotificationCompatBase']]"
			[Register (".ctor", "(Lxamarin/test/NotificationCompatBase;)V", "")]
			public unsafe InstanceInner (global::Xamarin.Test.NotificationCompatBase __self)
				: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
			{
				if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
					return;

				try {
					JValue* __args = stackalloc JValue [1];
					__args [0] = new JValue (__self);
					if (((object) this).GetType () != typeof (InstanceInner)) {
						SetHandle (
								global::Android.Runtime.JNIEnv.StartCreateInstance (((object) this).GetType (), "(L" + global::Android.Runtime.JNIEnv.GetJniName (GetType ().DeclaringType) + ";)V", __args),
								JniHandleOwnership.TransferLocalRef);
						global::Android.Runtime.JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, "(L" + global::Android.Runtime.JNIEnv.GetJniName (GetType ().DeclaringType) + ";)V", __args);
						return;
					}

					if (id_ctor_Lxamarin_test_NotificationCompatBase_ == IntPtr.Zero)
						id_ctor_Lxamarin_test_NotificationCompatBase_ = JNIEnv.GetMethodID (class_ref, "<init>", "(Lxamarin/test/NotificationCompatBase;)V");
					SetHandle (
							global::Android.Runtime.JNIEnv.StartCreateInstance (class_ref, id_ctor_Lxamarin_test_NotificationCompatBase_, __args),
							JniHandleOwnership.TransferLocalRef);
					JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, class_ref, id_ctor_Lxamarin_test_NotificationCompatBase_, __args);
				} finally {
				}
			}

		}

		[global::Android.Runtime.Register ("xamarin/test/NotificationCompatBase$InstanceInner", DoNotGenerateAcw=true)]
		internal partial class InstanceInnerInvoker : InstanceInner {

			public InstanceInnerInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

			protected override global::System.Type ThresholdType {
				get { return typeof (InstanceInnerInvoker); }
			}

		}


		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/NotificationCompatBase", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (NotificationCompatBase); }
		}

		protected NotificationCompatBase (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

	}
}
