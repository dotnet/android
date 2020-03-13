using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

#if ANDROID_30

namespace Android.Telephony {

	public partial class CellInfo {

		static Delegate cb_getCellIdentity;
#pragma warning disable 0169
		static Delegate GetGetCellIdentityHandler ()
		{
			if (cb_getCellIdentity == null)
				cb_getCellIdentity = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr>) n_GetCellIdentity);
			return cb_getCellIdentity;
		}

		static IntPtr n_GetCellIdentity (IntPtr jnienv, IntPtr native__this)
		{
			Android.Telephony.CellInfo __this = global::Java.Lang.Object.GetObject<Android.Telephony.CellInfo> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToLocalJniHandle (__this.CellIdentity);
		}
#pragma warning restore 0169

		public unsafe virtual Android.Telephony.CellIdentity CellIdentity {
			// Metadata.xml XPath method reference: path="/api/package[@name='android.telephony']/class[@name='CellInfo']/method[@name='getCellIdentity' and count(parameter)=0]"
			[Register ("getCellIdentity", "()Landroid/telephony/CellIdentity;", "GetGetCellIdentityHandler", ApiSince = 30)]
			get {
				const string __id = "getCellIdentity.()Landroid/telephony/CellIdentity;";
				try {
					var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
					return global::Java.Lang.Object.GetObject<Android.Telephony.CellIdentity> (__rm.Handle, JniHandleOwnership.TransferLocalRef);
				}
		                catch (Java.Lang.NoSuchMethodError) {
					throw new Java.Lang.AbstractMethodError (__id);
				}
			}
		}

		static Delegate cb_getCellSignalStrength;
#pragma warning disable 0169
		static Delegate GetGetCellSignalStrengthHandler ()
		{
			if (cb_getCellSignalStrength == null)
				cb_getCellSignalStrength = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr>) n_GetCellSignalStrength);
			return cb_getCellSignalStrength;
		}

		static IntPtr n_GetCellSignalStrength (IntPtr jnienv, IntPtr native__this)
		{
			Android.Telephony.CellInfo __this = global::Java.Lang.Object.GetObject<Android.Telephony.CellInfo> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToLocalJniHandle (__this.CellSignalStrength);
		}
#pragma warning restore 0169

		public unsafe virtual Android.Telephony.CellSignalStrength CellSignalStrength {
			// Metadata.xml XPath method reference: path="/api/package[@name='android.telephony']/class[@name='CellInfo']/method[@name='getCellSignalStrength' and count(parameter)=0]"
			[Register ("getCellSignalStrength", "()Landroid/telephony/CellSignalStrength;", "GetGetCellSignalStrengthHandler", ApiSince = 30)]
			get {
				const string __id = "getCellSignalStrength.()Landroid/telephony/CellSignalStrength;";
				try {
					var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
					return global::Java.Lang.Object.GetObject<Android.Telephony.CellSignalStrength> (__rm.Handle, JniHandleOwnership.TransferLocalRef);
				}
				catch (Java.Lang.NoSuchMethodError) {
					throw new Java.Lang.AbstractMethodError (__id);
				}
			}
		}
	}
}

#endif  // ANDROID_30
