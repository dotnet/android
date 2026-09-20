// Java allows a type to redeclare a member that a base type or base interface already
// declares, including members that are hand-bound instead of generated.
#pragma warning disable 0108, 0114
// Java types may declare a `finalize()` method, which is bound as `Finalize()`.
#pragma warning disable 0465
// Java deprecates types and members independently of the APIs that use, declare or
// override them, so a binding that is not deprecated can still reference one that is.
#pragma warning disable 0618, 0672, 0809
// Java nullness annotations are not required to agree between a member and the member
// it overrides or implements, and are absent from much of the API surface.
#pragma warning disable 8603, 8604, 8625, 8764, 8765, 8766, 8767, 8768

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
