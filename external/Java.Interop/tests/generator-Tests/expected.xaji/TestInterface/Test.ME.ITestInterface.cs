using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Test.ME {

	[Register ("mono/internal/test/me/TestInterface", DoNotGenerateAcw=true)]
	public abstract class TestInterface : Java.Lang.Object {
		internal TestInterface ()
		{
		}

		// Metadata.xml XPath field reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/field[@name='SPAN_COMPOSING']"
		[Register ("SPAN_COMPOSING")]
		public const int SpanComposing = (int) 256;


		// Metadata.xml XPath field reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/field[@name='DEFAULT_FOO']"
		[Register ("DEFAULT_FOO")]
		public static global::Java.Lang.Object DefaultFoo {
			get {
				const string __id = "DEFAULT_FOO.Ljava/lang/Object;";

				var __v = _members.StaticFields.GetObjectValue (__id);
				unsafe {
					// SAFETY: The field yields a current-thread local of __id's type, transferred once to its managed marshaler.
					return global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (__v.Handle, JniHandleOwnership.TransferLocalRef);
				}
			}
		}

		static readonly JniPeerMembers _members = new JniPeerMembers ("test/me/TestInterface", typeof (TestInterface));

	}

	[Register ("mono/internal/test/me/TestInterface", DoNotGenerateAcw=true)]
	[global::System.Obsolete (@"Use the 'TestInterface' type. This type will be removed in a future release.", error: true)]
	public abstract class TestInterfaceConsts : TestInterface {
		private TestInterfaceConsts ()
		{
		}

	}

	// Metadata.xml XPath interface reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']"
	[Register ("test/me/TestInterface", "", "Test.ME.ITestInterfaceInvoker")]
	public partial interface ITestInterface : IJavaObject, IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[Register ("getSpanFlags", "(Ljava/lang/Object;)I", "GetGetSpanFlags_Ljava_lang_Object_Handler:Test.ME.ITestInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		int GetSpanFlags (global::Java.Lang.Object tag);

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='append' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		[Register ("append", "(Ljava/lang/CharSequence;)V", "GetAppend_Ljava_lang_CharSequence_Handler:Test.ME.ITestInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		void Append (global::Java.Lang.ICharSequence value);

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='identity' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		[Register ("identity", "(Ljava/lang/CharSequence;)Ljava/lang/CharSequence;", "GetIdentity_Ljava_lang_CharSequence_Handler:Test.ME.ITestInterfaceInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		global::Java.Lang.ICharSequence IdentityFormatted (global::Java.Lang.ICharSequence value);

	}

	public static partial class ITestInterfaceExtensions {
		public static void Append (this Test.ME.ITestInterface self, string value)
		{
			global::Java.Lang.String jls_value;
			unsafe {
				// SAFETY: The constructor copies a managed string into a new Java peer; no raw reference is supplied.
				jls_value = value == null ? null : new global::Java.Lang.String (value);
			}
			self.Append (jls_value);
			jls_value?.Dispose ();
		}

		public static string Identity (this Test.ME.ITestInterface self, string value)
		{
			global::Java.Lang.String jls_value;
			unsafe {
				// SAFETY: The constructor copies a managed string into a new Java peer; no raw reference is supplied.
				jls_value = value == null ? null : new global::Java.Lang.String (value);
			}
			global::Java.Lang.ICharSequence __result = self.IdentityFormatted (jls_value);
			var __rsval = __result?.ToString ();
			jls_value?.Dispose ();
			return __rsval;
		}

	}

	[global::Android.Runtime.Register ("test/me/TestInterface", DoNotGenerateAcw=true)]
	internal partial class ITestInterfaceInvoker : global::Java.Lang.Object, ITestInterface {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_test_me_TestInterface; }
		}

		static readonly JniPeerMembers _members_test_me_TestInterface = new JniPeerMembers ("test/me/TestInterface", typeof (ITestInterfaceInvoker));

		/// <safety>handle must be zero or a live JNI object implementing this interface. Use an attached thread (locals: creating thread), with no concurrent deletion through aliases during construction. transfer must match ownership; transferred references cannot be used again, even if construction throws.</safety>
		/// <remarks>This constructs a JNI peer, not an opaque-address container. RequiresUnsafe enforcement is deferred to preserve safe zero/DoNotTransfer base initializers; the raw-reference obligations still apply.</remarks>
		public ITestInterfaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_getSpanFlags_GetSpanFlags_Ljava_lang_Object__I;
#pragma warning disable 0169
		static Delegate GetGetSpanFlags_Ljava_lang_Object_Handler ()
		{
			unsafe {
				// SAFETY: The delegate matches the registered callback signature; JNI, not this connector, supplies its arguments.
				return cb_getSpanFlags_GetSpanFlags_Ljava_lang_Object__I ??= new _JniMarshal_PPL_I (n_GetSpanFlags_Ljava_lang_Object_);
			}
		}

		/// <safety>Use the registered JNI ABI on jnienv's attached thread. Receiver/reference arguments must match the registered types and stay live without concurrent deletion for this call; permitted nulls stay null. JNI retains borrowed ownership. The helper reports managed failures and may return default; invalid addresses are not validated.</safety>
		static unsafe int n_GetSpanFlags_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_tag)
		{
			unsafe {
				// SAFETY: JNI supplies this thread's environment and live typed references; the static target pointer is synchronous.
				return global::Java.Interop.JniMarshal.SafeInvokeFunc (jnienv, native__this, native_tag, &__n_GetSpanFlags_Ljava_lang_Object_);
			}
		}
		/// <safety>Requires the entry point's live, type-correct borrowed references in its active JNI frame/thread, without concurrent deletion. Copy-back occurs on normal return only; failures propagate to the marshal helper.</safety>
		private static unsafe int __n_GetSpanFlags_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_tag)
		{
			unsafe {
				// SAFETY: The marshal frame keeps typed JNI references live on this thread through conversion and normal-return copy-back.
				var __this = global::Java.Lang.Object.GetObject<global::Test.ME.ITestInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var tag = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_tag, JniHandleOwnership.DoNotTransfer);
				int __ret = __this.GetSpanFlags (tag);
				return __ret;
			}
		}
#pragma warning restore 0169

		public int GetSpanFlags (global::Java.Lang.Object tag)
		{
			const string __id = "getSpanFlags.(Ljava/lang/Object;)I";
			unsafe {
				// SAFETY: __id fixes argument types/count; initialized stack arguments and JNI locals stay on this thread for the synchronous call.
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue ((tag == null) ? IntPtr.Zero : ((global::Java.Lang.Object) tag).Handle);
				var __rm = _members_test_me_TestInterface.InstanceMethods.InvokeAbstractInt32Method (__id, this, __args);
				var __result = __rm;
				global::System.GC.KeepAlive (tag);
				return __result;
			}
		}

		static Delegate cb_append_Append_Ljava_lang_CharSequence__V;
#pragma warning disable 0169
		static Delegate GetAppend_Ljava_lang_CharSequence_Handler ()
		{
			unsafe {
				// SAFETY: The delegate matches the registered callback signature; JNI, not this connector, supplies its arguments.
				return cb_append_Append_Ljava_lang_CharSequence__V ??= new _JniMarshal_PPL_V (n_Append_Ljava_lang_CharSequence_);
			}
		}

		/// <safety>Use the registered JNI ABI on jnienv's attached thread. Receiver/reference arguments must match the registered types and stay live without concurrent deletion for this call; permitted nulls stay null. JNI retains borrowed ownership. The helper reports managed failures and may return default; invalid addresses are not validated.</safety>
		static unsafe void n_Append_Ljava_lang_CharSequence_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			unsafe {
				// SAFETY: JNI supplies this thread's environment and live typed references; the static target pointer is synchronous.
				global::Java.Interop.JniMarshal.SafeInvokeAction (jnienv, native__this, native_value, &__n_Append_Ljava_lang_CharSequence_);
			}
		}
		/// <safety>Requires the entry point's live, type-correct borrowed references in its active JNI frame/thread, without concurrent deletion. Copy-back occurs on normal return only; failures propagate to the marshal helper.</safety>
		private static unsafe void __n_Append_Ljava_lang_CharSequence_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			unsafe {
				// SAFETY: The marshal frame keeps typed JNI references live on this thread through conversion and normal-return copy-back.
				var __this = global::Java.Lang.Object.GetObject<global::Test.ME.ITestInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var value = global::Java.Lang.Object.GetObject<global::Java.Lang.ICharSequence> (native_value, JniHandleOwnership.DoNotTransfer);
				__this.Append (value);
			}
		}
#pragma warning restore 0169

		public void Append (global::Java.Lang.ICharSequence value)
		{
			const string __id = "append.(Ljava/lang/CharSequence;)V";
			unsafe {
				// SAFETY: __id fixes argument types/count; initialized stack arguments and JNI locals stay on this thread for the synchronous call.
				IntPtr native_value = CharSequence.ToLocalJniHandle (value);
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (native_value);
					_members_test_me_TestInterface.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
					global::System.GC.KeepAlive (value);
				}
			}
		}

		static Delegate cb_identity_Identity_Ljava_lang_CharSequence__Ljava_lang_CharSequence_;
#pragma warning disable 0169
		static Delegate GetIdentity_Ljava_lang_CharSequence_Handler ()
		{
			unsafe {
				// SAFETY: The delegate matches the registered callback signature; JNI, not this connector, supplies its arguments.
				return cb_identity_Identity_Ljava_lang_CharSequence__Ljava_lang_CharSequence_ ??= new _JniMarshal_PPL_L (n_Identity_Ljava_lang_CharSequence_);
			}
		}

		/// <safety>Use the registered JNI ABI on jnienv's attached thread. Receiver/reference arguments must match the registered types and stay live without concurrent deletion for this call; permitted nulls stay null. JNI retains borrowed ownership. The helper reports managed failures and may return default; invalid addresses are not validated.</safety>
		static unsafe IntPtr n_Identity_Ljava_lang_CharSequence_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			unsafe {
				// SAFETY: JNI supplies this thread's environment and live typed references; the static target pointer is synchronous.
				return global::Java.Interop.JniMarshal.SafeInvokeFunc (jnienv, native__this, native_value, &__n_Identity_Ljava_lang_CharSequence_);
			}
		}
		/// <safety>Requires the entry point's live, type-correct borrowed references in its active JNI frame/thread, without concurrent deletion. Copy-back occurs on normal return only; failures propagate to the marshal helper.</safety>
		private static unsafe IntPtr __n_Identity_Ljava_lang_CharSequence_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			unsafe {
				// SAFETY: The marshal frame keeps typed JNI references live on this thread through conversion and normal-return copy-back.
				var __this = global::Java.Lang.Object.GetObject<global::Test.ME.ITestInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var value = global::Java.Lang.Object.GetObject<global::Java.Lang.ICharSequence> (native_value, JniHandleOwnership.DoNotTransfer);
				IntPtr __ret = CharSequence.ToLocalJniHandle (__this.IdentityFormatted (value));
				return __ret;
			}
		}
#pragma warning restore 0169

		public global::Java.Lang.ICharSequence IdentityFormatted (global::Java.Lang.ICharSequence value)
		{
			const string __id = "identity.(Ljava/lang/CharSequence;)Ljava/lang/CharSequence;";
			unsafe {
				// SAFETY: __id fixes argument types/count; initialized stack arguments and JNI locals stay on this thread for the synchronous call.
				IntPtr native_value = CharSequence.ToLocalJniHandle (value);
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (native_value);
					var __rm = _members_test_me_TestInterface.InstanceMethods.InvokeAbstractObjectMethod (__id, this, __args);
					return global::Java.Lang.Object.GetObject<Java.Lang.ICharSequence> (__rm.Handle, JniHandleOwnership.TransferLocalRef);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
					global::System.GC.KeepAlive (value);
				}
			}
		}

	}
}
