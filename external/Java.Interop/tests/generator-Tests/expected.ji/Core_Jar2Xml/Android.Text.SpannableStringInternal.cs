using System;
using System.Collections.Generic;
using Java.Interop;

namespace Android.Text {

	// Metadata.xml XPath class reference: path="/api/package[@name='android.text']/class[@name='SpannableStringInternal']"
	[global::Java.Interop.JniTypeSignature ("android/text/SpannableStringInternal", GenerateJavaPeer=false)]
	public abstract partial class SpannableStringInternal : global::Java.Lang.Object {
		static readonly JniPeerMembers _members = new JniPeerMembers ("android/text/SpannableStringInternal", typeof (SpannableStringInternal));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected SpannableStringInternal (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='android.text']/class[@name='SpannableStringInternal']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[return:global::Android.Runtime.GeneratedEnum]
		public virtual unsafe global::Android.Text.SpanTypes GetSpanFlags (global::Java.Lang.Object p0)
		{
			const string __id = "getSpanFlags.(Ljava/lang/Object;)I";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (p0);
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, __args);
				return (global::Android.Text.SpanTypes) __rm;
			} finally {
				global::System.GC.KeepAlive (p0);
			}
		}

	}

	[global::Java.Interop.JniTypeSignature ("android/text/SpannableStringInternal", GenerateJavaPeer=false)]
	internal partial class SpannableStringInternalInvoker : SpannableStringInternal {
		public SpannableStringInternalInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		static readonly JniPeerMembers _members = new JniPeerMembers ("android/text/SpannableStringInternal", typeof (SpannableStringInternalInvoker));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

	}
}
