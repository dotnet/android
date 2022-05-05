using System;
using System.Collections.Generic;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='CSharpKeywords']"
	[global::Java.Interop.JniTypeSignature ("xamarin/test/CSharpKeywords", GenerateJavaPeer=false)]
	public partial class CSharpKeywords : global::Java.Lang.Object {
		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/CSharpKeywords", typeof (CSharpKeywords));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected CSharpKeywords (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='CSharpKeywords']/method[@name='usePartial' and count(parameter)=1 and parameter[1][@type='int']]"
		[global::Java.Interop.JniMethodSignature ("usePartial", "(I)Ljava/lang/String;")]
		public virtual unsafe string UsePartial (int partial)
		{
			const string __id = "usePartial.(I)Ljava/lang/String;";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (partial);
				var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, __args);
				return global::Java.Interop.JniEnvironment.Strings.ToString (ref __rm, JniObjectReferenceOptions.CopyAndDispose);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='CSharpKeywords']/method[@name='useThis' and count(parameter)=1 and parameter[1][@type='java.lang.String']]"
		[global::Java.Interop.JniMethodSignature ("useThis", "(Ljava/lang/String;)Ljava/lang/String;")]
		public static unsafe string UseThis (string this_)
		{
			const string __id = "useThis.(Ljava/lang/String;)Ljava/lang/String;";
			var native_this = global::Java.Interop.JniEnvironment.Strings.NewString (this_);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_this);
				var __rm = _members.StaticMethods.InvokeObjectMethod (__id, __args);
				return global::Java.Interop.JniEnvironment.Strings.ToString (ref __rm, JniObjectReferenceOptions.CopyAndDispose);
			} finally {
				global::Java.Interop.JniObjectReference.Dispose (ref native_this);
			}
		}

	}
}
