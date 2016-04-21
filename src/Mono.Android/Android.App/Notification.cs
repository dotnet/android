using System;
using System.Collections.Generic;

using Android.Runtime;

namespace Android.App {

	partial class Notification {

		public Notification (int icon, string tickerText) : this (icon, tickerText, Java.Lang.JavaSystem.CurrentTimeMillis ()) {}

		public Notification (int icon, Java.Lang.ICharSequence tickerText) : this (icon, tickerText, Java.Lang.JavaSystem.CurrentTimeMillis ()) {}

		static IntPtr vibrate_jfieldId;
		[Register ("vibrate")]
		public long[] Vibrate {
			get {
				if (vibrate_jfieldId == IntPtr.Zero)
					vibrate_jfieldId = JNIEnv.GetFieldID (class_ref, "vibrate", "[J");
				return (long[]) JNIEnv.GetArray (JNIEnv.GetObjectField (Handle, vibrate_jfieldId), JniHandleOwnership.TransferLocalRef, typeof (long));
			}
			set {
				if (vibrate_jfieldId == IntPtr.Zero)
					vibrate_jfieldId = JNIEnv.GetFieldID (class_ref, "vibrate", "[J");
				IntPtr native_pattern = JNIEnv.NewArray (value);
				JNIEnv.SetField (Handle, vibrate_jfieldId, native_pattern);
				JNIEnv.DeleteLocalRef (native_pattern);
			}
		}
	}
}


