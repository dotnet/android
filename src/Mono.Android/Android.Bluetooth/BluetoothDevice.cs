#if ANDROID_26

namespace Android.Bluetooth
{
	public sealed partial class BluetoothDevice
	{
		// These methods were obsoleted as a warning in API-31
		[global::System.Obsolete ("This method has the wrong enumeration. Use the version that takes an 'Android.Bluetooth.BluetoothPhy' instead.")]
		[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
		public unsafe Android.Bluetooth.BluetoothGatt? ConnectGatt (Android.Content.Context? context, bool autoConnect, Android.Bluetooth.BluetoothGattCallback? @callback, [global::Android.Runtime.GeneratedEnum] Android.Bluetooth.BluetoothTransports transport, [global::Android.Runtime.GeneratedEnum] Android.Bluetooth.LE.ScanSettingsPhy phy)
			=> ConnectGatt (context, autoConnect, @callback, transport, (Android.Bluetooth.BluetoothPhy) phy);

		[global::System.Obsolete ("This method has the wrong enumeration. Use the version that takes an 'Android.Bluetooth.BluetoothPhy' instead.")]
		[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
		public unsafe Android.Bluetooth.BluetoothGatt? ConnectGatt (Android.Content.Context? context, bool autoConnect, Android.Bluetooth.BluetoothGattCallback? @callback, [global::Android.Runtime.GeneratedEnum] Android.Bluetooth.BluetoothTransports transport, [global::Android.Runtime.GeneratedEnum] Android.Bluetooth.LE.ScanSettingsPhy phy, Android.OS.Handler? handler)
			=> ConnectGatt (context, autoConnect, @callback, transport, (Android.Bluetooth.BluetoothPhy) phy, handler);
	}
}

#endif

