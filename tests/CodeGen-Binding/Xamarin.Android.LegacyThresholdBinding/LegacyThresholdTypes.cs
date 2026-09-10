using System;

using Android.Runtime;

using Java.Interop;

namespace Xamarin.Android.LegacyThresholdBinding {

	[Register ("com/xamarin/android/LegacyThresholdBase", DoNotGenerateAcw = true)]
	public class LegacyThresholdBase : Java.Lang.Object {

		static readonly JniPeerMembers _members = new XAPeerMembers ("com/xamarin/android/LegacyThresholdBase", typeof (LegacyThresholdBase));

		public override JniPeerMembers JniPeerMembers => _members;

		protected override IntPtr ThresholdClass => _members.JniPeerType.PeerReference.Handle;

		protected override Type ThresholdType => _members.ManagedPeerType;

		protected LegacyThresholdBase (IntPtr javaReference, JniHandleOwnership transfer)
			: base (javaReference, transfer)
		{
		}

		public unsafe LegacyThresholdBase ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string id = "()V";

			if (Handle != IntPtr.Zero)
				return;

			var reference = _members.InstanceMethods.StartCreateInstance (id, GetType (), null);
			SetHandle (reference.Handle, JniHandleOwnership.TransferLocalRef);
			_members.InstanceMethods.FinishCreateInstance (id, this, null);
		}

		public virtual unsafe void Method ()
		{
			_members.InstanceMethods.InvokeVirtualVoidMethod ("method.()V", this, null);
		}

		public bool MethodInvoked => _members.InstanceFields.GetBooleanValue ("methodInvoked.Z", this);
	}

	[Register ("com/xamarin/android/LegacyThresholdDerived", DoNotGenerateAcw = true)]
	public class LegacyThresholdDerived : LegacyThresholdBase {

		static readonly JniPeerMembers _members = new XAPeerMembers ("com/xamarin/android/LegacyThresholdDerived", typeof (LegacyThresholdDerived));

		public override JniPeerMembers JniPeerMembers => _members;

		protected override IntPtr ThresholdClass => _members.JniPeerType.PeerReference.Handle;

		protected override Type ThresholdType => _members.ManagedPeerType;

		protected LegacyThresholdDerived (IntPtr javaReference, JniHandleOwnership transfer)
			: base (javaReference, transfer)
		{
		}

		public unsafe LegacyThresholdDerived ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string id = "()V";

			if (Handle != IntPtr.Zero)
				return;

			var reference = _members.InstanceMethods.StartCreateInstance (id, GetType (), null);
			SetHandle (reference.Handle, JniHandleOwnership.TransferLocalRef);
			_members.InstanceMethods.FinishCreateInstance (id, this, null);
		}

		public bool DerivedMethodInvoked => _members.InstanceFields.GetBooleanValue ("derivedMethodInvoked.Z", this);
	}
}
