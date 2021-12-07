using System;
using System.Collections.Generic;
using Java.Interop;

namespace Com.Google.Android.Exoplayer.Drm {

	// Metadata.xml XPath class reference: path="/api/package[@name='com.google.android.exoplayer.drm']/class[@name='FrameworkMediaDrm']"
	[global::Java.Interop.JniTypeSignature ("com/google/android/exoplayer/drm/FrameworkMediaDrm", GenerateJavaPeer=false)]
	public sealed partial class FrameworkMediaDrm : global::Java.Lang.Object, global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm {
		static readonly JniPeerMembers _members = new JniPeerMembers ("com/google/android/exoplayer/drm/FrameworkMediaDrm", typeof (FrameworkMediaDrm));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		internal FrameworkMediaDrm (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='com.google.android.exoplayer.drm']/class[@name='FrameworkMediaDrm']/constructor[@name='FrameworkMediaDrm' and count(parameter)=0]"
		public unsafe FrameworkMediaDrm () : base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			const string __id = "()V";

			if (PeerReference.IsValid)
				return;

			try {
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), null);
				Construct (ref __r, JniObjectReferenceOptions.CopyAndDispose);
				_members.InstanceMethods.FinishCreateInstance (__id, this, null);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='com.google.android.exoplayer.drm']/class[@name='FrameworkMediaDrm']/method[@name='setOnEventListener' and count(parameter)=1 and parameter[1][@type='com.google.android.exoplayer.drm.ExoMediaDrm.OnEventListener&lt;com.google.android.exoplayer.drm.FrameworkMediaCrypto&gt;']]"
		public unsafe void SetOnEventListener (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener p0)
		{
			const string __id = "setOnEventListener.(Lcom/google/android/exoplayer/drm/ExoMediaDrm$OnEventListener;)V";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (p0);
				_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
			} finally {
				global::System.GC.KeepAlive (p0);
			}
		}

		// This method is explicitly implemented as a member of an instantiated Com.Google.Android.Exoplayer.Drm.IExoMediaDrm
		void global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm.SetOnEventListener (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener p0)
		{
			SetOnEventListener (global::Java.Interop.JavaObjectExtensions.JavaCast<global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener>(p0));
		}

	}
}
