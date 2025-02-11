using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Java.Util {

	// Metadata.xml XPath interface reference: path="/api/package[@name='java.util']/interface[@name='Queue']"
	[Register ("java/util/Queue", "", "Java.Util.IQueueInvoker")]
	[global::Java.Interop.JavaTypeParameters (new string [] {"E"})]
	public partial interface IQueue : global::Java.Util.ICollection {
		// Metadata.xml XPath method reference: path="/api/package[@name='java.util']/interface[@name='Queue']/method[@name='add' and count(parameter)=1 and parameter[1][@type='E']]"
		[Register ("add", "(Ljava/lang/Object;)Z", "GetAdd_Ljava_lang_Object_Handler:Java.Util.IQueueInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		bool Add (global::Java.Lang.Object e);

	}

	[global::Android.Runtime.Register ("java/util/Queue", DoNotGenerateAcw=true)]
	internal partial class IQueueInvoker : global::Java.Lang.Object, IQueue {
		static IntPtr java_class_ref {
			get { return _members_java_util_Queue.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_java_util_Queue; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members_java_util_Queue.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members_java_util_Queue.ManagedPeerType; }
		}

		static readonly JniPeerMembers _members_java_util_Collection = new XAPeerMembers ("java/util/Collection", typeof (IQueueInvoker));

		static readonly JniPeerMembers _members_java_util_Queue = new XAPeerMembers ("java/util/Queue", typeof (IQueueInvoker));

		public IQueueInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_add_Add_Ljava_lang_Object__Z;
#pragma warning disable 0169
		static Delegate GetAdd_Ljava_lang_Object_Handler ()
		{
			return cb_add_Add_Ljava_lang_Object__Z ??= new _JniMarshal_PPL_B (n_Add_Ljava_lang_Object_);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		static sbyte n_Add_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_e)
		{
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				var __this = global::Java.Lang.Object.GetObject<global::Java.Util.IQueue> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var e = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_e, JniHandleOwnership.DoNotTransfer);
				sbyte __ret = __this.Add (e) ? (sbyte)1 : (sbyte)0;
				return __ret;
			} catch (global::System.Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}
#pragma warning restore 0169

		public unsafe bool Add (global::Java.Lang.Object e)
		{
			const string __id = "add.(Ljava/lang/Object;)Z";
			IntPtr native_e = JNIEnv.ToLocalJniHandle (e);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_e);
				var __rm = _members_java_util_Queue.InstanceMethods.InvokeAbstractBooleanMethod (__id, this, __args);
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
			return cb_clear_Clear_V ??= new _JniMarshal_PP_V (n_Clear);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		static void n_Clear (IntPtr jnienv, IntPtr native__this)
		{
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				var __this = global::Java.Lang.Object.GetObject<global::Java.Util.IQueue> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				__this.Clear ();
			} catch (global::System.Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
			}
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
