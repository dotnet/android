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
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath interface reference: path="/api/package[@name='xamarin.test']/interface[@name='SpinnerAdapter']"
	[Register ("xamarin/test/SpinnerAdapter", "", "Xamarin.Test.ISpinnerAdapterInvoker")]
	public partial interface ISpinnerAdapter : global::Xamarin.Test.IAdapter {
	}

	[global::Android.Runtime.Register ("xamarin/test/SpinnerAdapter", DoNotGenerateAcw=true)]
	internal partial class ISpinnerAdapterInvoker : global::Java.Lang.Object, ISpinnerAdapter {
		static IntPtr java_class_ref {
			get { return _members_xamarin_test_SpinnerAdapter.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_xamarin_test_SpinnerAdapter; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members_xamarin_test_SpinnerAdapter.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members_xamarin_test_SpinnerAdapter.ManagedPeerType; }
		}

		static readonly JniPeerMembers _members_xamarin_test_Adapter = new XAPeerMembers ("xamarin/test/Adapter", typeof (ISpinnerAdapterInvoker));

		static readonly JniPeerMembers _members_xamarin_test_SpinnerAdapter = new XAPeerMembers ("xamarin/test/SpinnerAdapter", typeof (ISpinnerAdapterInvoker));

		public ISpinnerAdapterInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

	}
}
