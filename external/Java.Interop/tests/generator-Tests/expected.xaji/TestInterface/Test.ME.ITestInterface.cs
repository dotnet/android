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
				return global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (__v.Handle, JniHandleOwnership.TransferLocalRef);
			}
		}

		static readonly JniPeerMembers _members = new XAPeerMembers ("test/me/TestInterface", typeof (TestInterface));

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
			var jls_value = value == null ? null : new global::Java.Lang.String (value);
			self.Append (jls_value);
			jls_value?.Dispose ();
		}

		public static string Identity (this Test.ME.ITestInterface self, string value)
		{
			var jls_value = value == null ? null : new global::Java.Lang.String (value);
			global::Java.Lang.ICharSequence __result = self.IdentityFormatted (jls_value);
			var __rsval = __result?.ToString ();
			jls_value?.Dispose ();
			return __rsval;
		}

	}

	[global::Android.Runtime.Register ("test/me/TestInterface", DoNotGenerateAcw=true)]
	internal partial class ITestInterfaceInvoker : global::Java.Lang.Object, ITestInterface {
		static IntPtr java_class_ref {
			get { return _members_test_me_TestInterface.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_test_me_TestInterface; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members_test_me_TestInterface.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members_test_me_TestInterface.ManagedPeerType; }
		}

		static readonly JniPeerMembers _members_test_me_TestInterface = new XAPeerMembers ("test/me/TestInterface", typeof (ITestInterfaceInvoker));

		public ITestInterfaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static Delegate cb_getSpanFlags_GetSpanFlags_Ljava_lang_Object__I;
#pragma warning disable 0169
		static Delegate GetGetSpanFlags_Ljava_lang_Object_Handler ()
		{
			return cb_getSpanFlags_GetSpanFlags_Ljava_lang_Object__I ??= new _JniMarshal_PPL_I (n_GetSpanFlags_Ljava_lang_Object_);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		static int n_GetSpanFlags_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_tag)
		{
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				var __this = global::Java.Lang.Object.GetObject<global::Test.ME.ITestInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var tag = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_tag, JniHandleOwnership.DoNotTransfer);
				int __ret = __this.GetSpanFlags (tag);
				return __ret;
			} catch (global::System.Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}
#pragma warning restore 0169

		public unsafe int GetSpanFlags (global::Java.Lang.Object tag)
		{
			const string __id = "getSpanFlags.(Ljava/lang/Object;)I";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue ((tag == null) ? IntPtr.Zero : ((global::Java.Lang.Object) tag).Handle);
				var __rm = _members_test_me_TestInterface.InstanceMethods.InvokeAbstractInt32Method (__id, this, __args);
				return __rm;
			} finally {
				global::System.GC.KeepAlive (tag);
			}
		}

		static Delegate cb_append_Append_Ljava_lang_CharSequence__V;
#pragma warning disable 0169
		static Delegate GetAppend_Ljava_lang_CharSequence_Handler ()
		{
			return cb_append_Append_Ljava_lang_CharSequence__V ??= new _JniMarshal_PPL_V (n_Append_Ljava_lang_CharSequence_);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		static void n_Append_Ljava_lang_CharSequence_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return;

			try {
				var __this = global::Java.Lang.Object.GetObject<global::Test.ME.ITestInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var value = global::Java.Lang.Object.GetObject<global::Java.Lang.ICharSequence> (native_value, JniHandleOwnership.DoNotTransfer);
				__this.Append (value);
			} catch (global::System.Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
			} finally {
				global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}
#pragma warning restore 0169

		public unsafe void Append (global::Java.Lang.ICharSequence value)
		{
			const string __id = "append.(Ljava/lang/CharSequence;)V";
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

		static Delegate cb_identity_Identity_Ljava_lang_CharSequence__Ljava_lang_CharSequence_;
#pragma warning disable 0169
		static Delegate GetIdentity_Ljava_lang_CharSequence_Handler ()
		{
			return cb_identity_Identity_Ljava_lang_CharSequence__Ljava_lang_CharSequence_ ??= new _JniMarshal_PPL_L (n_Identity_Ljava_lang_CharSequence_);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		static IntPtr n_Identity_Ljava_lang_CharSequence_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
				return default;

			try {
				var __this = global::Java.Lang.Object.GetObject<global::Test.ME.ITestInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				var value = global::Java.Lang.Object.GetObject<global::Java.Lang.ICharSequence> (native_value, JniHandleOwnership.DoNotTransfer);
				IntPtr __ret = CharSequence.ToLocalJniHandle (__this.IdentityFormatted (value));
				return __ret;
			} catch (global::System.Exception __e) {
				__r.OnUserUnhandledException (ref __envp, __e);
				return default;
			} finally {
				global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
			}
		}
#pragma warning restore 0169

		public unsafe global::Java.Lang.ICharSequence IdentityFormatted (global::Java.Lang.ICharSequence value)
		{
			const string __id = "identity.(Ljava/lang/CharSequence;)Ljava/lang/CharSequence;";
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
