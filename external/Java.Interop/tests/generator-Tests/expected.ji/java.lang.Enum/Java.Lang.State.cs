using System;
using System.Collections.Generic;
using Java.Interop;

namespace Java.Lang {

	// Metadata.xml XPath class reference: path="/api/package[@name='java.lang']/class[@name='State']"
	[global::Java.Interop.JniTypeSignature ("java/lang/State", GenerateJavaPeer=false)]
	public sealed partial class State : global::Java.Lang.Enum {

		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='BLOCKED']"
		public static global::Java.Lang.State Blocked {
			get {
				const string __id = "BLOCKED.Ljava/lang/State;";

				var __v = _members.StaticFields.GetObjectValue (__id);
				return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::Java.Lang.State> (ref __v.Handle, JniObjectReferenceOptions.CopyAndDispose);
			}
		}


		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='NEW']"
		public static global::Java.Lang.State New {
			get {
				const string __id = "NEW.Ljava/lang/State;";

				var __v = _members.StaticFields.GetObjectValue (__id);
				return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::Java.Lang.State> (ref __v.Handle, JniObjectReferenceOptions.CopyAndDispose);
			}
		}


		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='RUNNABLE']"
		public static global::Java.Lang.State Runnable {
			get {
				const string __id = "RUNNABLE.Ljava/lang/State;";

				var __v = _members.StaticFields.GetObjectValue (__id);
				return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::Java.Lang.State> (ref __v.Handle, JniObjectReferenceOptions.CopyAndDispose);
			}
		}


		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='TERMINATED']"
		public static global::Java.Lang.State Terminated {
			get {
				const string __id = "TERMINATED.Ljava/lang/State;";

				var __v = _members.StaticFields.GetObjectValue (__id);
				return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::Java.Lang.State> (ref __v.Handle, JniObjectReferenceOptions.CopyAndDispose);
			}
		}


		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='TIMED_WAITING']"
		public static global::Java.Lang.State TimedWaiting {
			get {
				const string __id = "TIMED_WAITING.Ljava/lang/State;";

				var __v = _members.StaticFields.GetObjectValue (__id);
				return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::Java.Lang.State> (ref __v.Handle, JniObjectReferenceOptions.CopyAndDispose);
			}
		}


		// Metadata.xml XPath field reference: path="/api/package[@name='java.lang']/class[@name='State']/field[@name='WAITING']"
		public static global::Java.Lang.State Waiting {
			get {
				const string __id = "WAITING.Ljava/lang/State;";

				var __v = _members.StaticFields.GetObjectValue (__id);
				return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::Java.Lang.State> (ref __v.Handle, JniObjectReferenceOptions.CopyAndDispose);
			}
		}

		static readonly JniPeerMembers _members = new JniPeerMembers ("java/lang/State", typeof (State));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		internal State (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

	}
}
