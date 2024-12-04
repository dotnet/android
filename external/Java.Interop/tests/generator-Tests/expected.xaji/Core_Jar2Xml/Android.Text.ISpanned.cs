using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Android.Text {

	// Metadata.xml XPath interface reference: path="/api/package[@name='android.text']/interface[@name='Spanned']"
	[Register ("android/text/Spanned", "", "Android.Text.ISpannedInvoker")]
	public partial interface ISpanned : IJavaObject, IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='android.text']/interface[@name='Spanned']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[return:global::Android.Runtime.GeneratedEnum]
		[Register ("getSpanFlags", "(Ljava/lang/Object;)I", "GetGetSpanFlags_Ljava_lang_Object_Handler:Android.Text.ISpannedInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		global::Android.Text.SpanTypes GetSpanFlags (global::Java.Lang.Object tag);

	}

	[global::Android.Runtime.Register ("android/text/Spanned", DoNotGenerateAcw=true)]
	internal partial class ISpannedInvoker : global::Java.Lang.Object, ISpanned {
		static IntPtr java_class_ref {
			get { return _members_android_text_Spanned.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_android_text_Spanned; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members_android_text_Spanned.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members_android_text_Spanned.ManagedPeerType; }
		}

		static readonly JniPeerMembers _members_android_text_Spanned = new XAPeerMembers ("android/text/Spanned", typeof (ISpannedInvoker));

		public ISpannedInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_getSpanFlags_GetSpanFlags_Ljava_lang_Object__I;
#pragma warning disable 0169
		static Delegate GetGetSpanFlags_Ljava_lang_Object_Handler ()
		{
			return cb_getSpanFlags_GetSpanFlags_Ljava_lang_Object__I ??= new _JniMarshal_PPL_I (n_GetSpanFlags_Ljava_lang_Object_);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		static int n_GetSpanFlags_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_tag)
		{
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				var __this = global::Java.Lang.Object.GetObject<global::Android.Text.ISpanned> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var tag = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_tag, JniHandleOwnership.DoNotTransfer);
				int __ret = (int) __this.GetSpanFlags (tag);
				return __ret;
			} catch (global::System.Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
			}
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
