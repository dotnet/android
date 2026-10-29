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
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_android_text_Spanned; }
		}

		static readonly JniPeerMembers _members_android_text_Spanned = new JniPeerMembers ("android/text/Spanned", typeof (ISpannedInvoker));

		/// <safety>
		/// handle must be zero or a live JNI object implementing this interface. Use an attached
		/// thread (locals: creating thread), with no concurrent deletion through aliases during
		/// construction. transfer must match ownership; transferred references cannot be used again,
		/// even if construction throws.
		/// </safety>
		/// <remarks>
		/// This constructs a JNI peer, not an opaque-address container. RequiresUnsafe enforcement
		/// is deferred to preserve safe zero/DoNotTransfer base initializers; the raw-reference
		/// obligations still apply.
		/// </remarks>
		public ISpannedInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_getSpanFlags_GetSpanFlags_Ljava_lang_Object__I;
#pragma warning disable 0169
		static Delegate GetGetSpanFlags_Ljava_lang_Object_Handler ()
		{
			unsafe {
				// SAFETY: The delegate matches the registered callback signature; JNI, not this connector, supplies its arguments.
				return cb_getSpanFlags_GetSpanFlags_Ljava_lang_Object__I ??= new _JniMarshal_PPL_I (n_GetSpanFlags_Ljava_lang_Object_);
			}
		}

		/// <safety>
		/// Use the registered JNI ABI on jnienv's attached thread. Receiver/reference arguments
		/// must match the registered types and stay live without concurrent deletion for this call;
		/// permitted nulls stay null. JNI retains borrowed ownership. The helper reports managed
		/// failures and may return default; invalid addresses are not validated.
		/// </safety>
		static unsafe int n_GetSpanFlags_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_tag)
		{
			unsafe {
				// SAFETY: JNI supplies this thread's environment and live typed references; the static target pointer is synchronous.
				return global::Java.Interop.JniMarshal.SafeInvokeFunc (jnienv, native__this, native_tag, &__n_GetSpanFlags_Ljava_lang_Object_);
			}
		}
		/// <safety>
		/// Requires the entry point's live, type-correct borrowed references in its active JNI
		/// frame/thread, without concurrent deletion. Copy-back occurs on normal return only;
		/// failures propagate to the marshal helper.
		/// </safety>
		private static unsafe int __n_GetSpanFlags_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_tag)
		{
			unsafe {
				// SAFETY: The marshal frame keeps typed JNI references live on this thread through conversion and normal-return copy-back.
				var __this = global::Java.Lang.Object.GetObject<global::Android.Text.ISpanned> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var tag = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_tag, JniHandleOwnership.DoNotTransfer);
				int __ret = (int) __this.GetSpanFlags (tag);
				return __ret;
			}
		}
#pragma warning restore 0169

		public global::Android.Text.SpanTypes GetSpanFlags (global::Java.Lang.Object tag)
		{
			const string __id = "getSpanFlags.(Ljava/lang/Object;)I";
			unsafe {
				// SAFETY: __id fixes argument types/count; initialized stack arguments and JNI locals stay on this thread for the synchronous call.
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue ((tag == null) ? IntPtr.Zero : ((global::Java.Lang.Object) tag).Handle);
				var __rm = _members_android_text_Spanned.InstanceMethods.InvokeAbstractInt32Method (__id, this, __args);
				var __result = (global::Android.Text.SpanTypes) __rm;
				global::System.GC.KeepAlive (tag);
				return __result;
			}
		}

	}
}
