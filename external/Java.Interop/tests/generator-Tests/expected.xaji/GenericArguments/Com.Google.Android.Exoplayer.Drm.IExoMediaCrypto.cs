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
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_com_google_android_exoplayer_drm_ExoMediaCrypto; }
		}

		static readonly JniPeerMembers _members_com_google_android_exoplayer_drm_ExoMediaCrypto = new JniPeerMembers ("com/google/android/exoplayer/drm/ExoMediaCrypto", typeof (IExoMediaCryptoInvoker));

		public IExoMediaCryptoInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_requiresSecureDecoderComponent_RequiresSecureDecoderComponent_Ljava_lang_String__Z;
#pragma warning disable 0169
		static Delegate GetRequiresSecureDecoderComponent_Ljava_lang_String_Handler ()
		{
			unsafe {
				// SAFETY: The delegate matches the registered callback signature; JNI, not this connector, supplies its arguments.
				return cb_requiresSecureDecoderComponent_RequiresSecureDecoderComponent_Ljava_lang_String__Z ??= new _JniMarshal_PPL_B (n_RequiresSecureDecoderComponent_Ljava_lang_String_);
			}
		}

		/// <safety>
		/// Use the registered JNI ABI on jnienv's attached thread. Receiver/reference arguments
		/// must match the registered types and stay live without concurrent deletion for this call;
		/// permitted nulls stay null. JNI retains borrowed ownership. The helper reports managed
		/// failures and may return default; invalid addresses are not validated.
		/// </safety>
		static unsafe sbyte n_RequiresSecureDecoderComponent_Ljava_lang_String_ (IntPtr jnienv, IntPtr native__this, IntPtr native_p0)
		{
			unsafe {
				// SAFETY: JNI supplies this thread's environment and live typed references; the static target pointer is synchronous.
				return global::Java.Interop.JniMarshal.SafeInvokeFunc (jnienv, native__this, native_p0, &__n_RequiresSecureDecoderComponent_Ljava_lang_String_);
			}
		}
		/// <safety>
		/// Requires the entry point's live, type-correct borrowed references in its active JNI
		/// frame/thread, without concurrent deletion. Copy-back occurs on normal return only;
		/// failures propagate to the marshal helper.
		/// </safety>
		private static unsafe sbyte __n_RequiresSecureDecoderComponent_Ljava_lang_String_ (IntPtr jnienv, IntPtr native__this, IntPtr native_p0)
		{
			unsafe {
				// SAFETY: The marshal frame keeps typed JNI references live on this thread through conversion and normal-return copy-back.
				var __this = global::Java.Lang.Object.GetObject<global::Com.Google.Android.Exoplayer.Drm.IExoMediaCrypto> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var p0 = JNIEnv.GetString (native_p0, JniHandleOwnership.DoNotTransfer);
				sbyte __ret = __this.RequiresSecureDecoderComponent (p0) ? (sbyte)1 : (sbyte)0;
				return __ret;
			}
		}
#pragma warning restore 0169

		public bool RequiresSecureDecoderComponent (string p0)
		{
			const string __id = "requiresSecureDecoderComponent.(Ljava/lang/String;)Z";
			unsafe {
				// SAFETY: __id fixes argument types/count; initialized stack arguments and JNI locals stay on this thread for the synchronous call.
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
}
