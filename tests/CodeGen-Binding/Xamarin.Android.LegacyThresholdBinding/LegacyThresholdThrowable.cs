using System;

using Android.Runtime;

using Java.Interop;

namespace Xamarin.Android.LegacyThresholdBinding {

	[Register ("com/xamarin/android/LegacyThresholdThrowable", DoNotGenerateAcw = true)]
	public class LegacyThresholdThrowable : Java.Lang.Throwable {

		static readonly JniPeerMembers _members = new XAPeerMembers ("com/xamarin/android/LegacyThresholdThrowable", typeof (LegacyThresholdThrowable));

		public override JniPeerMembers JniPeerMembers => _members;

		protected override IntPtr ThresholdClass => _members.JniPeerType.PeerReference.Handle;

		protected override Type ThresholdType => _members.ManagedPeerType;

		protected LegacyThresholdThrowable (IntPtr javaReference, JniHandleOwnership transfer)
			: base (javaReference, transfer)
		{
		}

		public unsafe LegacyThresholdThrowable ()
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
}
