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
		static IntPtr java_class_ref {
			get { return _members_com_google_android_exoplayer_drm_ExoMediaCrypto.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_com_google_android_exoplayer_drm_ExoMediaCrypto; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members_com_google_android_exoplayer_drm_ExoMediaCrypto.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members_com_google_android_exoplayer_drm_ExoMediaCrypto.ManagedPeerType; }
		}

		static readonly JniPeerMembers _members_com_google_android_exoplayer_drm_ExoMediaCrypto = new XAPeerMembers ("com/google/android/exoplayer/drm/ExoMediaCrypto", typeof (IExoMediaCryptoInvoker));

		public IExoMediaCryptoInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_requiresSecureDecoderComponent_Ljava_lang_String_;
#pragma warning disable 0169
		static Delegate GetRequiresSecureDecoderComponent_Ljava_lang_String_Handler ()
		{
			if (cb_requiresSecureDecoderComponent_Ljava_lang_String_ == null)
				cb_requiresSecureDecoderComponent_Ljava_lang_String_ = JNINativeWrapper.CreateDelegate (new _JniMarshal_PPL_Z (n_RequiresSecureDecoderComponent_Ljava_lang_String_));
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

		public unsafe bool RequiresSecureDecoderComponent (string p0)
		{
			const string __id = "requiresSecureDecoderComponent.(Ljava/lang/String;)Z";
			IntPtr native_p0 = JNIEnv.NewString ((string)p0);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_p0);
				var __rm = _members_com_google_android_exoplayer_drm_ExoMediaCrypto.InstanceMethods.InvokeAbstractBooleanMethod (__id, this, __args);
				return __rm;
			} finally {
				JNIEnv.DeleteLocalRef (native_p0);
			}
		}

	}
}
