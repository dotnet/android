using System;
using System.Collections.Generic;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='NotificationCompatBase']"
	[global::Java.Interop.JniTypeSignature ("xamarin/test/NotificationCompatBase", GenerateJavaPeer=false)]
	public partial class NotificationCompatBase : global::Java.Lang.Object {
		// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='NotificationCompatBase.Action']"
		[global::Java.Interop.JniTypeSignature ("xamarin/test/NotificationCompatBase$Action", GenerateJavaPeer=false)]
		public abstract partial class Action : global::Java.Lang.Object {
			// Metadata.xml XPath interface reference: path="/api/package[@name='xamarin.test']/interface[@name='NotificationCompatBase.Action.Factory']"
			[global::Java.Interop.JniTypeSignature ("xamarin/test/NotificationCompatBase$Action$Factory", GenerateJavaPeer=false)]
			public partial interface IFactory : IJavaPeerable {
				// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/interface[@name='NotificationCompatBase.Action.Factory']/method[@name='build' and count(parameter)=1 and parameter[1][@type='int']]"
				global::Xamarin.Test.NotificationCompatBase.Action Build (int p0);

			}

			static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/NotificationCompatBase$Action", typeof (Action));

			[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
			[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
			public override global::Java.Interop.JniPeerMembers JniPeerMembers {
				get { return _members; }
			}

			protected Action (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
			{
			}

		}

		[global::Java.Interop.JniTypeSignature ("xamarin/test/NotificationCompatBase$Action", GenerateJavaPeer=false)]
		internal partial class ActionInvoker : Action {
			public ActionInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
			{
			}

			static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/NotificationCompatBase$Action", typeof (ActionInvoker));

			[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
			[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
			public override global::Java.Interop.JniPeerMembers JniPeerMembers {
				get { return _members; }
			}

		}

		// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='NotificationCompatBase.InstanceInner']"
		[global::Java.Interop.JniTypeSignature ("xamarin/test/NotificationCompatBase$InstanceInner", GenerateJavaPeer=false)]
		public abstract partial class InstanceInner : global::Java.Lang.Object {
			static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/NotificationCompatBase$InstanceInner", typeof (InstanceInner));

			[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
			[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
			public override global::Java.Interop.JniPeerMembers JniPeerMembers {
				get { return _members; }
			}

			protected InstanceInner (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
			{
			}

			// Metadata.xml XPath constructor reference: path="/api/package[@name='xamarin.test']/class[@name='NotificationCompatBase.InstanceInner']/constructor[@name='NotificationCompatBase.InstanceInner' and count(parameter)=1 and parameter[1][@type='xamarin.test.NotificationCompatBase']]"
			public unsafe InstanceInner (global::Xamarin.Test.NotificationCompatBase __self) : base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
			{
				string __id = "(L" + global::Android.Runtime.JNIEnv.GetJniName (GetType ().DeclaringType) + ";)V";

				if (PeerReference.IsValid)
					return;

				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (__self);
					var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), __args);
					Construct (ref __r, JniObjectReferenceOptions.CopyAndDispose);
					_members.InstanceMethods.FinishCreateInstance (__id, this, __args);
				} finally {
					global::System.GC.KeepAlive (__self);
				}
			}

		}

		[global::Java.Interop.JniTypeSignature ("xamarin/test/NotificationCompatBase$InstanceInner", GenerateJavaPeer=false)]
		internal partial class InstanceInnerInvoker : InstanceInner {
			public InstanceInnerInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
			{
			}

			static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/NotificationCompatBase$InstanceInner", typeof (InstanceInnerInvoker));

			[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
			[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
			public override global::Java.Interop.JniPeerMembers JniPeerMembers {
				get { return _members; }
			}

		}

		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/NotificationCompatBase", typeof (NotificationCompatBase));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected NotificationCompatBase (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

	}
}
