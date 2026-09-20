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

namespace Android.Text {

	// Metadata.xml XPath class reference: path="/api/package[@name='android.text']/class[@name='SpannableString']"
	[global::Java.Interop.JniTypeSignature ("android/text/SpannableString", GenerateJavaPeer=false)]
	public partial class SpannableString : global::Android.Text.SpannableStringInternal, global::Android.Text.ISpannable {
		static readonly JniPeerMembers _members = new JniPeerMembers ("android/text/SpannableString", typeof (SpannableString));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected SpannableString (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='android.text']/class[@name='SpannableString']/constructor[@name='SpannableString' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		public unsafe SpannableString (global::Java.Lang.ICharSequence source) : base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			const string __id = "(Ljava/lang/CharSequence;)V";

			if (PeerReference.IsValid)
				return;

			IntPtr native_source = CharSequence.ToLocalJniHandle (source);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_source);
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), __args);
				Construct (ref __r, JniObjectReferenceOptions.CopyAndDispose);
				_members.InstanceMethods.FinishCreateInstance (__id, this, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_source);
				global::System.GC.KeepAlive (source);
			}
		}

		public unsafe SpannableString (string source) : base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			const string __id = "(Ljava/lang/CharSequence;)V";

			if (PeerReference.IsValid)
				return;

			IntPtr native_source = CharSequence.ToLocalJniHandle (source);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_source);
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), __args);
				Construct (ref __r, JniObjectReferenceOptions.CopyAndDispose);
				_members.InstanceMethods.FinishCreateInstance (__id, this, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_source);
				global::System.GC.KeepAlive (source);
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='android.text']/class[@name='SpannableString']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		public override unsafe global::Android.Text.SpanTypes GetSpanFlags (global::Java.Lang.Object what)
		{
			const string __id = "getSpanFlags.(Ljava/lang/Object;)I";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (what);
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, __args);
				return (global::Android.Text.SpanTypes) __rm;
			} finally {
				global::System.GC.KeepAlive (what);
			}
		}

	}
}
