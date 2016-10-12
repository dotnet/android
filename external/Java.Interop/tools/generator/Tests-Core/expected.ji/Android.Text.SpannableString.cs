using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Android.Text {

	// Metadata.xml XPath class reference: path="/api/package[@name='android.text']/class[@name='SpannableString']"
	[global::Android.Runtime.Register ("android/text/SpannableString", DoNotGenerateAcw=true)]
	public partial class SpannableString : Android.Text.SpannableStringInternal, Android.Text.ISpannable {

		internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("android/text/SpannableString", typeof (SpannableString));
		internal static new IntPtr class_ref {
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

		protected SpannableString (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='android.text']/class[@name='SpannableString']/constructor[@name='SpannableString' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		[Register (".ctor", "(Ljava/lang/CharSequence;)V", "")]
		public unsafe SpannableString (Java.Lang.ICharSequence source)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string __id = "(Ljava/lang/CharSequence;)V";

			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			IntPtr native_source = CharSequence.ToLocalJniHandle (source);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_source);
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), __args);
				SetHandle (__r.Handle, JniHandleOwnership.TransferLocalRef);
				_members.InstanceMethods.FinishCreateInstance (__id, this, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_source);
			}
		}

		[Register (".ctor", "(Ljava/lang/CharSequence;)V", "")]
		public unsafe SpannableString (string source)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string __id = "(Ljava/lang/CharSequence;)V";

			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			IntPtr native_source = CharSequence.ToLocalJniHandle (source);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_source);
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), __args);
				SetHandle (__r.Handle, JniHandleOwnership.TransferLocalRef);
				_members.InstanceMethods.FinishCreateInstance (__id, this, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_source);
			}
		}

		static Delegate cb_getSpanFlags_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetGetSpanFlags_Ljava_lang_Object_Handler ()
		{
			if (cb_getSpanFlags_Ljava_lang_Object_ == null)
				cb_getSpanFlags_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr, int>) n_GetSpanFlags_Ljava_lang_Object_);
			return cb_getSpanFlags_Ljava_lang_Object_;
		}

		static int n_GetSpanFlags_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_what)
		{
			Android.Text.SpannableString __this = global::Java.Lang.Object.GetObject<Android.Text.SpannableString> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			Java.Lang.Object what = global::Java.Lang.Object.GetObject<Java.Lang.Object> (native_what, JniHandleOwnership.DoNotTransfer);
			int __ret = (int) __this.GetSpanFlags (what);
			return __ret;
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='android.text']/class[@name='SpannableString']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[Register ("getSpanFlags", "(Ljava/lang/Object;)I", "GetGetSpanFlags_Ljava_lang_Object_Handler")]
		public override unsafe Android.Text.SpanTypes GetSpanFlags (Java.Lang.Object what)
		{
			const string __id = "getSpanFlags.(Ljava/lang/Object;)I";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue ((what == null) ? IntPtr.Zero : ((global::Java.Lang.Object) what).Handle);
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, __args);
				return (Android.Text.SpanTypes) __rm;
			} finally {
			}
		}

	}
}
