using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='AbsSpinner']"
	[global::Android.Runtime.Register ("xamarin/test/AbsSpinner", DoNotGenerateAcw=true)]
	public abstract partial class AbsSpinner : Xamarin.Test.AdapterView<Xamarin.Test.ISpinnerAdapter> {

		internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("xamarin/test/AbsSpinner", typeof (AbsSpinner));
		internal static new IntPtr class_ref {
			get {
				return _members.JniPeerType.PeerReference.Handle;
			}
		}

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override IntPtr ThresholdClass {
			get { return _members.JniPeerType.PeerReference.Handle; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
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
			global::Xamarin.Test.AbsSpinner __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.AbsSpinner> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
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
			global::Xamarin.Test.AbsSpinner __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.AbsSpinner> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Xamarin.Test.ISpinnerAdapter adapter = (global::Xamarin.Test.ISpinnerAdapter)global::Java.Lang.Object.GetObject<global::Xamarin.Test.ISpinnerAdapter> (native_adapter, JniHandleOwnership.DoNotTransfer);
			__this.Adapter = adapter;
		}
#pragma warning restore 0169

		public override unsafe global::Xamarin.Test.ISpinnerAdapter Adapter {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='AbsSpinner']/method[@name='getAdapter' and count(parameter)=0]"
			[Register ("getAdapter", "()Lxamarin/test/SpinnerAdapter;", "GetGetAdapterHandler")]
			get {
				const string __id = "getAdapter.()Lxamarin/test/SpinnerAdapter;";
				try {
					var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
					return global::Java.Lang.Object.GetObject<global::Xamarin.Test.ISpinnerAdapter> (__rm.Handle, JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='AbsSpinner']/method[@name='setAdapter' and count(parameter)=1 and parameter[1][@type='xamarin.test.SpinnerAdapter']]"
			[Register ("setAdapter", "(Lxamarin/test/SpinnerAdapter;)V", "GetSetAdapter_Lxamarin_test_SpinnerAdapter_Handler")]
			set {
				const string __id = "setAdapter.(Lxamarin/test/SpinnerAdapter;)V";
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue ((value == null) ? IntPtr.Zero : value.Handle);
					_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
				} finally {
				}
			}
		}

	}

	[global::Android.Runtime.Register ("xamarin/test/AbsSpinner", DoNotGenerateAcw=true)]
	internal partial class AbsSpinnerInvoker : AbsSpinner {

		public AbsSpinnerInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("xamarin/test/AbsSpinner", typeof (AbsSpinnerInvoker));

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

		protected override unsafe global::Java.Lang.Object RawAdapter {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='AdapterView']/method[@name='getAdapter' and count(parameter)=0]"
			[Register ("getAdapter", "()Lxamarin/test/Adapter;", "GetGetAdapterHandler")]
			get {
				const string __id = "getAdapter.()Lxamarin/test/Adapter;";
				try {
					var __rm = _members.InstanceMethods.InvokeAbstractObjectMethod (__id, this, null);
					return (Java.Lang.Object) global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (__rm.Handle, JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='AdapterView']/method[@name='setAdapter' and count(parameter)=1 and parameter[1][@type='T']]"
			[Register ("setAdapter", "(Lxamarin/test/Adapter;)V", "GetSetAdapter_Lxamarin_test_Adapter_Handler")]
			set {
				const string __id = "setAdapter.(Lxamarin/test/Adapter;)V";
				IntPtr native_value = JNIEnv.ToLocalJniHandle (value);
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (native_value);
					_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

	}

}
