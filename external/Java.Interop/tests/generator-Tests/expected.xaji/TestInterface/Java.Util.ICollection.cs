// Java allows a type to redeclare a member that a base type or base interface already
// declares, including members that are hand-bound instead of generated.
#pragma warning disable 0108, 0114
// Java types may declare a `finalize()` method, which is bound as `Finalize()`.
#pragma warning disable 0465
// Java deprecates types and members independently of the APIs that use, declare or
// override them, so a binding that is not deprecated can still reference one that is.
#pragma warning disable 0618, 0672, 0809
// Java nullness annotations are not required to agree between a member and the member
// it overrides or implements, and are absent from much of the API surface.
#pragma warning disable 8603, 8604, 8625, 8764, 8765, 8766, 8767, 8768

using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Java.Util {

	// Metadata.xml XPath interface reference: path="/api/package[@name='java.util']/interface[@name='Collection']"
	[Register ("java/util/Collection", "", "Java.Util.ICollectionInvoker")]
	[global::Java.Interop.JavaTypeParameters (new string [] {"E"})]
	public partial interface ICollection : IJavaObject, IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='java.util']/interface[@name='Collection']/method[@name='add' and count(parameter)=1 and parameter[1][@type='E']]"
		[Register ("add", "(Ljava/lang/Object;)Z", "GetAdd_Ljava_lang_Object_Handler:Java.Util.ICollectionInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		bool Add (global::Java.Lang.Object e);

		// Metadata.xml XPath method reference: path="/api/package[@name='java.util']/interface[@name='Collection']/method[@name='clear' and count(parameter)=0]"
		[Register ("clear", "()V", "GetClearHandler:Java.Util.ICollectionInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		void Clear ();

	}

	[global::Android.Runtime.Register ("java/util/Collection", DoNotGenerateAcw=true)]
	internal partial class ICollectionInvoker : global::Java.Lang.Object, ICollection {
		static IntPtr java_class_ref {
			get { return _members_java_util_Collection.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_java_util_Collection; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members_java_util_Collection.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members_java_util_Collection.ManagedPeerType; }
		}

		static readonly JniPeerMembers _members_java_util_Collection = new XAPeerMembers ("java/util/Collection", typeof (ICollectionInvoker));

		public ICollectionInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_add_Add_Ljava_lang_Object__Z;
#pragma warning disable 0169
		static Delegate GetAdd_Ljava_lang_Object_Handler ()
		{
			return cb_add_Add_Ljava_lang_Object__Z ??= new _JniMarshal_PPL_B (n_Add_Ljava_lang_Object_);
		}

		static sbyte n_Add_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_e)
		{
			unsafe {
				return global::Java.Interop.JniMarshal.SafeInvokeFunc (jnienv, native__this, native_e, &__n_Add_Ljava_lang_Object_);
			}
		}
		private static sbyte __n_Add_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_e)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.Util.ICollection> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var e = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_e, JniHandleOwnership.DoNotTransfer);
			sbyte __ret = __this.Add (e) ? (sbyte)1 : (sbyte)0;
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
				var __rm = _members_java_util_Collection.InstanceMethods.InvokeAbstractBooleanMethod (__id, this, __args);
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

		static void n_Clear (IntPtr jnienv, IntPtr native__this)
		{
			unsafe {
				global::Java.Interop.JniMarshal.SafeInvokeAction (jnienv, native__this, &__n_Clear);
			}
		}
		private static void __n_Clear (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Java.Util.ICollection> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
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
