using System;
using System.Collections.Generic;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath interface reference: path="/api/package[@name='xamarin.test']/interface[@name='SpinnerAdapter']"
	[global::Java.Interop.JniTypeSignature ("xamarin/test/SpinnerAdapter", GenerateJavaPeer=false)]
	public partial interface ISpinnerAdapter : global::Xamarin.Test.IAdapter {
	}

	[global::Java.Interop.JniTypeSignature ("xamarin/test/SpinnerAdapter", GenerateJavaPeer=false)]
	internal partial class ISpinnerAdapterInvoker : global::Java.Lang.Object, ISpinnerAdapter {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_xamarin_test_SpinnerAdapter; }
		}

		static readonly JniPeerMembers _members_xamarin_test_Adapter = new JniPeerMembers ("xamarin/test/Adapter", typeof (ISpinnerAdapterInvoker));

		static readonly JniPeerMembers _members_xamarin_test_SpinnerAdapter = new JniPeerMembers ("xamarin/test/SpinnerAdapter", typeof (ISpinnerAdapterInvoker));

		public ISpinnerAdapterInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

	}
}
