#if ANDROID_18

namespace Android.Bluetooth
{
	public partial class BluetoothGattServer
	{
		public bool SendResponse (BluetoothDevice device, int requestId, Android.Bluetooth.GattStatus status, int offset, byte [] value)
		{
			return SendResponse (device, requestId, (ProfileState) status, offset, value);
		}
	}
}

#endif

