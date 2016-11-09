using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Com.Google.Android.Exoplayer.Drm {

	// Metadata.xml XPath class reference: path="/api/package[@name='com.google.android.exoplayer.drm']/class[@name='FrameworkMediaDrm']"
	[global::Android.Runtime.Register ("com/google/android/exoplayer/drm/FrameworkMediaDrm", DoNotGenerateAcw=true)]
	public sealed partial class FrameworkMediaDrm : global::Java.Lang.Object, global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm {

		internal            static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("com/google/android/exoplayer/drm/FrameworkMediaDrm", typeof (FrameworkMediaDrm));
		internal static IntPtr class_ref {
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

		internal FrameworkMediaDrm (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='com.google.android.exoplayer.drm']/class[@name='FrameworkMediaDrm']/constructor[@name='FrameworkMediaDrm' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe FrameworkMediaDrm ()
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

		// Metadata.xml XPath method reference: path="/api/package[@name='com.google.android.exoplayer.drm']/class[@name='FrameworkMediaDrm']/method[@name='setOnEventListener' and count(parameter)=1 and parameter[1][@type='com.google.android.exoplayer.drm.ExoMediaDrm.OnEventListener&lt;com.google.android.exoplayer.drm.FrameworkMediaCrypto&gt;']]"
		[Register ("setOnEventListener", "(Lcom/google/android/exoplayer/drm/ExoMediaDrm$OnEventListener;)V", "")]
		public unsafe void SetOnEventListener (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener p0)
		{
			const string __id = "setOnEventListener.(Lcom/google/android/exoplayer/drm/ExoMediaDrm$OnEventListener;)V";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue ((p0 == null) ? IntPtr.Zero : ((global::Java.Lang.Object) p0).Handle);
				_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
			} finally {
			}
		}

		// This method is explicitly implemented as a member of an instantiated Com.Google.Android.Exoplayer.Drm.IExoMediaDrm
		void global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm.SetOnEventListener (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener p0)
		{
			SetOnEventListener (global::Java.Interop.JavaObjectExtensions.JavaCast<global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener>(p0));
		}

	}
}
