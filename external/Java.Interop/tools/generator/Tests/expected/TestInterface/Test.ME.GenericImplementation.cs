using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Test.ME {

	// Metadata.xml XPath class reference: path="/api/package[@name='test.me']/class[@name='GenericImplementation']"
	[global::Android.Runtime.Register ("test/me/GenericImplementation", DoNotGenerateAcw=true)]
	public partial class GenericImplementation : global::Java.Lang.Object, global::Test.ME.IGenericInterface {

		internal static IntPtr java_class_handle;
		internal static IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("test/me/GenericImplementation", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (GenericImplementation); }
		}

		protected GenericImplementation (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static IntPtr id_ctor;
		// Metadata.xml XPath constructor reference: path="/api/package[@name='test.me']/class[@name='GenericImplementation']/constructor[@name='GenericImplementation' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe GenericImplementation ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				if (GetType () != typeof (GenericImplementation)) {
					SetHandle (
							global::Android.Runtime.JNIEnv.StartCreateInstance (GetType (), "()V"),
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

		static IntPtr id_SetObject_arrayB;
		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='GenericImplementation']/method[@name='SetObject' and count(parameter)=1 and parameter[1][@type='byte[]']]"
		[Register ("SetObject", "([B)V", "GetSetObject_arrayBHandler")]
		public virtual unsafe void SetObject (byte[] value)
		{
			if (id_SetObject_arrayB == IntPtr.Zero)
				id_SetObject_arrayB = JNIEnv.GetMethodID (class_ref, "SetObject", "([B)V");
			IntPtr native_value = JNIEnv.NewArray (value);
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native_value);

				if (GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_SetObject_arrayB, __args);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "SetObject", "([B)V"), __args);
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
