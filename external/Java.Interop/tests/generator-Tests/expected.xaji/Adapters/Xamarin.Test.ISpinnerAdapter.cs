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
