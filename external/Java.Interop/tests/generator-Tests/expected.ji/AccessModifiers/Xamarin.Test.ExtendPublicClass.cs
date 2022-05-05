using System;
using System.Collections.Generic;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='ExtendPublicClass']"
	[global::Java.Interop.JniTypeSignature ("xamarin/test/ExtendPublicClass", GenerateJavaPeer=false)]
	public partial class ExtendPublicClass : global::Java.Lang.Object {
		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/ExtendPublicClass", typeof (ExtendPublicClass));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected ExtendPublicClass (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='xamarin.test']/class[@name='ExtendPublicClass']/constructor[@name='ExtendPublicClass' and count(parameter)=0]"
		public unsafe ExtendPublicClass () : base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			const string __id = "()V";

			if (PeerReference.IsValid)
				return;

			try {
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), null);
				Construct (ref __r, JniObjectReferenceOptions.CopyAndDispose);
				_members.InstanceMethods.FinishCreateInstance (__id, this, null);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='ExtendPublicClass']/method[@name='foo' and count(parameter)=0]"
		[global::Java.Interop.JniMethodSignature ("foo", "()V")]
		public virtual unsafe void Foo ()
		{
			const string __id = "foo.()V";
			try {
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, null);
			} finally {
			}
		}

	}
}
