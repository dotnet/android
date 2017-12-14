using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Com.Google.Android.Exoplayer.Drm {

	// Metadata.xml XPath interface reference: path="/api/package[@name='com.google.android.exoplayer.drm']/interface[@name='ExoMediaDrm.OnEventListener']"
	[Register ("com/google/android/exoplayer/drm/ExoMediaDrm$OnEventListener", "", "Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListenerInvoker")]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T extends com.google.android.exoplayer.drm.ExoMediaCrypto"})]
	public partial interface IExoMediaDrmOnEventListener : IJavaObject {

		// Metadata.xml XPath method reference: path="/api/package[@name='com.google.android.exoplayer.drm']/interface[@name='ExoMediaDrm.OnEventListener']/method[@name='onEvent' and count(parameter)=5 and parameter[1][@type='com.google.android.exoplayer.drm.ExoMediaDrm&lt;T&gt;'] and parameter[2][@type='byte[]'] and parameter[3][@type='int'] and parameter[4][@type='int'] and parameter[5][@type='byte[]']]"
		[Register ("onEvent", "(Lcom/google/android/exoplayer/drm/ExoMediaDrm;[BII[B)V", "GetOnEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayBHandler:Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		void OnEvent (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm p0, byte[] p1, int p2, int p3, byte[] p4);

	}

	[global::Android.Runtime.Register ("com/google/android/exoplayer/drm/ExoMediaDrm$OnEventListener", DoNotGenerateAcw=true)]
	internal class IExoMediaDrmOnEventListenerInvoker : global::Java.Lang.Object, IExoMediaDrmOnEventListener {

		static IntPtr java_class_ref = JNIEnv.FindClass ("com/google/android/exoplayer/drm/ExoMediaDrm$OnEventListener");

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (IExoMediaDrmOnEventListenerInvoker); }
		}

		IntPtr class_ref;

		public static IExoMediaDrmOnEventListener GetObject (IntPtr handle, JniHandleOwnership transfer)
		{
			return global::Java.Lang.Object.GetObject<IExoMediaDrmOnEventListener> (handle, transfer);
		}

		static IntPtr Validate (IntPtr handle)
		{
			if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
				throw new InvalidCastException (string.Format ("Unable to convert instance of type '{0}' to type '{1}'.",
							JNIEnv.GetClassNameFromInstance (handle), "com.google.android.exoplayer.drm.ExoMediaDrm.OnEventListener"));
			return handle;
		}

		protected override void Dispose (bool disposing)
		{
			if (this.class_ref != IntPtr.Zero)
				JNIEnv.DeleteGlobalRef (this.class_ref);
			this.class_ref = IntPtr.Zero;
			base.Dispose (disposing);
		}

		public IExoMediaDrmOnEventListenerInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
		{
			IntPtr local_ref = JNIEnv.GetObjectClass (((global::Java.Lang.Object) this).Handle);
			this.class_ref = JNIEnv.NewGlobalRef (local_ref);
			JNIEnv.DeleteLocalRef (local_ref);
		}

		static Delegate cb_onEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB;
#pragma warning disable 0169
		static Delegate GetOnEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayBHandler ()
		{
			if (cb_onEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB == null)
				cb_onEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr, IntPtr, int, int, IntPtr>) n_OnEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB);
			return cb_onEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB;
		}

		static void n_OnEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB (IntPtr jnienv, IntPtr native__this, IntPtr native_p0, IntPtr native_p1, int p2, int p3, IntPtr native_p4)
		{
			global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener __this = global::Java.Lang.Object.GetObject<global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm p0 = (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm)global::Java.Lang.Object.GetObject<global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm> (native_p0, JniHandleOwnership.DoNotTransfer);
			byte[] p1 = (byte[]) JNIEnv.GetArray (native_p1, JniHandleOwnership.DoNotTransfer, typeof (byte));
			byte[] p4 = (byte[]) JNIEnv.GetArray (native_p4, JniHandleOwnership.DoNotTransfer, typeof (byte));
			__this.OnEvent (p0, p1, p2, p3, p4);
			if (p1 != null)
				JNIEnv.CopyArray (p1, native_p1);
			if (p4 != null)
				JNIEnv.CopyArray (p4, native_p4);
		}
#pragma warning restore 0169

		IntPtr id_onEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB;
		public unsafe void OnEvent (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm p0, byte[] p1, int p2, int p3, byte[] p4)
		{
			if (id_onEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB == IntPtr.Zero)
				id_onEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB = JNIEnv.GetMethodID (class_ref, "onEvent", "(Lcom/google/android/exoplayer/drm/ExoMediaDrm;[BII[B)V");
			IntPtr native_p1 = JNIEnv.NewArray (p1);
			IntPtr native_p4 = JNIEnv.NewArray (p4);
			JValue* __args = stackalloc JValue [5];
			__args [0] = new JValue (p0);
			__args [1] = new JValue (native_p1);
			__args [2] = new JValue (p2);
			__args [3] = new JValue (p3);
			__args [4] = new JValue (native_p4);
			JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_onEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB, __args);
			if (p1 != null) {
				JNIEnv.CopyArray (native_p1, p1);
				JNIEnv.DeleteLocalRef (native_p1);
			}
			if (p4 != null) {
				JNIEnv.CopyArray (native_p4, p4);
				JNIEnv.DeleteLocalRef (native_p4);
			}
		}

	}

	public partial class ExoMediaDrmOnEventEventArgs : global::System.EventArgs {

		public ExoMediaDrmOnEventEventArgs (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm p0, byte[] p1, int p2, int p3, byte[] p4)
		{
			this.p0 = p0;
			this.p1 = p1;
			this.p2 = p2;
			this.p3 = p3;
			this.p4 = p4;
		}

		global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm p0;
		public global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm P0 {
			get { return p0; }
		}

		byte[] p1;
		public byte[] P1 {
			get { return p1; }
		}

		int p2;
		public int P2 {
			get { return p2; }
		}

		int p3;
		public int P3 {
			get { return p3; }
		}

		byte[] p4;
		public byte[] P4 {
			get { return p4; }
		}
	}

	[global::Android.Runtime.Register ("mono/com/google/android/exoplayer/drm/ExoMediaDrm_OnEventListenerImplementor")]
	internal sealed partial class IExoMediaDrmOnEventListenerImplementor : global::Java.Lang.Object, IExoMediaDrmOnEventListener {

		object sender;

		public IExoMediaDrmOnEventListenerImplementor (object sender)
			: base (
				global::Android.Runtime.JNIEnv.StartCreateInstance ("mono/com/google/android/exoplayer/drm/ExoMediaDrm_OnEventListenerImplementor", "()V"),
				JniHandleOwnership.TransferLocalRef)
		{
			global::Android.Runtime.JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, "()V");
			this.sender = sender;
		}

#pragma warning disable 0649
		public EventHandler<ExoMediaDrmOnEventEventArgs> Handler;
#pragma warning restore 0649

		public void OnEvent (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm p0, byte[] p1, int p2, int p3, byte[] p4)
		{
			var __h = Handler;
			if (__h != null)
				__h (sender, new ExoMediaDrmOnEventEventArgs (p0, p1, p2, p3, p4));
		}

		internal static bool __IsEmpty (IExoMediaDrmOnEventListenerImplementor value)
		{
			return value.Handler == null;
		}
	}


	// Metadata.xml XPath interface reference: path="/api/package[@name='com.google.android.exoplayer.drm']/interface[@name='ExoMediaDrm']"
	[Register ("com/google/android/exoplayer/drm/ExoMediaDrm", "", "Com.Google.Android.Exoplayer.Drm.IExoMediaDrmInvoker")]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T extends com.google.android.exoplayer.drm.ExoMediaCrypto"})]
	public partial interface IExoMediaDrm : IJavaObject {

		// Metadata.xml XPath method reference: path="/api/package[@name='com.google.android.exoplayer.drm']/interface[@name='ExoMediaDrm']/method[@name='setOnEventListener' and count(parameter)=1 and parameter[1][@type='com.google.android.exoplayer.drm.ExoMediaDrm.OnEventListener&lt;T&gt;']]"
		[Register ("setOnEventListener", "(Lcom/google/android/exoplayer/drm/ExoMediaDrm$OnEventListener;)V", "GetSetOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_Handler:Com.Google.Android.Exoplayer.Drm.IExoMediaDrmInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		void SetOnEventListener (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener p0);

	}

	[global::Android.Runtime.Register ("com/google/android/exoplayer/drm/ExoMediaDrm", DoNotGenerateAcw=true)]
	internal class IExoMediaDrmInvoker : global::Java.Lang.Object, IExoMediaDrm {

		static IntPtr java_class_ref = JNIEnv.FindClass ("com/google/android/exoplayer/drm/ExoMediaDrm");

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (IExoMediaDrmInvoker); }
		}

		IntPtr class_ref;

		public static IExoMediaDrm GetObject (IntPtr handle, JniHandleOwnership transfer)
		{
			return global::Java.Lang.Object.GetObject<IExoMediaDrm> (handle, transfer);
		}

		static IntPtr Validate (IntPtr handle)
		{
			if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
				throw new InvalidCastException (string.Format ("Unable to convert instance of type '{0}' to type '{1}'.",
							JNIEnv.GetClassNameFromInstance (handle), "com.google.android.exoplayer.drm.ExoMediaDrm"));
			return handle;
		}

		protected override void Dispose (bool disposing)
		{
			if (this.class_ref != IntPtr.Zero)
				JNIEnv.DeleteGlobalRef (this.class_ref);
			this.class_ref = IntPtr.Zero;
			base.Dispose (disposing);
		}

		public IExoMediaDrmInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
		{
			IntPtr local_ref = JNIEnv.GetObjectClass (((global::Java.Lang.Object) this).Handle);
			this.class_ref = JNIEnv.NewGlobalRef (local_ref);
			JNIEnv.DeleteLocalRef (local_ref);
		}

		static Delegate cb_setOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_;
#pragma warning disable 0169
		static Delegate GetSetOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_Handler ()
		{
			if (cb_setOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_ == null)
				cb_setOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_SetOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_);
			return cb_setOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_;
		}

		static void n_SetOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_ (IntPtr jnienv, IntPtr native__this, IntPtr native_p0)
		{
			global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm __this = global::Java.Lang.Object.GetObject<global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener p0 = (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener)global::Java.Lang.Object.GetObject<global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener> (native_p0, JniHandleOwnership.DoNotTransfer);
			__this.SetOnEventListener (p0);
		}
#pragma warning restore 0169

		IntPtr id_setOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_;
		public unsafe void SetOnEventListener (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener p0)
		{
			if (id_setOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_ == IntPtr.Zero)
				id_setOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_ = JNIEnv.GetMethodID (class_ref, "setOnEventListener", "(Lcom/google/android/exoplayer/drm/ExoMediaDrm$OnEventListener;)V");
			JValue* __args = stackalloc JValue [1];
			__args [0] = new JValue (p0);
			JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_setOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_, __args);
		}

	}

}
