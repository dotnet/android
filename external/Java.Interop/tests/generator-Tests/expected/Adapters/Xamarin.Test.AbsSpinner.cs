using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='AbsSpinner']"
	[global::Android.Runtime.Register ("xamarin/test/AbsSpinner", DoNotGenerateAcw=true)]
	public abstract partial class AbsSpinner : Xamarin.Test.AdapterView<Xamarin.Test.ISpinnerAdapter> {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/AbsSpinner", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (AbsSpinner); }
		}

		protected AbsSpinner (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static Delegate cb_getAdapter;
#pragma warning disable 0169
		static Delegate GetGetAdapterHandler ()
		{
			if (cb_getAdapter == null)
				cb_getAdapter = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr>) n_GetAdapter);
			return cb_getAdapter;
		}

		static IntPtr n_GetAdapter (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.AbsSpinner> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToLocalJniHandle (__this.Adapter);
		}
#pragma warning restore 0169

		static Delegate cb_setAdapter_Lxamarin_test_SpinnerAdapter_;
#pragma warning disable 0169
		static Delegate GetSetAdapter_Lxamarin_test_SpinnerAdapter_Handler ()
		{
			if (cb_setAdapter_Lxamarin_test_SpinnerAdapter_ == null)
				cb_setAdapter_Lxamarin_test_SpinnerAdapter_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_SetAdapter_Lxamarin_test_SpinnerAdapter_);
			return cb_setAdapter_Lxamarin_test_SpinnerAdapter_;
		}

		static void n_SetAdapter_Lxamarin_test_SpinnerAdapter_ (IntPtr jnienv, IntPtr native__this, IntPtr native_adapter)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.AbsSpinner> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var adapter = (global::Xamarin.Test.ISpinnerAdapter)global::Java.Lang.Object.GetObject<global::Xamarin.Test.ISpinnerAdapter> (native_adapter, JniHandleOwnership.DoNotTransfer);
			__this.Adapter = adapter;
		}
#pragma warning restore 0169

		static IntPtr id_getAdapter;
		static IntPtr id_setAdapter_Lxamarin_test_SpinnerAdapter_;
		public override unsafe global::Xamarin.Test.ISpinnerAdapter Adapter {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='AbsSpinner']/method[@name='getAdapter' and count(parameter)=0]"
			[Register ("getAdapter", "()Lxamarin/test/SpinnerAdapter;", "GetGetAdapterHandler")]
			get {
				if (id_getAdapter == IntPtr.Zero)
					id_getAdapter = JNIEnv.GetMethodID (class_ref, "getAdapter", "()Lxamarin/test/SpinnerAdapter;");
				try {

					if (((object) this).GetType () == ThresholdType)
						return global::Java.Lang.Object.GetObject<global::Xamarin.Test.ISpinnerAdapter> (JNIEnv.CallObjectMethod (((global::Java.Lang.Object) this).Handle, id_getAdapter), JniHandleOwnership.TransferLocalRef);
					else
						return global::Java.Lang.Object.GetObject<global::Xamarin.Test.ISpinnerAdapter> (JNIEnv.CallNonvirtualObjectMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "getAdapter", "()Lxamarin/test/SpinnerAdapter;")), JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='AbsSpinner']/method[@name='setAdapter' and count(parameter)=1 and parameter[1][@type='xamarin.test.SpinnerAdapter']]"
			[Register ("setAdapter", "(Lxamarin/test/SpinnerAdapter;)V", "GetSetAdapter_Lxamarin_test_SpinnerAdapter_Handler")]
			set {
				if (id_setAdapter_Lxamarin_test_SpinnerAdapter_ == IntPtr.Zero)
					id_setAdapter_Lxamarin_test_SpinnerAdapter_ = JNIEnv.GetMethodID (class_ref, "setAdapter", "(Lxamarin/test/SpinnerAdapter;)V");
				try {
					JValue* __args = stackalloc JValue [1];
					__args [0] = new JValue (value);

					if (((object) this).GetType () == ThresholdType)
						JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_setAdapter_Lxamarin_test_SpinnerAdapter_, __args);
					else
						JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "setAdapter", "(Lxamarin/test/SpinnerAdapter;)V"), __args);
				} finally {
				}
			}
		}

	}

	[global::Android.Runtime.Register ("xamarin/test/AbsSpinner", DoNotGenerateAcw=true)]
	internal partial class AbsSpinnerInvoker : AbsSpinner {

		public AbsSpinnerInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		protected override global::System.Type ThresholdType {
			get { return typeof (AbsSpinnerInvoker); }
		}

		static IntPtr id_getAdapter;
		static IntPtr id_setAdapter_Lxamarin_test_Adapter_;
		protected override unsafe global::Java.Lang.Object RawAdapter {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='AdapterView']/method[@name='getAdapter' and count(parameter)=0]"
			[Register ("getAdapter", "()Lxamarin/test/Adapter;", "GetGetAdapterHandler")]
			get {
				if (id_getAdapter == IntPtr.Zero)
					id_getAdapter = JNIEnv.GetMethodID (class_ref, "getAdapter", "()Lxamarin/test/Adapter;");
				try {
					return (Java.Lang.Object) global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (JNIEnv.CallObjectMethod (((global::Java.Lang.Object) this).Handle, id_getAdapter), JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='AdapterView']/method[@name='setAdapter' and count(parameter)=1 and parameter[1][@type='T']]"
			[Register ("setAdapter", "(Lxamarin/test/Adapter;)V", "GetSetAdapter_Lxamarin_test_Adapter_Handler")]
			set {
				if (id_setAdapter_Lxamarin_test_Adapter_ == IntPtr.Zero)
					id_setAdapter_Lxamarin_test_Adapter_ = JNIEnv.GetMethodID (class_ref, "setAdapter", "(Lxamarin/test/Adapter;)V");
				IntPtr native_value = JNIEnv.ToLocalJniHandle (value);
				try {
					JValue* __args = stackalloc JValue [1];
					__args [0] = new JValue (native_value);
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_setAdapter_Lxamarin_test_Adapter_, __args);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

	}

}
