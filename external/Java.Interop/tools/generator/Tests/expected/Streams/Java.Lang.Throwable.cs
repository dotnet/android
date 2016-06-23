using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='Throwable']"
	[global::Android.Runtime.Register ("java/lang/Throwable", DoNotGenerateAcw=true)]
	public partial class Throwable  {

		internal static IntPtr java_class_handle;
		internal static IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("java/lang/Throwable", ref java_class_handle);
			}
		}

		static Delegate cb_getMessage;
#pragma warning disable 0169
		static Delegate GetGetMessageHandler ()
		{
			if (cb_getMessage == null)
				cb_getMessage = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr>) n_GetMessage);
			return cb_getMessage;
		}

		static IntPtr n_GetMessage (IntPtr jnienv, IntPtr native__this)
		{
			global::Java.Lang.Throwable __this = global::Java.Lang.Object.GetObject<global::Java.Lang.Throwable> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.NewString (__this.Message);
		}
#pragma warning restore 0169

		static IntPtr id_getMessage;
		public new virtual unsafe string Message {
			// Metadata.xml XPath method reference: path="/api/package[@name='java.lang']/class[@name='Throwable']/method[@name='getMessage' and count(parameter)=0]"
			[Register ("getMessage", "()Ljava/lang/String;", "GetGetMessageHandler")]
			get {
				if (id_getMessage == IntPtr.Zero)
					id_getMessage = JNIEnv.GetMethodID (class_ref, "getMessage", "()Ljava/lang/String;");
				try {

					if (GetType () == ThresholdType)
						return JNIEnv.GetString (JNIEnv.CallObjectMethod (((global::Java.Lang.Throwable) this).Handle, id_getMessage), JniHandleOwnership.TransferLocalRef);
					else
						return JNIEnv.GetString (JNIEnv.CallNonvirtualObjectMethod (((global::Java.Lang.Throwable) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "getMessage", "()Ljava/lang/String;")), JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
		}

	}
}
