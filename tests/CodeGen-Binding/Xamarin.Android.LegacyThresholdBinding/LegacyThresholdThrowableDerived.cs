using System;

using Android.Runtime;

using Java.Interop;

namespace Xamarin.Android.LegacyThresholdBinding {

	[Register ("com/xamarin/android/LegacyThresholdThrowableDerived", DoNotGenerateAcw = true)]
	public class LegacyThresholdThrowableDerived : LegacyThresholdThrowable {

		static readonly JniPeerMembers _members = new XAPeerMembers ("com/xamarin/android/LegacyThresholdThrowableDerived", typeof (LegacyThresholdThrowableDerived));

		public override JniPeerMembers JniPeerMembers => _members;

		protected override IntPtr ThresholdClass => _members.JniPeerType.PeerReference.Handle;

		protected override Type ThresholdType => _members.ManagedPeerType;

		protected LegacyThresholdThrowableDerived (IntPtr javaReference, JniHandleOwnership transfer)
			: base (javaReference, transfer)
		{
		}

		public LegacyThresholdThrowableDerived ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string id = "()V";

			if (Handle != IntPtr.Zero)
				return;

			var reference = _members.InstanceMethods.StartCreateInstance (id, GetType (), null);
			SetHandle (reference.Handle, JniHandleOwnership.TransferLocalRef);
			// SAFETY: The ()V constructor takes no arguments; this owns the new peer.
			unsafe {
				_members.InstanceMethods.FinishCreateInstance (id, this, null);
			}
		}

		public bool DerivedMethodInvoked => _members.InstanceFields.GetBooleanValue ("derivedMethodInvoked.Z", this);
	}
}
