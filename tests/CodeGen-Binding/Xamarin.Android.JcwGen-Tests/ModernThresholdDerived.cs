using System;

using Android.Runtime;

using Java.Interop;

namespace Xamarin.Android.JcwGenTests {

	[Register ("com/xamarin/android/ModernThresholdDerived", DoNotGenerateAcw = true)]
	public class ModernThresholdDerived : LegacyThresholdBinding.LegacyThresholdDerived {

		static readonly JniPeerMembers _members = new JniPeerMembers ("com/xamarin/android/ModernThresholdDerived", typeof (ModernThresholdDerived));

		public override JniPeerMembers JniPeerMembers => _members;

		protected ModernThresholdDerived (IntPtr javaReference, JniHandleOwnership transfer)
			: base (javaReference, transfer)
		{
		}

		public unsafe ModernThresholdDerived ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string id = "()V";

			if (Handle != IntPtr.Zero)
				return;

			var reference = _members.InstanceMethods.StartCreateInstance (id, GetType (), null);
			SetHandle (reference.Handle, JniHandleOwnership.TransferLocalRef);
			_members.InstanceMethods.FinishCreateInstance (id, this, null);
		}

		public bool ModernMethodInvoked => _members.InstanceFields.GetBooleanValue ("modernMethodInvoked.Z", this);
	}

	public class ManagedModernThresholdDerived : ModernThresholdDerived {
	}
}
