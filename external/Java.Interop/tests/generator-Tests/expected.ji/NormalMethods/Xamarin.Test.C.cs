using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='C']"
	[global::Android.Runtime.Register ("xamarin/test/C", DoNotGenerateAcw=true)]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T extends xamarin.test.C"})]
	public partial class C : global::Java.Lang.Object {

		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/C", typeof (C));
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

		protected C (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static Delegate cb_setCustomDimension_I;
#pragma warning disable 0169
		static Delegate GetSetCustomDimension_IHandler ()
		{
			if (cb_setCustomDimension_I == null)
				cb_setCustomDimension_I = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, int, IntPtr>) n_SetCustomDimension_I);
			return cb_setCustomDimension_I;
		}

		static IntPtr n_SetCustomDimension_I (IntPtr jnienv, IntPtr native__this, int index)
		{
			global::Xamarin.Test.C __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.C> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToLocalJniHandle (__this.SetCustomDimension (index));
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='C']/method[@name='setCustomDimension' and count(parameter)=1 and parameter[1][@type='int']]"
		[Register ("setCustomDimension", "(I)Lxamarin/test/C;", "GetSetCustomDimension_IHandler")]
		public virtual unsafe global::Java.Lang.Object SetCustomDimension (int index)
		{
			const string __id = "setCustomDimension.(I)Lxamarin/test/C;";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (index);
				var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, __args);
				return (Java.Lang.Object) global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (__rm.Handle, JniHandleOwnership.TransferLocalRef);
			} finally {
			}
		}

	}
}
