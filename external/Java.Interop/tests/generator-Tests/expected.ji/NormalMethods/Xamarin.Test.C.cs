using System;
using System.Collections.Generic;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='C']"
	[global::Java.Interop.JniTypeSignature ("xamarin/test/C", GenerateJavaPeer=false)]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T extends xamarin.test.C"})]
	public partial class C : global::Java.Lang.Object {
		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/C", typeof (C));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected C (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='C']/method[@name='setCustomDimension' and count(parameter)=1 and parameter[1][@type='int']]"
		public virtual unsafe global::Java.Lang.Object SetCustomDimension (int index)
		{
			const string __id = "setCustomDimension.(I)Lxamarin/test/C;";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (index);
				var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, __args);
				return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::global::Java.Lang.Object>(ref __rm, JniObjectReferenceOptions.CopyAndDispose);
			} finally {
			}
		}

	}
}
