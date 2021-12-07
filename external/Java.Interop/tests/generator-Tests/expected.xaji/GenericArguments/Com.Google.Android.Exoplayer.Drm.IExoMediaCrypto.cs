using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Com.Google.Android.Exoplayer.Drm {

	// Metadata.xml XPath interface reference: path="/api/package[@name='com.google.android.exoplayer.drm']/interface[@name='ExoMediaCrypto']"
	[Register ("com/google/android/exoplayer/drm/ExoMediaCrypto", "", "Com.Google.Android.Exoplayer.Drm.IExoMediaCryptoInvoker")]
	public partial interface IExoMediaCrypto : IJavaObject, IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='com.google.android.exoplayer.drm']/interface[@name='ExoMediaCrypto']/method[@name='requiresSecureDecoderComponent' and count(parameter)=1 and parameter[1][@type='java.lang.String']]"
		[Register ("requiresSecureDecoderComponent", "(Ljava/lang/String;)Z", "GetRequiresSecureDecoderComponent_Ljava_lang_String_Handler:Com.Google.Android.Exoplayer.Drm.IExoMediaCryptoInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		bool RequiresSecureDecoderComponent (string p0);

	}

	[global::Android.Runtime.Register ("com/google/android/exoplayer/drm/ExoMediaCrypto", DoNotGenerateAcw=true)]
	internal partial class IExoMediaCryptoInvoker : global::Java.Lang.Object, IExoMediaCrypto {
		static readonly JniPeerMembers _members = new XAPeerMembers ("com/google/android/exoplayer/drm/ExoMediaCrypto", typeof (IExoMediaCryptoInvoker));

		static IntPtr java_class_ref {
			get { return _members.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

		new IntPtr class_ref;

		public static IExoMediaCrypto GetObject (IntPtr handle, JniHandleOwnership transfer)
		{
			return global::Java.Lang.Object.GetObject<IExoMediaCrypto> (handle, transfer);
		}

		static IntPtr Validate (IntPtr handle)
		{
			if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
				throw new InvalidCastException ($"Unable to convert instance of type '{JNIEnv.GetClassNameFromInstance (handle)}' to type 'com.google.android.exoplayer.drm.ExoMediaCrypto'.");
			return handle;
		}

		protected override void Dispose (bool disposing)
		{
			if (this.class_ref != IntPtr.Zero)
				JNIEnv.DeleteGlobalRef (this.class_ref);
			this.class_ref = IntPtr.Zero;
			base.Dispose (disposing);
		}

		public IExoMediaCryptoInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
		{
			IntPtr local_ref = JNIEnv.GetObjectClass (((global::Java.Lang.Object) this).Handle);
			this.class_ref = JNIEnv.NewGlobalRef (local_ref);
			JNIEnv.DeleteLocalRef (local_ref);
		}

		static Delegate cb_requiresSecureDecoderComponent_Ljava_lang_String_;
#pragma warning disable 0169
		static Delegate GetRequiresSecureDecoderComponent_Ljava_lang_String_Handler ()
		{
			if (cb_requiresSecureDecoderComponent_Ljava_lang_String_ == null)
				cb_requiresSecureDecoderComponent_Ljava_lang_String_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPL_Z) n_RequiresSecureDecoderComponent_Ljava_lang_String_);
			return cb_requiresSecureDecoderComponent_Ljava_lang_String_;
		}

		static bool n_RequiresSecureDecoderComponent_Ljava_lang_String_ (IntPtr jnienv, IntPtr native__this, IntPtr native_p0)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Com.Google.Android.Exoplayer.Drm.IExoMediaCrypto> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var p0 = JNIEnv.GetString (native_p0, JniHandleOwnership.DoNotTransfer);
			bool __ret = __this.RequiresSecureDecoderComponent (p0);
			return __ret;
		}
#pragma warning restore 0169

		IntPtr id_requiresSecureDecoderComponent_Ljava_lang_String_;
		public unsafe bool RequiresSecureDecoderComponent (string p0)
		{
			if (id_requiresSecureDecoderComponent_Ljava_lang_String_ == IntPtr.Zero)
				id_requiresSecureDecoderComponent_Ljava_lang_String_ = JNIEnv.GetMethodID (class_ref, "requiresSecureDecoderComponent", "(Ljava/lang/String;)Z");
			IntPtr native_p0 = JNIEnv.NewString (p0);
			JValue* __args = stackalloc JValue [1];
			__args [0] = new JValue (native_p0);
			var __ret = JNIEnv.CallBooleanMethod (((global::Java.Lang.Object) this).Handle, id_requiresSecureDecoderComponent_Ljava_lang_String_, __args);
			JNIEnv.DeleteLocalRef (native_p0);
			return __ret;
		}

	}
}
