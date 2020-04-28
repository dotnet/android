using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='A']"
	[global::Android.Runtime.Register ("xamarin/test/A", DoNotGenerateAcw=true)]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T extends java.lang.Object"})]
	public partial class A : global::Java.Lang.Object {

		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/A", typeof (A));
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

		protected A (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static Delegate cb_setA_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetSetA_Ljava_lang_Object_Handler ()
		{
			if (cb_setA_Ljava_lang_Object_ == null)
				cb_setA_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPL_V) n_SetA_Ljava_lang_Object_);
			return cb_setA_Ljava_lang_Object_;
		}

		static void n_SetA_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_adapter)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.A> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var adapter = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_adapter, JniHandleOwnership.DoNotTransfer);
			__this.SetA (adapter);
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='A']/method[@name='setA' and count(parameter)=1 and parameter[1][@type='T']]"
		[Register ("setA", "(Ljava/lang/Object;)V", "GetSetA_Ljava_lang_Object_Handler")]
		public virtual unsafe void SetA (global::Java.Lang.Object adapter)
		{
			const string __id = "setA.(Ljava/lang/Object;)V";
			IntPtr native_adapter = JNIEnv.ToLocalJniHandle (adapter);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_adapter);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_adapter);
			}
		}

		static Delegate cb_listTest_Ljava_util_List_;
#pragma warning disable 0169
		static Delegate GetListTest_Ljava_util_List_Handler ()
		{
			if (cb_listTest_Ljava_util_List_ == null)
				cb_listTest_Ljava_util_List_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPL_V) n_ListTest_Ljava_util_List_);
			return cb_listTest_Ljava_util_List_;
		}

		static void n_ListTest_Ljava_util_List_ (IntPtr jnienv, IntPtr native__this, IntPtr native_p0)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.A> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var p0 = global::Android.Runtime.JavaList<global::Java.Lang.Integer>.FromJniHandle (native_p0, JniHandleOwnership.DoNotTransfer);
			__this.ListTest (p0);
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='A']/method[@name='listTest' and count(parameter)=1 and parameter[1][@type='java.util.List&lt;java.lang.Integer&gt;']]"
		[Register ("listTest", "(Ljava/util/List;)V", "GetListTest_Ljava_util_List_Handler")]
		public virtual unsafe void ListTest (global::System.Collections.Generic.IList<global::Java.Lang.Integer> p0)
		{
			const string __id = "listTest.(Ljava/util/List;)V";
			IntPtr native_p0 = global::Android.Runtime.JavaList<global::Java.Lang.Integer>.ToLocalJniHandle (p0);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_p0);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_p0);
			}
		}

	}
}
