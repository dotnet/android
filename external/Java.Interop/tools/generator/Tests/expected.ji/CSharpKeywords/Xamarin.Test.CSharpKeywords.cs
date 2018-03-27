using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='CSharpKeywords']"
	[global::Android.Runtime.Register ("xamarin/test/CSharpKeywords", DoNotGenerateAcw=true)]
	public partial class CSharpKeywords : global::Java.Lang.Object {

		internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("xamarin/test/CSharpKeywords", typeof (CSharpKeywords));
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

		protected CSharpKeywords (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static Delegate cb_usePartial_I;
#pragma warning disable 0169
		static Delegate GetUsePartial_IHandler ()
		{
			if (cb_usePartial_I == null)
				cb_usePartial_I = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, int, IntPtr>) n_UsePartial_I);
			return cb_usePartial_I;
		}

		static IntPtr n_UsePartial_I (IntPtr jnienv, IntPtr native__this, int partial_)
		{
			global::Xamarin.Test.CSharpKeywords __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.CSharpKeywords> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.NewString (__this.UsePartial (partial_));
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='CSharpKeywords']/method[@name='usePartial' and count(parameter)=1 and parameter[1][@type='int']]"
		[Register ("usePartial", "(I)Ljava/lang/String;", "GetUsePartial_IHandler")]
		public virtual unsafe string UsePartial (int partial_)
		{
			const string __id = "usePartial.(I)Ljava/lang/String;";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (partial_);
				var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, __args);
				return JNIEnv.GetString (__rm.Handle, JniHandleOwnership.TransferLocalRef);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='CSharpKeywords']/method[@name='useThis' and count(parameter)=1 and parameter[1][@type='java.lang.String']]"
		[Register ("useThis", "(Ljava/lang/String;)Ljava/lang/String;", "")]
		public static unsafe string UseThis (string this_)
		{
			const string __id = "useThis.(Ljava/lang/String;)Ljava/lang/String;";
			IntPtr native_this = JNIEnv.NewString (this_);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_this);
				var __rm = _members.StaticMethods.InvokeObjectMethod (__id, __args);
				return JNIEnv.GetString (__rm.Handle, JniHandleOwnership.TransferLocalRef);
			} finally {
				JNIEnv.DeleteLocalRef (native_this);
			}
		}

	}
}
