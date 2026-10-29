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
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_java_util_Collection; }
		}

		static readonly JniPeerMembers _members_java_util_Collection = new JniPeerMembers ("java/util/Collection", typeof (ICollectionInvoker));

		/// <safety>
		/// handle must be zero or a live JNI object implementing this interface. Use an attached
		/// thread (locals: creating thread), with no concurrent deletion through aliases during
		/// construction. transfer must match ownership; transferred references cannot be used again,
		/// even if construction throws.
		/// </safety>
		/// <remarks>
		/// This constructs a JNI peer, not an opaque-address container. RequiresUnsafe enforcement
		/// is deferred to preserve safe zero/DoNotTransfer base initializers; the raw-reference
		/// obligations still apply.
		/// </remarks>
		public ICollectionInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_add_Add_Ljava_lang_Object__Z;
#pragma warning disable 0169
		static Delegate GetAdd_Ljava_lang_Object_Handler ()
		{
			unsafe {
				// SAFETY: The delegate matches the registered callback signature; JNI, not this connector, supplies its arguments.
				return cb_add_Add_Ljava_lang_Object__Z ??= new _JniMarshal_PPL_B (n_Add_Ljava_lang_Object_);
			}
		}

		/// <safety>
		/// Use the registered JNI ABI on jnienv's attached thread. Receiver/reference arguments
		/// must match the registered types and stay live without concurrent deletion for this call;
		/// permitted nulls stay null. JNI retains borrowed ownership. The helper reports managed
		/// failures and may return default; invalid addresses are not validated.
		/// </safety>
		static unsafe sbyte n_Add_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_e)
		{
			unsafe {
				// SAFETY: JNI supplies this thread's environment and live typed references; the static target pointer is synchronous.
				return global::Java.Interop.JniMarshal.SafeInvokeFunc (jnienv, native__this, native_e, &__n_Add_Ljava_lang_Object_);
			}
		}
		/// <safety>
		/// Requires the entry point's live, type-correct borrowed references in its active JNI
		/// frame/thread, without concurrent deletion. Copy-back occurs on normal return only;
		/// failures propagate to the marshal helper.
		/// </safety>
		private static unsafe sbyte __n_Add_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_e)
		{
			unsafe {
				// SAFETY: The marshal frame keeps typed JNI references live on this thread through conversion and normal-return copy-back.
				var __this = global::Java.Lang.Object.GetObject<global::Java.Util.ICollection> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var e = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_e, JniHandleOwnership.DoNotTransfer);
				sbyte __ret = __this.Add (e) ? (sbyte)1 : (sbyte)0;
				return __ret;
			}
		}
#pragma warning restore 0169

		public bool Add (global::Java.Lang.Object e)
		{
			const string __id = "add.(Ljava/lang/Object;)Z";
			unsafe {
				// SAFETY: __id fixes argument types/count; initialized stack arguments and JNI locals stay on this thread for the synchronous call.
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
		}

		static Delegate cb_clear_Clear_V;
#pragma warning disable 0169
		static Delegate GetClearHandler ()
		{
			unsafe {
				// SAFETY: The delegate matches the registered callback signature; JNI, not this connector, supplies its arguments.
				return cb_clear_Clear_V ??= new _JniMarshal_PP_V (n_Clear);
			}
		}

		/// <safety>
		/// Use the registered JNI ABI on jnienv's attached thread. Receiver/reference arguments
		/// must match the registered types and stay live without concurrent deletion for this call;
		/// permitted nulls stay null. JNI retains borrowed ownership. The helper reports managed
		/// failures and may return default; invalid addresses are not validated.
		/// </safety>
		static unsafe void n_Clear (IntPtr jnienv, IntPtr native__this)
		{
			unsafe {
				// SAFETY: JNI supplies this thread's environment and live typed references; the static target pointer is synchronous.
				global::Java.Interop.JniMarshal.SafeInvokeAction (jnienv, native__this, &__n_Clear);
			}
		}
		/// <safety>
		/// Requires the entry point's live, type-correct borrowed references in its active JNI
		/// frame/thread, without concurrent deletion. Copy-back occurs on normal return only;
		/// failures propagate to the marshal helper.
		/// </safety>
		private static unsafe void __n_Clear (IntPtr jnienv, IntPtr native__this)
		{
			unsafe {
				// SAFETY: The marshal frame keeps typed JNI references live on this thread through conversion and normal-return copy-back.
				var __this = global::Java.Lang.Object.GetObject<global::Java.Util.ICollection> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				__this.Clear ();
			}
		}
#pragma warning restore 0169

		public void Clear ()
		{
			const string __id = "clear.()V";
			unsafe {
				// SAFETY: __id fixes argument types/count; initialized stack arguments and JNI locals stay on this thread for the synchronous call.
				_members_java_util_Collection.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			}
		}

	}
}
