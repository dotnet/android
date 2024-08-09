using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Java.Util {

	// Metadata.xml XPath interface reference: path="/api/package[@name='java.util']/interface[@name='Deque']"
	[Register ("java/util/Deque", "", "Java.Util.IDequeInvoker")]
	[global::Java.Interop.JavaTypeParameters (new string [] {"E"})]
	public partial interface IDeque : global::Java.Util.IQueue {
		// Metadata.xml XPath method reference: path="/api/package[@name='java.util']/interface[@name='Deque']/method[@name='add' and count(parameter)=1 and parameter[1][@type='E']]"
		[Register ("add", "(Ljava/lang/Object;)Z", "GetAdd_Ljava_lang_Object_Handler:Java.Util.IDequeInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		bool Add (global::Java.Lang.Object e);

	}

	[global::Android.Runtime.Register ("java/util/Deque", DoNotGenerateAcw=true)]
	internal partial class IDequeInvoker : global::Java.Lang.Object, IDeque {
		static IntPtr java_class_ref {
			get { return _members_java_util_Deque.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_java_util_Deque; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members_java_util_Deque.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members_java_util_Deque.ManagedPeerType; }
		}

		static readonly JniPeerMembers _members_java_util_Collection = new XAPeerMembers ("java/util/Collection", typeof (IDequeInvoker));

		static readonly JniPeerMembers _members_java_util_Deque = new XAPeerMembers ("java/util/Deque", typeof (IDequeInvoker));

		static readonly JniPeerMembers _members_java_util_Queue = new XAPeerMembers ("java/util/Queue", typeof (IDequeInvoker));

		public IDequeInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_add_Add_Ljava_lang_Object__Z;
#pragma warning disable 0169
		static Delegate GetAdd_Ljava_lang_Object_Handler ()
		{
			if (cb_add_Add_Ljava_lang_Object__Z == null)
				cb_add_Add_Ljava_lang_Object__Z = JNINativeWrapper.CreateDelegate (new _JniMarshal_PPL_Z (n_Add_Ljava_lang_Object_));
			return cb_add_Add_Ljava_lang_Object__Z;
		}

		static bool n_Add_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_e)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.Util.IDeque> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var e = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_e, JniHandleOwnership.DoNotTransfer);
			bool __ret = __this.Add (e);
			return __ret;
		}
#pragma warning restore 0169

		public unsafe bool Add (global::Java.Lang.Object e)
		{
			const string __id = "add.(Ljava/lang/Object;)Z";
			IntPtr native_e = JNIEnv.ToLocalJniHandle (e);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_e);
				var __rm = _members_java_util_Deque.InstanceMethods.InvokeAbstractBooleanMethod (__id, this, __args);
				return __rm;
			} finally {
				JNIEnv.DeleteLocalRef (native_e);
				global::System.GC.KeepAlive (e);
			}
		}

		static Delegate cb_clear_Clear_V;
#pragma warning disable 0169
		static Delegate GetClearHandler ()
		{
			if (cb_clear_Clear_V == null)
				cb_clear_Clear_V = JNINativeWrapper.CreateDelegate (new _JniMarshal_PP_V (n_Clear));
			return cb_clear_Clear_V;
		}

		static void n_Clear (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.Util.IDeque> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Clear ();
		}
#pragma warning restore 0169

		public unsafe void Clear ()
		{
			const string __id = "clear.()V";
			try {
				_members_java_util_Collection.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			} finally {
			}
		}

	}
}
