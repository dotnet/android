using System;
using System.Collections.Generic;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath interface reference: path="/api/package[@name='xamarin.test']/interface[@name='Adapter']"
	[global::Java.Interop.JniTypeSignature ("xamarin/test/Adapter", GenerateJavaPeer=false)]
	public partial interface IAdapter : IJavaPeerable {
	}

	[global::Java.Interop.JniTypeSignature ("xamarin/test/Adapter", GenerateJavaPeer=false)]
	internal partial class IAdapterInvoker : global::Java.Lang.Object, IAdapter {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_xamarin_test_Adapter; }
		}

		static readonly JniPeerMembers _members_xamarin_test_Adapter = new JniPeerMembers ("xamarin/test/Adapter", typeof (IAdapterInvoker));

		public IAdapterInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

	}
}
