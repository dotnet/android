#if ANDROID_18
using System;
using Android.Runtime;

namespace Android.Bluetooth
{
	// Proposed
	partial class BluetoothGattServerCallback
	{
		// Old and busted
		[Obsolete ("Use OnServiceAdded(Android.Bluetooth.GattStatus, Android.Bluetooth.BluetoothGattService)", true)]
		[Register ("onServiceAdded", "(ILandroid/bluetooth/BluetoothGattService;)V", "GetOnServiceAdded_ILandroid_bluetooth_BluetoothGattService_Handler_ext")]
		public virtual void OnServiceAdded (ProfileState status, BluetoothGattService service)
		{
			ActualOnServiceAdded (status, service);
		}

		void ActualOnServiceAdded (ProfileState status, BluetoothGattService? service)
		{
			this.OnServiceAdded ((GattStatus) (int) status, service);
		}

		static Delegate? cb_onServiceAdded_ILandroid_bluetooth_BluetoothGattService_ext;
#pragma warning disable 0169
		static Delegate GetOnServiceAdded_ILandroid_bluetooth_BluetoothGattService_Handler_ext ()
		{
			return cb_onServiceAdded_ILandroid_bluetooth_BluetoothGattService_ext ??= new _JniMarshal_PPIL_V (n_OnServiceAdded_ILandroid_bluetooth_BluetoothGattService_ext);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		static void n_OnServiceAdded_ILandroid_bluetooth_BluetoothGattService_ext (IntPtr jnienv, IntPtr native__this, int native_status, IntPtr native_service)
		{
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				Android.Bluetooth.BluetoothGattServerCallback __this = global::Java.Lang.Object.GetObject<Android.Bluetooth.BluetoothGattServerCallback> (jnienv, native__this, JniHandleOwnership.DoNotTransfer)!;
				Android.Bluetooth.ProfileState status = (Android.Bluetooth.ProfileState) native_status;
				var service = global::Java.Lang.Object.GetObject<Android.Bluetooth.BluetoothGattService> (native_service, JniHandleOwnership.DoNotTransfer);
				__this.ActualOnServiceAdded (status, service);
			} catch (global::System.Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return;
			} finally {
				global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}
#pragma warning restore 0169
	}
}
#endif
