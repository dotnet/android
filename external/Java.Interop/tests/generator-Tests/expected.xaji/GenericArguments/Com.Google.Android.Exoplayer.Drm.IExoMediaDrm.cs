using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Com.Google.Android.Exoplayer.Drm {

	// Metadata.xml XPath interface reference: path="/api/package[@name='com.google.android.exoplayer.drm']/interface[@name='ExoMediaDrm.OnEventListener']"
	[Register ("com/google/android/exoplayer/drm/ExoMediaDrm$OnEventListener", "", "Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListenerInvoker")]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T extends com.google.android.exoplayer.drm.ExoMediaCrypto"})]
	public partial interface IExoMediaDrmOnEventListener : IJavaObject, IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='com.google.android.exoplayer.drm']/interface[@name='ExoMediaDrm.OnEventListener']/method[@name='onEvent' and count(parameter)=5 and parameter[1][@type='com.google.android.exoplayer.drm.ExoMediaDrm&lt;T&gt;'] and parameter[2][@type='byte[]'] and parameter[3][@type='int'] and parameter[4][@type='int'] and parameter[5][@type='byte[]']]"
		[Register ("onEvent", "(Lcom/google/android/exoplayer/drm/ExoMediaDrm;[BII[B)V", "GetOnEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayBHandler:Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		void OnEvent (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm p0, byte[] p1, int p2, int p3, byte[] p4);

	}

	[global::Android.Runtime.Register ("com/google/android/exoplayer/drm/ExoMediaDrm$OnEventListener", DoNotGenerateAcw=true)]
	internal partial class IExoMediaDrmOnEventListenerInvoker : global::Java.Lang.Object, IExoMediaDrmOnEventListener {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_com_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener; }
		}

		static readonly JniPeerMembers _members_com_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener = new JniPeerMembers ("com/google/android/exoplayer/drm/ExoMediaDrm$OnEventListener", typeof (IExoMediaDrmOnEventListenerInvoker));

		/// <safety>handle must be zero or a live JNI object implementing this interface. Use an attached thread (locals: creating thread), with no concurrent deletion through aliases during construction. transfer must match ownership; transferred references cannot be used again, even if construction throws.</safety>
		/// <remarks>This constructs a JNI peer, not an opaque-address container. RequiresUnsafe enforcement is deferred to preserve safe zero/DoNotTransfer base initializers; the raw-reference obligations still apply.</remarks>
		public IExoMediaDrmOnEventListenerInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_onEvent_OnEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB_V;
#pragma warning disable 0169
		static Delegate GetOnEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayBHandler ()
		{
			unsafe {
				// SAFETY: The delegate matches the registered callback signature; JNI, not this connector, supplies its arguments.
				return cb_onEvent_OnEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB_V ??= new _JniMarshal_PPLLIIL_V (n_OnEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB);
			}
		}

		/// <safety>Use the registered JNI ABI on jnienv's attached thread. Receiver/reference arguments must match the registered types and stay live without concurrent deletion for this call; permitted nulls stay null. JNI retains borrowed ownership. The helper reports managed failures and may return default; invalid addresses are not validated.</safety>
		static unsafe void n_OnEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB (IntPtr jnienv, IntPtr native__this, IntPtr native_p0, IntPtr native_p1, int p2, int p3, IntPtr native_p4)
		{
			unsafe {
				// SAFETY: JNI supplies this thread's environment and live typed references; the static target pointer is synchronous.
				global::Java.Interop.JniMarshal.SafeInvokeAction (jnienv, native__this, native_p0, native_p1, p2, p3, native_p4, &__n_OnEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB);
			}
		}
		/// <safety>Requires the entry point's live, type-correct borrowed references in its active JNI frame/thread, without concurrent deletion. Copy-back occurs on normal return only; failures propagate to the marshal helper.</safety>
		private static unsafe void __n_OnEvent_Lcom_google_android_exoplayer_drm_ExoMediaDrm_arrayBIIarrayB (IntPtr jnienv, IntPtr native__this, IntPtr native_p0, IntPtr native_p1, int p2, int p3, IntPtr native_p4)
		{
			unsafe {
				// SAFETY: The marshal frame keeps typed JNI references live on this thread through conversion and normal-return copy-back.
				var __this = global::Java.Lang.Object.GetObject<global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var p0 = (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm)global::Java.Lang.Object.GetObject<global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm> (native_p0, JniHandleOwnership.DoNotTransfer);
				var p1 = (byte[]) JNIEnv.GetArray (native_p1, JniHandleOwnership.DoNotTransfer, typeof (byte));
				var p4 = (byte[]) JNIEnv.GetArray (native_p4, JniHandleOwnership.DoNotTransfer, typeof (byte));
				__this.OnEvent (p0, p1, p2, p3, p4);
				if (p1 != null)
					JNIEnv.CopyArray (p1, native_p1);
				if (p4 != null)
					JNIEnv.CopyArray (p4, native_p4);
			}
		}
#pragma warning restore 0169

		public void OnEvent (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm p0, byte[] p1, int p2, int p3, byte[] p4)
		{
			const string __id = "onEvent.(Lcom/google/android/exoplayer/drm/ExoMediaDrm;[BII[B)V";
			unsafe {
				// SAFETY: __id fixes argument types/count; initialized stack arguments and JNI locals stay on this thread for the synchronous call.
				IntPtr native_p1 = JNIEnv.NewArray (p1);
				IntPtr native_p4 = JNIEnv.NewArray (p4);
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [5];
					__args [0] = new JniArgumentValue ((p0 == null) ? IntPtr.Zero : ((global::Java.Lang.Object) p0).Handle);
					__args [1] = new JniArgumentValue (native_p1);
					__args [2] = new JniArgumentValue (p2);
					__args [3] = new JniArgumentValue (p3);
					__args [4] = new JniArgumentValue (native_p4);
					_members_com_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
				} finally {
					if (p1 != null) {
						JNIEnv.CopyArray (native_p1, p1);
						JNIEnv.DeleteLocalRef (native_p1);
					}
					if (p4 != null) {
						JNIEnv.CopyArray (native_p4, p4);
						JNIEnv.DeleteLocalRef (native_p4);
					}
					global::System.GC.KeepAlive (p0);
					global::System.GC.KeepAlive (p1);
					global::System.GC.KeepAlive (p4);
				}
			}
		}

	}

	// event args for com.google.android.exoplayer.drm.ExoMediaDrm.OnEventListener.onEvent
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

		public IExoMediaDrmOnEventListenerImplementor (object sender) : base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string __id = "()V";
			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;
			unsafe {
				// SAFETY: ()V needs no arguments; SetHandle takes the new local peer before FinishCreateInstance.
				var h = JniPeerMembers.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), null);
				SetHandle (h.Handle, JniHandleOwnership.TransferLocalRef);
				JniPeerMembers.InstanceMethods.FinishCreateInstance (__id, this, null);
			}
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
	public partial interface IExoMediaDrm : IJavaObject, IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='com.google.android.exoplayer.drm']/interface[@name='ExoMediaDrm']/method[@name='setOnEventListener' and count(parameter)=1 and parameter[1][@type='com.google.android.exoplayer.drm.ExoMediaDrm.OnEventListener&lt;T&gt;']]"
		[Register ("setOnEventListener", "(Lcom/google/android/exoplayer/drm/ExoMediaDrm$OnEventListener;)V", "GetSetOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_Handler:Com.Google.Android.Exoplayer.Drm.IExoMediaDrmInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		void SetOnEventListener (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener p0);

	}

	[global::Android.Runtime.Register ("com/google/android/exoplayer/drm/ExoMediaDrm", DoNotGenerateAcw=true)]
	internal partial class IExoMediaDrmInvoker : global::Java.Lang.Object, IExoMediaDrm {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_com_google_android_exoplayer_drm_ExoMediaDrm; }
		}

		static readonly JniPeerMembers _members_com_google_android_exoplayer_drm_ExoMediaDrm = new JniPeerMembers ("com/google/android/exoplayer/drm/ExoMediaDrm", typeof (IExoMediaDrmInvoker));

		/// <safety>handle must be zero or a live JNI object implementing this interface. Use an attached thread (locals: creating thread), with no concurrent deletion through aliases during construction. transfer must match ownership; transferred references cannot be used again, even if construction throws.</safety>
		/// <remarks>This constructs a JNI peer, not an opaque-address container. RequiresUnsafe enforcement is deferred to preserve safe zero/DoNotTransfer base initializers; the raw-reference obligations still apply.</remarks>
		public IExoMediaDrmInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_setOnEventListener_SetOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener__V;
#pragma warning disable 0169
		static Delegate GetSetOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_Handler ()
		{
			unsafe {
				// SAFETY: The delegate matches the registered callback signature; JNI, not this connector, supplies its arguments.
				return cb_setOnEventListener_SetOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener__V ??= new _JniMarshal_PPL_V (n_SetOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_);
			}
		}

		/// <safety>Use the registered JNI ABI on jnienv's attached thread. Receiver/reference arguments must match the registered types and stay live without concurrent deletion for this call; permitted nulls stay null. JNI retains borrowed ownership. The helper reports managed failures and may return default; invalid addresses are not validated.</safety>
		static unsafe void n_SetOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_ (IntPtr jnienv, IntPtr native__this, IntPtr native_p0)
		{
			unsafe {
				// SAFETY: JNI supplies this thread's environment and live typed references; the static target pointer is synchronous.
				global::Java.Interop.JniMarshal.SafeInvokeAction (jnienv, native__this, native_p0, &__n_SetOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_);
			}
		}
		/// <safety>Requires the entry point's live, type-correct borrowed references in its active JNI frame/thread, without concurrent deletion. Copy-back occurs on normal return only; failures propagate to the marshal helper.</safety>
		private static unsafe void __n_SetOnEventListener_Lcom_google_android_exoplayer_drm_ExoMediaDrm_OnEventListener_ (IntPtr jnienv, IntPtr native__this, IntPtr native_p0)
		{
			unsafe {
				// SAFETY: The marshal frame keeps typed JNI references live on this thread through conversion and normal-return copy-back.
				var __this = global::Java.Lang.Object.GetObject<global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var p0 = (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener)global::Java.Lang.Object.GetObject<global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener> (native_p0, JniHandleOwnership.DoNotTransfer);
				__this.SetOnEventListener (p0);
			}
		}
#pragma warning restore 0169

		public void SetOnEventListener (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener p0)
		{
			const string __id = "setOnEventListener.(Lcom/google/android/exoplayer/drm/ExoMediaDrm$OnEventListener;)V";
			unsafe {
				// SAFETY: __id fixes argument types/count; initialized stack arguments and JNI locals stay on this thread for the synchronous call.
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue ((p0 == null) ? IntPtr.Zero : ((global::Java.Lang.Object) p0).Handle);
				_members_com_google_android_exoplayer_drm_ExoMediaDrm.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
				global::System.GC.KeepAlive (p0);
			}
		}

	}
}
