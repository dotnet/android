using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Test.ME {

	// Metadata.xml XPath class reference: path="/api/package[@name='test.me']/class[@name='TestInterfaceImplementation']"
	[global::Android.Runtime.Register ("test/me/TestInterfaceImplementation", DoNotGenerateAcw=true)]
	public abstract partial class TestInterfaceImplementation : global::Java.Lang.Object, global::Test.ME.ITestInterface {


		public static class InterfaceConsts {

			// The following are fields from: test.me.TestInterface

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
		}

		static readonly JniPeerMembers _members = new JniPeerMembers ("test/me/TestInterfaceImplementation", typeof (TestInterfaceImplementation));
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

		protected TestInterfaceImplementation (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='test.me']/class[@name='TestInterfaceImplementation']/constructor[@name='TestInterfaceImplementation' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe TestInterfaceImplementation ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string __id = "()V";

			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), null);
				SetHandle (__r.Handle, JniHandleOwnership.TransferLocalRef);
				_members.InstanceMethods.FinishCreateInstance (__id, this, null);
			} finally {
			}
		}

		static Delegate cb_getSpanFlags_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetGetSpanFlags_Ljava_lang_Object_Handler ()
		{
			if (cb_getSpanFlags_Ljava_lang_Object_ == null)
				cb_getSpanFlags_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPL_I) n_GetSpanFlags_Ljava_lang_Object_);
			return cb_getSpanFlags_Ljava_lang_Object_;
		}

		static int n_GetSpanFlags_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_tag)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Test.ME.TestInterfaceImplementation> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var tag = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_tag, JniHandleOwnership.DoNotTransfer);
			int __ret = __this.GetSpanFlags (tag);
			return __ret;
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[Register ("getSpanFlags", "(Ljava/lang/Object;)I", "GetGetSpanFlags_Ljava_lang_Object_Handler")]
		public abstract int GetSpanFlags (global::Java.Lang.Object tag);

		static Delegate cb_append_Ljava_lang_CharSequence_;
#pragma warning disable 0169
		static Delegate GetAppend_Ljava_lang_CharSequence_Handler ()
		{
			if (cb_append_Ljava_lang_CharSequence_ == null)
				cb_append_Ljava_lang_CharSequence_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPL_V) n_Append_Ljava_lang_CharSequence_);
			return cb_append_Ljava_lang_CharSequence_;
		}

		static void n_Append_Ljava_lang_CharSequence_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Test.ME.TestInterfaceImplementation> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var value = global::Java.Lang.Object.GetObject<global::Java.Lang.ICharSequence> (native_value, JniHandleOwnership.DoNotTransfer);
			__this.Append (value);
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='append' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		[Register ("append", "(Ljava/lang/CharSequence;)V", "GetAppend_Ljava_lang_CharSequence_Handler")]
		public abstract void Append (global::Java.Lang.ICharSequence value);

		public void Append (string value)
		{
			var jls_value = value == null ? null : new global::Java.Lang.String (value);
			Append (jls_value);
			jls_value?.Dispose ();
		}

		static Delegate cb_identity_Ljava_lang_CharSequence_;
#pragma warning disable 0169
		static Delegate GetIdentity_Ljava_lang_CharSequence_Handler ()
		{
			if (cb_identity_Ljava_lang_CharSequence_ == null)
				cb_identity_Ljava_lang_CharSequence_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPL_L) n_Identity_Ljava_lang_CharSequence_);
			return cb_identity_Ljava_lang_CharSequence_;
		}

		static IntPtr n_Identity_Ljava_lang_CharSequence_ (IntPtr jnienv, IntPtr native__this, IntPtr native_value)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Test.ME.TestInterfaceImplementation> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var value = global::Java.Lang.Object.GetObject<global::Java.Lang.ICharSequence> (native_value, JniHandleOwnership.DoNotTransfer);
			IntPtr __ret = CharSequence.ToLocalJniHandle (__this.IdentityFormatted (value));
			return __ret;
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='identity' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		[Register ("identity", "(Ljava/lang/CharSequence;)Ljava/lang/CharSequence;", "GetIdentity_Ljava_lang_CharSequence_Handler")]
		public abstract global::Java.Lang.ICharSequence IdentityFormatted (global::Java.Lang.ICharSequence value);

		public string Identity (string value)
		{
			var jls_value = value == null ? null : new global::Java.Lang.String (value);
			global::Java.Lang.ICharSequence __result = IdentityFormatted (jls_value);
			var __rsval = __result?.ToString ();
			jls_value?.Dispose ();
			return __rsval;
		}

	}

	[global::Android.Runtime.Register ("test/me/TestInterfaceImplementation", DoNotGenerateAcw=true)]
	internal partial class TestInterfaceImplementationInvoker : TestInterfaceImplementation {

		public TestInterfaceImplementationInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		static readonly JniPeerMembers _members = new JniPeerMembers ("test/me/TestInterfaceImplementation", typeof (TestInterfaceImplementationInvoker));

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[Register ("getSpanFlags", "(Ljava/lang/Object;)I", "GetGetSpanFlags_Ljava_lang_Object_Handler")]
		public override unsafe int GetSpanFlags (global::Java.Lang.Object tag)
		{
			const string __id = "getSpanFlags.(Ljava/lang/Object;)I";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue ((tag == null) ? IntPtr.Zero : ((global::Java.Lang.Object) tag).Handle);
				var __rm = _members.InstanceMethods.InvokeAbstractInt32Method (__id, this, __args);
				return __rm;
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='append' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		[Register ("append", "(Ljava/lang/CharSequence;)V", "GetAppend_Ljava_lang_CharSequence_Handler")]
		public override unsafe void Append (global::Java.Lang.ICharSequence value)
		{
			const string __id = "append.(Ljava/lang/CharSequence;)V";
			IntPtr native_value = CharSequence.ToLocalJniHandle (value);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_value);
				_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_value);
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='identity' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		[Register ("identity", "(Ljava/lang/CharSequence;)Ljava/lang/CharSequence;", "GetIdentity_Ljava_lang_CharSequence_Handler")]
		public override unsafe global::Java.Lang.ICharSequence IdentityFormatted (global::Java.Lang.ICharSequence value)
		{
			const string __id = "identity.(Ljava/lang/CharSequence;)Ljava/lang/CharSequence;";
			IntPtr native_value = CharSequence.ToLocalJniHandle (value);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_value);
				var __rm = _members.InstanceMethods.InvokeAbstractObjectMethod (__id, this, __args);
				return global::Java.Lang.Object.GetObject<Java.Lang.ICharSequence> (__rm.Handle, JniHandleOwnership.TransferLocalRef);
			} finally {
				JNIEnv.DeleteLocalRef (native_value);
			}
		}

	}

}
