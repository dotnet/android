using System;
using System.Collections.Generic;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='A']"
	[global::Java.Interop.JniTypeSignature ("xamarin/test/A", GenerateJavaPeer=false)]
	public partial class A : global::Java.Lang.Object {
		// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='A.B']"
		[global::Java.Interop.JniTypeSignature ("xamarin/test/A$B", GenerateJavaPeer=false)]
		[global::Java.Interop.JavaTypeParameters (new string [] {"T extends xamarin.test.A.B"})]
		public partial class B : global::Java.Lang.Object {
			static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/A$B", typeof (B));

			[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
			[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
			public override global::Java.Interop.JniPeerMembers JniPeerMembers {
				get { return _members; }
			}

			protected B (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
			{
			}

			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='A.B']/method[@name='setCustomDimension' and count(parameter)=1 and parameter[1][@type='int']]"
			public virtual unsafe global::Java.Lang.Object SetCustomDimension (int index)
			{
				const string __id = "setCustomDimension.(I)Lxamarin/test/A$B;";
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (index);
					var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, __args);
					return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::global::Java.Lang.Object>(ref __rm, JniObjectReferenceOptions.CopyAndDispose);
				} finally {
				}
			}

		}

		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/A", typeof (A));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected A (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='A']/method[@name='getHandle' and count(parameter)=0]"
		public virtual unsafe int GetHandle ()
		{
			const string __id = "getHandle.()I";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, null);
				return __rm;
			} finally {
			}
		}

	}
}
