using System;
using System.Collections.Generic;
using Java.Interop;

namespace Xamarin.Google.Composable {

	// Metadata.xml XPath class reference: path="/api/package[@name='com.com.google.compose']/class[@name='MyClass']"
	[global::Java.Interop.JniTypeSignature ("com/com/google/compose/MyClass", GenerateJavaPeer=false)]
	public partial class MyClass : global::Java.Lang.Object {
		static readonly JniPeerMembers _members = new JniPeerMembers ("com/com/google/compose/MyClass", typeof (MyClass));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected MyClass (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

	}
}
