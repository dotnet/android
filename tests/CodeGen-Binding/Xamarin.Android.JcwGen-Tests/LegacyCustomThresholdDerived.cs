using System;

using Android.Runtime;

using Java.Interop;

namespace Xamarin.Android.JcwGenTests {

	public class LegacyCustomThresholdDerived : LegacyThresholdBinding.LegacyThresholdDerived {

		static readonly JniPeerMembers baseMembers = new XAPeerMembers (
			"com/xamarin/android/LegacyThresholdBase", typeof (LegacyThresholdBinding.LegacyThresholdBase));

		protected override Type ThresholdType => typeof (LegacyThresholdBinding.LegacyThresholdBase);
		protected override IntPtr ThresholdClass => baseMembers.JniPeerType.PeerReference.Handle;
	}
}
