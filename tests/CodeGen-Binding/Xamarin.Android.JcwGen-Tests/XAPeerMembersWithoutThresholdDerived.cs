using System;

using Android.Runtime;

using Java.Interop;

namespace Xamarin.Android.JcwGenTests {

	[Register ("com/xamarin/android/XAPeerMembersWithoutThresholdDerived", DoNotGenerateAcw = true)]
	public class XAPeerMembersWithoutThresholdDerived : LegacyThresholdBinding.LegacyThresholdDerived {

		static readonly JniPeerMembers _members = new XAPeerMembers ("com/xamarin/android/XAPeerMembersWithoutThresholdDerived", typeof (XAPeerMembersWithoutThresholdDerived));

		public override JniPeerMembers JniPeerMembers => _members;

		protected XAPeerMembersWithoutThresholdDerived (IntPtr javaReference, JniHandleOwnership transfer)
			: base (javaReference, transfer)
		{
		}

		public unsafe XAPeerMembersWithoutThresholdDerived ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string id = "()V";

			if (Handle != IntPtr.Zero)
				return;

			var reference = _members.InstanceMethods.StartCreateInstance (id, GetType (), null);
			SetHandle (reference.Handle, JniHandleOwnership.TransferLocalRef);
			_members.InstanceMethods.FinishCreateInstance (id, this, null);
		}

		public bool MethodInvokedWithoutThreshold => _members.InstanceFields.GetBooleanValue ("methodInvokedWithoutThreshold.Z", this);
	}

	public class ManagedXAPeerMembersWithoutThresholdDerived : XAPeerMembersWithoutThresholdDerived {
	}
}
