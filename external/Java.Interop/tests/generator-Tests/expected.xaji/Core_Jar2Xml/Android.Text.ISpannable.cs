using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Android.Text {

	// Metadata.xml XPath interface reference: path="/api/package[@name='android.text']/interface[@name='Spannable']"
	[Register ("android/text/Spannable", "", "Android.Text.ISpannableInvoker")]
	public partial interface ISpannable : global::Android.Text.ISpanned {
	}

	[global::Android.Runtime.Register ("android/text/Spannable", DoNotGenerateAcw=true)]
	internal partial class ISpannableInvoker : global::Java.Lang.Object, ISpannable {
		static IntPtr java_class_ref {
			get { return _members_android_text_Spannable.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_android_text_Spannable; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members_android_text_Spannable.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members_android_text_Spannable.ManagedPeerType; }
		}

		static readonly JniPeerMembers _members_android_text_Spannable = new XAPeerMembers ("android/text/Spannable", typeof (ISpannableInvoker));

		static readonly JniPeerMembers _members_android_text_Spanned = new XAPeerMembers ("android/text/Spanned", typeof (ISpannableInvoker));

		public ISpannableInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_getSpanFlags_GetSpanFlags_Ljava_lang_Object__I;
#pragma warning disable 0169
		static Delegate GetGetSpanFlags_Ljava_lang_Object_Handler ()
		{
			if (cb_getSpanFlags_GetSpanFlags_Ljava_lang_Object__I == null)
				cb_getSpanFlags_GetSpanFlags_Ljava_lang_Object__I = JNINativeWrapper.CreateDelegate (new _JniMarshal_PPL_I (n_GetSpanFlags_Ljava_lang_Object_));
			return cb_getSpanFlags_GetSpanFlags_Ljava_lang_Object__I;
		}

		static int n_GetSpanFlags_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_tag)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Android.Text.ISpannable> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var tag = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_tag, JniHandleOwnership.DoNotTransfer);
			int __ret = (int) __this.GetSpanFlags (tag);
			return __ret;
		}
#pragma warning restore 0169

		public unsafe global::Android.Text.SpanTypes GetSpanFlags (global::Java.Lang.Object tag)
		{
			const string __id = "getSpanFlags.(Ljava/lang/Object;)I";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue ((tag == null) ? IntPtr.Zero : ((global::Java.Lang.Object) tag).Handle);
				var __rm = _members_android_text_Spanned.InstanceMethods.InvokeAbstractInt32Method (__id, this, __args);
				return (global::Android.Text.SpanTypes) __rm;
			} finally {
				global::System.GC.KeepAlive (tag);
			}
		}

	}
}
