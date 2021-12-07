using System;
using System.Collections.Generic;
using Java.Interop;

namespace Com.Google.Android.Exoplayer.Drm {

	// Metadata.xml XPath interface reference: path="/api/package[@name='com.google.android.exoplayer.drm']/interface[@name='ExoMediaDrm.OnEventListener']"
	[global::Java.Interop.JniTypeSignature ("com/google/android/exoplayer/drm/ExoMediaDrm$OnEventListener", GenerateJavaPeer=false)]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T extends com.google.android.exoplayer.drm.ExoMediaCrypto"})]
	public partial interface IExoMediaDrmOnEventListener : IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='com.google.android.exoplayer.drm']/interface[@name='ExoMediaDrm.OnEventListener']/method[@name='onEvent' and count(parameter)=5 and parameter[1][@type='com.google.android.exoplayer.drm.ExoMediaDrm&lt;T&gt;'] and parameter[2][@type='byte[]'] and parameter[3][@type='int'] and parameter[4][@type='int'] and parameter[5][@type='byte[]']]"
		void OnEvent (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm p0, global::Java.Interop.JavaSByteArray p1, int p2, int p3, global::Java.Interop.JavaSByteArray p4);

	}

	// event args for com.google.android.exoplayer.drm.ExoMediaDrm.OnEventListener.onEvent
	public partial class ExoMediaDrmOnEventEventArgs : global::System.EventArgs {
		public ExoMediaDrmOnEventEventArgs (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm p0, global::Java.Interop.JavaSByteArray p1, int p2, int p3, global::Java.Interop.JavaSByteArray p4)
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

		global::Java.Interop.JavaSByteArray p1;

		public global::Java.Interop.JavaSByteArray P1 {
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

		global::Java.Interop.JavaSByteArray p4;

		public global::Java.Interop.JavaSByteArray P4 {
			get { return p4; }
		}

	}

	[global::Android.Runtime.Register ("mono/com/google/android/exoplayer/drm/ExoMediaDrm_OnEventListenerImplementor")]
	internal sealed partial class IExoMediaDrmOnEventListenerImplementor : global::Java.Lang.Object, IExoMediaDrmOnEventListener {

		object sender;

		public IExoMediaDrmOnEventListenerImplementor (object sender) : base (global::Android.Runtime.JNIEnv.StartCreateInstance ("mono/com/google/android/exoplayer/drm/ExoMediaDrm_OnEventListenerImplementor", "()V"), JniHandleOwnership.TransferLocalRef)
		{
			global::Android.Runtime.JNIEnv.FinishCreateInstance (this.PeerReference, "()V");
			this.sender = sender;
		}

		#pragma warning disable 0649
		public EventHandler<ExoMediaDrmOnEventEventArgs> Handler;
		#pragma warning restore 0649

		public void OnEvent (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrm p0, global::Java.Interop.JavaSByteArray p1, int p2, int p3, global::Java.Interop.JavaSByteArray p4)
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
	[global::Java.Interop.JniTypeSignature ("com/google/android/exoplayer/drm/ExoMediaDrm", GenerateJavaPeer=false)]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T extends com.google.android.exoplayer.drm.ExoMediaCrypto"})]
	public partial interface IExoMediaDrm : IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='com.google.android.exoplayer.drm']/interface[@name='ExoMediaDrm']/method[@name='setOnEventListener' and count(parameter)=1 and parameter[1][@type='com.google.android.exoplayer.drm.ExoMediaDrm.OnEventListener&lt;T&gt;']]"
		void SetOnEventListener (global::Com.Google.Android.Exoplayer.Drm.IExoMediaDrmOnEventListener p0);

	}
}
