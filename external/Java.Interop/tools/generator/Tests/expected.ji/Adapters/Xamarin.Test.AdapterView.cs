using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='AdapterView']"
	[global::Android.Runtime.Register ("xamarin/test/AdapterView", DoNotGenerateAcw=true)]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T extends xamarin.test.Adapter"})]
	public abstract partial class AdapterView : global::Java.Lang.Object {

		internal            static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("xamarin/test/AdapterView", typeof (AdapterView));
		internal static IntPtr class_ref {
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

		protected AdapterView (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

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
			global::Xamarin.Test.AdapterView __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.AdapterView> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToLocalJniHandle (__this.RawAdapter);
		}
#pragma warning restore 0169

		static Delegate cb_setAdapter_Lxamarin_test_Adapter_;
#pragma warning disable 0169
		static Delegate GetSetAdapter_Lxamarin_test_Adapter_Handler ()
		{
			if (cb_setAdapter_Lxamarin_test_Adapter_ == null)
				cb_setAdapter_Lxamarin_test_Adapter_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_SetAdapter_Lxamarin_test_Adapter_);
			return cb_setAdapter_Lxamarin_test_Adapter_;
		}

		static void n_SetAdapter_Lxamarin_test_Adapter_ (IntPtr jnienv, IntPtr native__this, IntPtr native_adapter)
		{
			global::Xamarin.Test.AdapterView __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.AdapterView> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Java.Lang.Object adapter = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_adapter, JniHandleOwnership.DoNotTransfer);
			__this.RawAdapter = adapter;
		}
#pragma warning restore 0169

		protected abstract global::Java.Lang.Object RawAdapter {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='AdapterView']/method[@name='getAdapter' and count(parameter)=0]"
			[Register ("getAdapter", "()Lxamarin/test/Adapter;", "GetGetAdapterHandler")] get;
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='AdapterView']/method[@name='setAdapter' and count(parameter)=1 and parameter[1][@type='T']]"
			[Register ("setAdapter", "(Lxamarin/test/Adapter;)V", "GetSetAdapter_Lxamarin_test_Adapter_Handler")] set;
		}

	}

	[global::Android.Runtime.Register ("xamarin/test/AdapterView", DoNotGenerateAcw=true)]
	internal partial class AdapterViewInvoker : AdapterView {

		public AdapterViewInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("xamarin/test/AdapterView", typeof (AdapterViewInvoker));

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
