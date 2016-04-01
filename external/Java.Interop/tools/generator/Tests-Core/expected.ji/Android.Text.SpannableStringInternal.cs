using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Android.Text {

	// Metadata.xml XPath class reference: path="/api/package[@name='android.text']/class[@name='SpannableStringInternal']"
	[global::Android.Runtime.Register ("android/text/SpannableStringInternal", DoNotGenerateAcw=true)]
	public abstract partial class SpannableStringInternal : Java.Lang.Object {

		internal            static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("android/text/SpannableStringInternal", typeof (SpannableStringInternal));
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

		protected SpannableStringInternal (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static Delegate cb_getSpanFlags_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetGetSpanFlags_Ljava_lang_Object_Handler ()
		{
			if (cb_getSpanFlags_Ljava_lang_Object_ == null)
				cb_getSpanFlags_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr, int>) n_GetSpanFlags_Ljava_lang_Object_);
			return cb_getSpanFlags_Ljava_lang_Object_;
		}

		static int n_GetSpanFlags_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_p0)
		{
			Android.Text.SpannableStringInternal __this = global::Java.Lang.Object.GetObject<Android.Text.SpannableStringInternal> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			Java.Lang.Object p0 = global::Java.Lang.Object.GetObject<Java.Lang.Object> (native_p0, JniHandleOwnership.DoNotTransfer);
			int __ret = (int) __this.GetSpanFlags (p0);
			return __ret;
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='android.text']/class[@name='SpannableStringInternal']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[return:global::Android.Runtime.GeneratedEnum]
		[Register ("getSpanFlags", "(Ljava/lang/Object;)I", "GetGetSpanFlags_Ljava_lang_Object_Handler")]
		public virtual unsafe Android.Text.SpanTypes GetSpanFlags (Java.Lang.Object p0)
		{
			const string __id = "getSpanFlags.(Ljava/lang/Object;)I";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue ((p0 == null) ? IntPtr.Zero : p0.Handle);
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, __args);
				return (Android.Text.SpanTypes) __rm;
			} finally {
			}
		}

	}

	[global::Android.Runtime.Register ("android/text/SpannableStringInternal", DoNotGenerateAcw=true)]
	internal partial class SpannableStringInternalInvoker : SpannableStringInternal {

		public SpannableStringInternalInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("android/text/SpannableStringInternal", typeof (SpannableStringInternalInvoker));

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

	}

}
