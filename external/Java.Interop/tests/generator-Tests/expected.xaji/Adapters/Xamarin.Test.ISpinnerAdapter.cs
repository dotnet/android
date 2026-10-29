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
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_xamarin_test_SpinnerAdapter; }
		}

		static readonly JniPeerMembers _members_xamarin_test_Adapter = new JniPeerMembers ("xamarin/test/Adapter", typeof (ISpinnerAdapterInvoker));

		static readonly JniPeerMembers _members_xamarin_test_SpinnerAdapter = new JniPeerMembers ("xamarin/test/SpinnerAdapter", typeof (ISpinnerAdapterInvoker));

		/// <safety>
		/// handle must be zero or a live JNI object implementing this interface. Use an attached
		/// thread (locals: creating thread), with no concurrent deletion through aliases during
		/// construction. transfer must match ownership; transferred references cannot be used again,
		/// even if construction throws.
		/// </safety>
		/// <remarks>
		/// This constructs a JNI peer, not an opaque-address container. RequiresUnsafe enforcement
		/// is deferred to preserve safe zero/DoNotTransfer base initializers; the raw-reference
		/// obligations still apply.
		/// </remarks>
		public ISpinnerAdapterInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

	}
}
