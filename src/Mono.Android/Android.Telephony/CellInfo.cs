using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

#if ANDROID_30

namespace Android.Telephony {

	public partial class CellInfo {

		static Delegate? cb_getCellIdentity;
#pragma warning disable 0169
		[global::System.Runtime.Versioning.SupportedOSPlatform ("android28.0")]
		static Delegate GetGetCellIdentityHandler ()
		{
			return cb_getCellIdentity ??= new _JniMarshal_PP_L (n_GetCellIdentity);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		[global::System.Runtime.Versioning.SupportedOSPlatform ("android28.0")]
		static IntPtr n_GetCellIdentity (IntPtr jnienv, IntPtr native__this)
		{
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				var __this = global::Java.Lang.Object.GetObject<Android.Telephony.CellInfo> (jnienv, native__this, JniHandleOwnership.DoNotTransfer)!;
				return JNIEnv.ToLocalJniHandle (__this.CellIdentity);
			} catch (global::System.Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}
#pragma warning restore 0169

		[global::System.Runtime.Versioning.SupportedOSPlatform ("android28.0")]
		public unsafe virtual Android.Telephony.CellIdentity CellIdentity {
			// Metadata.xml XPath method reference: path="/api/package[@name='android.telephony']/class[@name='CellInfo']/method[@name='getCellIdentity' and count(parameter)=0]"
			[Register ("getCellIdentity", "()Landroid/telephony/CellIdentity;", "GetGetCellIdentityHandler", ApiSince = 30)]
			get {
				const string __id = "getCellIdentity.()Landroid/telephony/CellIdentity;";
				try {
					var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
					return global::Java.Lang.Object.GetObject<Android.Telephony.CellIdentity> (__rm.Handle, JniHandleOwnership.TransferLocalRef)!;
				}
		                catch (Java.Lang.NoSuchMethodError) {
					throw new Java.Lang.AbstractMethodError (__id);
				}
			}
		}

		static Delegate? cb_getCellSignalStrength;
#pragma warning disable 0169
		static Delegate GetGetCellSignalStrengthHandler ()
		{
			return cb_getCellSignalStrength ??= new _JniMarshal_PP_L (n_GetCellSignalStrength);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		static IntPtr n_GetCellSignalStrength (IntPtr jnienv, IntPtr native__this)
		{
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				var __this = global::Java.Lang.Object.GetObject<Android.Telephony.CellInfo> (jnienv, native__this, JniHandleOwnership.DoNotTransfer)!;
				return JNIEnv.ToLocalJniHandle (__this.CellSignalStrength);
			} catch (global::System.Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}
#pragma warning restore 0169

		public unsafe virtual Android.Telephony.CellSignalStrength CellSignalStrength {
			// Metadata.xml XPath method reference: path="/api/package[@name='android.telephony']/class[@name='CellInfo']/method[@name='getCellSignalStrength' and count(parameter)=0]"
			[Register ("getCellSignalStrength", "()Landroid/telephony/CellSignalStrength;", "GetGetCellSignalStrengthHandler", ApiSince = 30)]
			get {
				const string __id = "getCellSignalStrength.()Landroid/telephony/CellSignalStrength;";
				try {
					var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
					return global::Java.Lang.Object.GetObject<Android.Telephony.CellSignalStrength> (__rm.Handle, JniHandleOwnership.TransferLocalRef)!;
				}
				catch (Java.Lang.NoSuchMethodError) {
					throw new Java.Lang.AbstractMethodError (__id);
				}
			}
		}
	}
}

#endif  // ANDROID_30
