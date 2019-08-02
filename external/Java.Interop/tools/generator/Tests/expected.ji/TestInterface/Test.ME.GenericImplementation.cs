using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Test.ME {

	// Metadata.xml XPath class reference: path="/api/package[@name='test.me']/class[@name='GenericImplementation']"
	[global::Android.Runtime.Register ("test/me/GenericImplementation", DoNotGenerateAcw=true)]
	public partial class GenericImplementation : global::Java.Lang.Object, global::Test.ME.IGenericInterface {

		internal static new readonly JniPeerMembers _members = new JniPeerMembers ("test/me/GenericImplementation", typeof (GenericImplementation));
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

		protected GenericImplementation (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='test.me']/class[@name='GenericImplementation']/constructor[@name='GenericImplementation' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe GenericImplementation ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string __id = "()V";

			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), null);
				SetHandle (__r.Handle, JniHandleOwnership.TransferLocalRef);
				_members.InstanceMethods.FinishCreateInstance (__id, this, null);
			} finally {
			}
		}

		static Delegate cb_SetObject_arrayB;
#pragma warning disable 0169
		static Delegate GetSetObject_arrayBHandler ()
		{
			if (cb_SetObject_arrayB == null)
				cb_SetObject_arrayB = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_SetObject_arrayB);
			return cb_SetObject_arrayB;
		}

		static void n_SetObject_arrayB (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			global::Test.ME.GenericImplementation __this = global::Java.Lang.Object.GetObject<global::Test.ME.GenericImplementation> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			byte[] value = (byte[]) JNIEnv.GetArray (native_value, JniHandleOwnership.DoNotTransfer, typeof (byte));
			__this.SetObject (value);
			if (value != null)
				JNIEnv.CopyArray (value, native_value);
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='GenericImplementation']/method[@name='SetObject' and count(parameter)=1 and parameter[1][@type='byte[]']]"
		[Register ("SetObject", "([B)V", "GetSetObject_arrayBHandler")]
		public virtual unsafe void SetObject (byte[] value)
		{
			const string __id = "SetObject.([B)V";
			IntPtr native_value = JNIEnv.NewArray (value);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_value);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
			} finally {
				if (value != null) {
					JNIEnv.CopyArray (native_value, value);
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		// This method is explicitly implemented as a member of an instantiated Test.ME.IGenericInterface
		void global::Test.ME.IGenericInterface.SetObject (global::Java.Lang.Object value)
		{
			SetObject (value.ToArray<byte> ());
		}

	}
}
