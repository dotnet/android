using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Android.Runtime;

using Java.Interop;

namespace Android.OS {

	[global::System.Runtime.Versioning.ObsoletedOSPlatform ("android30.0")]
	[Register ("android/os/AsyncTask", DoNotGenerateAcw=true)]
	public abstract class AsyncTask<TParams, TProgress, TResult> : AsyncTask {

		static IntPtr java_class_handle;
		internal static IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("android/os/AsyncTask", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (AsyncTask); }
		}

		public AsyncTask (IntPtr doNotUse, JniHandleOwnership transfer)
			: base (doNotUse, transfer)
		{
		}

		static IntPtr id_ctor;
		[Register (".ctor", "()V", "")]
		public AsyncTask ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			if (GetType () != typeof (AsyncTask)) {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "()V"),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "()V");
				return;
			}

			if (id_ctor == IntPtr.Zero)
				id_ctor = JNIEnv.GetMethodID (class_ref, "<init>", "()V");
			SetHandle (
					JNIEnv.StartCreateInstance (class_ref, id_ctor),
					JniHandleOwnership.TransferLocalRef);
			JNIEnv.FinishCreateInstance (Handle, class_ref, id_ctor);
		}

		[Preserve (Conditional = true)]
		protected override Java.Lang.Object? DoInBackground (params Java.Lang.Object[]? native_parms)
		{
			TParams[] parms = new TParams[native_parms?.Length ?? 0];
			for (int i = 0; i < parms.Length; i++)
#pragma warning disable CS8601 // Possible null reference assignment.
				parms [i] = JavaConvert.FromJavaObject<TParams>(native_parms? [i]);
#pragma warning restore CS8601 // Possible null reference assignment.
			return JavaObjectExtensions.JavaCast<Java.Lang.Object>(JavaConvert.ToJavaObject (RunInBackground (parms)));
		}

		[Register ("doInBackground", "([Ljava/lang/Object;)Ljava/lang/Object;", "GetDoInBackground_arrayLjava_lang_Object_Handler")]
		protected abstract TResult RunInBackground (params TParams[] @params);

		static IntPtr id_execute_arrayLjava_lang_Object_;
		[Register ("execute", "([Ljava/lang/Object;)Landroid/os/AsyncTask;", "")]
		public Android.OS.AsyncTask<TParams, TProgress, TResult>? Execute (params TParams[] @params)
		{
			if (id_execute_arrayLjava_lang_Object_ == IntPtr.Zero)
				id_execute_arrayLjava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "execute", "([Ljava/lang/Object;)Landroid/os/AsyncTask;");
			IntPtr native__params = JNIEnv.NewObjectArray<TParams> (@params);
			try {
				var __ret = Java.Lang.Object.GetObject<Android.OS.AsyncTask<TParams, TProgress, TResult>> (JNIEnv.CallObjectMethod  (Handle, id_execute_arrayLjava_lang_Object_, new JValue (native__params)), JniHandleOwnership.TransferLocalRef);
				if (@params != null)
					JNIEnv.CopyObjectArray (native__params, @params);
				return __ret;
			} finally {
				if (@params != null) {
					JNIEnv.DeleteLocalRef (native__params);
				}
			}
		}

		static IntPtr id_get;
		[Register ("get", "()Ljava/lang/Object;", "")]
		public TResult? GetResult ()
		{
			if (id_get == IntPtr.Zero)
				id_get = JNIEnv.GetMethodID (class_ref, "get", "()Ljava/lang/Object;");
			return JavaConvert.FromJniHandle<TResult>(JNIEnv.CallObjectMethod  (Handle, id_get), JniHandleOwnership.TransferLocalRef);
		}

		protected override void OnPostExecute (Java.Lang.Object? result)
		{
			OnPostExecute (JavaConvert.FromJavaObject<TResult> (result));
		}

		[Register ("onPostExecute", "(Ljava/lang/Object;)V", "GetOnPostExecute_Ljava_lang_Object_Handler")]
		protected virtual void OnPostExecute ([AllowNull]TResult result)
		{
			base.OnPostExecute (JavaObjectExtensions.JavaCast<Java.Lang.Object>(JavaConvert.ToJavaObject (result)));
		}

		protected override void OnProgressUpdate (params Java.Lang.Object[]? native_values)
		{
			TProgress[] values = new TProgress [native_values?.Length ?? 0];
			for (int i = 0; i < values.Length; i++)
#pragma warning disable CS8601 // Possible null reference assignment.
				values [i] = JavaConvert.FromJavaObject<TProgress>(native_values? [i]);
#pragma warning restore CS8601 // Possible null reference assignment.
			OnProgressUpdate (values);
		}

		[Register ("onProgressUpdate", "([Ljava/lang/Object;)V", "GetOnProgressUpdate_arrayLjava_lang_Object_Handler")]
		protected virtual void OnProgressUpdate (params TProgress[] values)
		{
			Java.Lang.Object[] native_values = new Java.Lang.Object [values.Length];
			for (int i = 0; i < values.Length; i++)
#pragma warning disable CS8601 // Possible null reference assignment.
				native_values [i] = JavaObjectExtensions.JavaCast<Java.Lang.Object>(JavaConvert.ToJavaObject (values [i]));
#pragma warning restore CS8601 // Possible null reference assignment.
			base.OnProgressUpdate (native_values);
		}

		protected void PublishProgress (params TProgress[] values)
		{
			Java.Lang.Object[] native_values = new Java.Lang.Object [values.Length];
			for (int i = 0; i < values.Length; i++)
#pragma warning disable CS8601 // Possible null reference assignment.
				native_values [i] = JavaObjectExtensions.JavaCast<Java.Lang.Object>(JavaConvert.ToJavaObject (values [i]));
#pragma warning restore CS8601 // Possible null reference assignment.
			base.PublishProgress (native_values);
		}
	}
}
