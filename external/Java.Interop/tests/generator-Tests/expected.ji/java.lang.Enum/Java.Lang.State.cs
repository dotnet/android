using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='State']"
	[global::Android.Runtime.Register ("java/lang/State", DoNotGenerateAcw=true)]
	public sealed partial class State : global::Java.Lang.Enum {



		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='BLOCKED']"
		[Register ("BLOCKED")]
		public static global::Java.Lang.State Blocked {
			get {
				const string __id = "BLOCKED.Ljava/lang/State;";

				var __v = _members.StaticFields.GetObjectValue (__id);
				return global::Java.Lang.Object.GetObject<global::Java.Lang.State> (__v.Handle, JniHandleOwnership.TransferLocalRef);
			}
		}


		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='NEW']"
		[Register ("NEW")]
		public static global::Java.Lang.State New {
			get {
				const string __id = "NEW.Ljava/lang/State;";

				var __v = _members.StaticFields.GetObjectValue (__id);
				return global::Java.Lang.Object.GetObject<global::Java.Lang.State> (__v.Handle, JniHandleOwnership.TransferLocalRef);
			}
		}


		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='RUNNABLE']"
		[Register ("RUNNABLE")]
		public static global::Java.Lang.State Runnable {
			get {
				const string __id = "RUNNABLE.Ljava/lang/State;";

				var __v = _members.StaticFields.GetObjectValue (__id);
				return global::Java.Lang.Object.GetObject<global::Java.Lang.State> (__v.Handle, JniHandleOwnership.TransferLocalRef);
			}
		}


		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='TERMINATED']"
		[Register ("TERMINATED")]
		public static global::Java.Lang.State Terminated {
			get {
				const string __id = "TERMINATED.Ljava/lang/State;";

				var __v = _members.StaticFields.GetObjectValue (__id);
				return global::Java.Lang.Object.GetObject<global::Java.Lang.State> (__v.Handle, JniHandleOwnership.TransferLocalRef);
			}
		}


		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='TIMED_WAITING']"
		[Register ("TIMED_WAITING")]
		public static global::Java.Lang.State TimedWaiting {
			get {
				const string __id = "TIMED_WAITING.Ljava/lang/State;";

				var __v = _members.StaticFields.GetObjectValue (__id);
				return global::Java.Lang.Object.GetObject<global::Java.Lang.State> (__v.Handle, JniHandleOwnership.TransferLocalRef);
			}
		}


		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='WAITING']"
		[Register ("WAITING")]
		public static global::Java.Lang.State Waiting {
			get {
				const string __id = "WAITING.Ljava/lang/State;";

				var __v = _members.StaticFields.GetObjectValue (__id);
				return global::Java.Lang.Object.GetObject<global::Java.Lang.State> (__v.Handle, JniHandleOwnership.TransferLocalRef);
			}
		}
		static readonly JniPeerMembers _members = new JniPeerMembers ("java/lang/State", typeof (State));
		internal static new IntPtr class_ref {
			get {
				return _members.JniPeerType.PeerReference.Handle;
			}
		}

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override IntPtr ThresholdClass {
			get { return _members.JniPeerType.PeerReference.Handle; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

		internal State (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

	}
}
