#if ANDROID_26

namespace Android.Bluetooth.LE
{
	public sealed partial class AdvertisingSetParameters
	{
		public sealed partial class Builder
		{
			// These methods were obsoleted as a warning in API-31
			[global::System.Obsolete ("This method has the wrong enumeration. Use the version that takes an 'Android.Bluetooth.BluetoothPhy' instead.")]
			[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
			public unsafe Android.Bluetooth.LE.AdvertisingSetParameters.Builder? SetPrimaryPhy ([global::Android.Runtime.GeneratedEnum] Android.Bluetooth.LE.ScanSettingsPhy primaryPhy)
				=> SetPrimaryPhy ((Android.Bluetooth.BluetoothPhy) primaryPhy);

			[global::System.Obsolete ("This method has the wrong enumeration. Use the version that takes an 'Android.Bluetooth.BluetoothPhy' instead.")]
			[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
			public unsafe Android.Bluetooth.LE.AdvertisingSetParameters.Builder? SetSecondaryPhy ([global::Android.Runtime.GeneratedEnum] Android.Bluetooth.LE.ScanSettingsPhy secondaryPhy)
				=> SetSecondaryPhy ((Android.Bluetooth.BluetoothPhy) secondaryPhy);
		}
	}
}

#endif

