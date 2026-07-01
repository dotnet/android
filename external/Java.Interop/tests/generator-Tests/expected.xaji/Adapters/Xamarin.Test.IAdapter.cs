using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath interface reference: path="/api/package[@name='xamarin.test']/interface[@name='Adapter']"
	[Register ("xamarin/test/Adapter", "", "Xamarin.Test.IAdapterInvoker")]
	public partial interface IAdapter : IJavaObject, IJavaPeerable {
	}

	[global::Android.Runtime.Register ("xamarin/test/Adapter", DoNotGenerateAcw=true)]
	internal partial class IAdapterInvoker : global::Java.Lang.Object, IAdapter {
		static IntPtr java_class_ref {
			get { return _members_xamarin_test_Adapter.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_xamarin_test_Adapter; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members_xamarin_test_Adapter.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members_xamarin_test_Adapter.ManagedPeerType; }
		}

		static readonly JniPeerMembers _members_xamarin_test_Adapter = new XAPeerMembers ("xamarin/test/Adapter", typeof (IAdapterInvoker));

		public IAdapterInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

	}
}
