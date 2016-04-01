using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='State']"
	[global::Android.Runtime.Register ("java/lang/State", DoNotGenerateAcw=true)]
	public sealed partial class State : global::Java.Lang.Enum {


		static IntPtr BLOCKED_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='BLOCKED']"
		[Register ("BLOCKED")]
		public static global::Java.Lang.State Blocked {
			get {
				if (BLOCKED_jfieldId == IntPtr.Zero)
					BLOCKED_jfieldId = JNIEnv.GetStaticFieldID (class_ref, "BLOCKED", "Ljava/lang/State;");
				IntPtr __ret = JNIEnv.GetStaticObjectField (class_ref, BLOCKED_jfieldId);
				return global::Java.Lang.Object.GetObject<global::Java.Lang.State> (__ret, JniHandleOwnership.TransferLocalRef);
			}
		}

		static IntPtr NEW_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='NEW']"
		[Register ("NEW")]
		public static global::Java.Lang.State New {
			get {
				if (NEW_jfieldId == IntPtr.Zero)
					NEW_jfieldId = JNIEnv.GetStaticFieldID (class_ref, "NEW", "Ljava/lang/State;");
				IntPtr __ret = JNIEnv.GetStaticObjectField (class_ref, NEW_jfieldId);
				return global::Java.Lang.Object.GetObject<global::Java.Lang.State> (__ret, JniHandleOwnership.TransferLocalRef);
			}
		}

		static IntPtr RUNNABLE_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='RUNNABLE']"
		[Register ("RUNNABLE")]
		public static global::Java.Lang.State Runnable {
			get {
				if (RUNNABLE_jfieldId == IntPtr.Zero)
					RUNNABLE_jfieldId = JNIEnv.GetStaticFieldID (class_ref, "RUNNABLE", "Ljava/lang/State;");
				IntPtr __ret = JNIEnv.GetStaticObjectField (class_ref, RUNNABLE_jfieldId);
				return global::Java.Lang.Object.GetObject<global::Java.Lang.State> (__ret, JniHandleOwnership.TransferLocalRef);
			}
		}

		static IntPtr TERMINATED_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='TERMINATED']"
		[Register ("TERMINATED")]
		public static global::Java.Lang.State Terminated {
			get {
				if (TERMINATED_jfieldId == IntPtr.Zero)
					TERMINATED_jfieldId = JNIEnv.GetStaticFieldID (class_ref, "TERMINATED", "Ljava/lang/State;");
				IntPtr __ret = JNIEnv.GetStaticObjectField (class_ref, TERMINATED_jfieldId);
				return global::Java.Lang.Object.GetObject<global::Java.Lang.State> (__ret, JniHandleOwnership.TransferLocalRef);
			}
		}

		static IntPtr TIMED_WAITING_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='TIMED_WAITING']"
		[Register ("TIMED_WAITING")]
		public static global::Java.Lang.State TimedWaiting {
			get {
				if (TIMED_WAITING_jfieldId == IntPtr.Zero)
					TIMED_WAITING_jfieldId = JNIEnv.GetStaticFieldID (class_ref, "TIMED_WAITING", "Ljava/lang/State;");
				IntPtr __ret = JNIEnv.GetStaticObjectField (class_ref, TIMED_WAITING_jfieldId);
				return global::Java.Lang.Object.GetObject<global::Java.Lang.State> (__ret, JniHandleOwnership.TransferLocalRef);
			}
		}

		static IntPtr WAITING_jfieldId;

		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='WAITING']"
		[Register ("WAITING")]
		public static global::Java.Lang.State Waiting {
			get {
				if (WAITING_jfieldId == IntPtr.Zero)
					WAITING_jfieldId = JNIEnv.GetStaticFieldID (class_ref, "WAITING", "Ljava/lang/State;");
				IntPtr __ret = JNIEnv.GetStaticObjectField (class_ref, WAITING_jfieldId);
				return global::Java.Lang.Object.GetObject<global::Java.Lang.State> (__ret, JniHandleOwnership.TransferLocalRef);
			}
		}
		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("java/lang/State", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (State); }
		}

		internal State (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

	}
}
