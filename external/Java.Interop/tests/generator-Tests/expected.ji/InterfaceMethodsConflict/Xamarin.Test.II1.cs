using System;
using System.Collections.Generic;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath interface reference: path="/api/package[@name='xamarin.test']/interface[@name='I1']"
	[global::Java.Interop.JniTypeSignature ("xamarin/test/I1", GenerateJavaPeer=false, InvokerType=typeof (Xamarin.Test.II1Invoker))]
	public partial interface II1 : IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/interface[@name='I1']/method[@name='close' and count(parameter)=0]"
		[global::Java.Interop.JniMethodSignature ("close", "()V")]
		void Close ();

	}

	[global::Java.Interop.JniTypeSignature ("xamarin/test/I1", GenerateJavaPeer=false)]
	internal partial class II1Invoker : global::Java.Lang.Object, II1 {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_xamarin_test_I1; }
		}

		static readonly JniPeerMembers _members_xamarin_test_I1 = new JniPeerMembers ("xamarin/test/I1", typeof (II1Invoker));

		public II1Invoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		public unsafe void Close ()
		{
			const string __id = "close.()V";
			try {
				_members_xamarin_test_I1.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			} finally {
			}
		}

	}
}
