using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Java.IO {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.io']/class[@name='FilterOutputStream']"
	[global::Android.Runtime.Register ("java/io/FilterOutputStream", DoNotGenerateAcw=true)]
	public partial class FilterOutputStream : global::Java.IO.OutputStream {

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("java/io/FilterOutputStream", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (FilterOutputStream); }
		}

		protected FilterOutputStream (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static IntPtr id_ctor_Ljava_io_OutputStream_;
		// Metadata.xml XPath constructor reference: path="/api/package[@name='java.io']/class[@name='FilterOutputStream']/constructor[@name='FilterOutputStream' and count(parameter)=1 and parameter[1][@type='java.io.OutputStream']]"
		[Register (".ctor", "(Ljava/io/OutputStream;)V", "")]
		public unsafe FilterOutputStream (global::System.IO.Stream @out)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			IntPtr native__out = global::Android.Runtime.OutputStreamAdapter.ToLocalJniHandle (@out);
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native__out);
				if (GetType () != typeof (FilterOutputStream)) {
					SetHandle (
							global::Android.Runtime.JNIEnv.StartCreateInstance (GetType (), "(Ljava/io/OutputStream;)V", __args),
							JniHandleOwnership.TransferLocalRef);
					global::Android.Runtime.JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, "(Ljava/io/OutputStream;)V", __args);
					return;
				}

				if (id_ctor_Ljava_io_OutputStream_ == IntPtr.Zero)
					id_ctor_Ljava_io_OutputStream_ = JNIEnv.GetMethodID (class_ref, "<init>", "(Ljava/io/OutputStream;)V");
				SetHandle (
						global::Android.Runtime.JNIEnv.StartCreateInstance (class_ref, id_ctor_Ljava_io_OutputStream_, __args),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, class_ref, id_ctor_Ljava_io_OutputStream_, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native__out);
			}
		}

		static Delegate cb_write_I;
#pragma warning disable 0169
		static Delegate GetWrite_IHandler ()
		{
			if (cb_write_I == null)
				cb_write_I = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, int>) n_Write_I);
			return cb_write_I;
		}

		static void n_Write_I (IntPtr jnienv, IntPtr native__this, int oneByte)
		{
			global::Java.IO.FilterOutputStream __this = global::Java.Lang.Object.GetObject<global::Java.IO.FilterOutputStream> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Write (oneByte);
		}
#pragma warning restore 0169

		static IntPtr id_write_I;
		// Metadata.xml XPath method reference: path="/api/package[@name='java.io']/class[@name='FilterOutputStream']/method[@name='write' and count(parameter)=1 and parameter[1][@type='int']]"
		[Register ("write", "(I)V", "GetWrite_IHandler")]
		public override unsafe void Write (int oneByte)
		{
			if (id_write_I == IntPtr.Zero)
				id_write_I = JNIEnv.GetMethodID (class_ref, "write", "(I)V");
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (oneByte);

				if (GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_write_I, __args);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "write", "(I)V"), __args);
			} finally {
			}
		}

	}
}
