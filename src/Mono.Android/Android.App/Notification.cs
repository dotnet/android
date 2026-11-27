using System;
using System.Collections.Generic;

using Android.Runtime;

namespace Android.App {

	partial class Notification {

		public Notification (int icon, string tickerText) : this (icon, tickerText, Java.Lang.JavaSystem.CurrentTimeMillis ()) {}

#pragma warning disable CS0618 // Preserve the legacy ICharSequence constructor.
		public Notification (int icon, Java.Lang.ICharSequence tickerText) : this (icon, tickerText, Java.Lang.JavaSystem.CurrentTimeMillis ()) {}
#pragma warning restore CS0618

		static IntPtr vibrate_jfieldId;
		[Register ("vibrate")]
		public long[] Vibrate {
			get {
				unsafe {
					// SAFETY: vibrate is the long-array instance field; GC.KeepAlive (this) retains
					// the peer across field access and the returned local reference transfers to GetArray.
					if (vibrate_jfieldId == IntPtr.Zero)
						vibrate_jfieldId = JNIEnv.GetFieldID (_members.GetPeerTypeHandle (), "vibrate", "[J");
					IntPtr lref = JNIEnv.GetObjectField (Handle, vibrate_jfieldId);
					GC.KeepAlive (this);
					return (long[]) JNIEnv.GetArray (lref, JniHandleOwnership.TransferLocalRef, typeof (long))!;
				}
			}
			set {
				unsafe {
					// SAFETY: vibrate is the long-array instance field; this peer and the locally allocated array reference are retained until finally.
					if (vibrate_jfieldId == IntPtr.Zero)
						vibrate_jfieldId = JNIEnv.GetFieldID (_members.GetPeerTypeHandle (), "vibrate", "[J");
					IntPtr native_pattern = JNIEnv.NewArray (value);
					try {
						JNIEnv.SetField (Handle, vibrate_jfieldId, native_pattern);
					} finally {
						JNIEnv.DeleteLocalRef (native_pattern);
						GC.KeepAlive (this);
					}
				}
			}
		}
	}
}
