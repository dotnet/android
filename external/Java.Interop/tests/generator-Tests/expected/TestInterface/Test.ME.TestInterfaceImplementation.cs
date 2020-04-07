using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Test.ME {

	// Metadata.xml XPath class reference: path="/api/package[@name='test.me']/class[@name='TestInterfaceImplementation']"
	[global::Android.Runtime.Register ("test/me/TestInterfaceImplementation", DoNotGenerateAcw=true)]
	public abstract partial class TestInterfaceImplementation : global::Java.Lang.Object, global::Test.ME.ITestInterface {


		public static class InterfaceConsts {

			// The following are fields from: test.me.TestInterface

			// Metadata.xml XPath field reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/field[@name='SPAN_COMPOSING']"
			[Register ("SPAN_COMPOSING")]
			public const int SpanComposing = (int) 256;

			static IntPtr DEFAULT_FOO_jfieldId;

			// Metadata.xml XPath field reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/field[@name='DEFAULT_FOO']"
			[Register ("DEFAULT_FOO")]
			public static global::Java.Lang.Object DefaultFoo {
				get {
					if (DEFAULT_FOO_jfieldId == IntPtr.Zero)
						DEFAULT_FOO_jfieldId = JNIEnv.GetStaticFieldID (class_ref, "DEFAULT_FOO", "Ljava/lang/Object;");
					IntPtr __ret = JNIEnv.GetStaticObjectField (class_ref, DEFAULT_FOO_jfieldId);
					return global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (__ret, JniHandleOwnership.TransferLocalRef);
				}
			}
		}

		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("test/me/TestInterfaceImplementation", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (TestInterfaceImplementation); }
		}

		protected TestInterfaceImplementation (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static IntPtr id_ctor;
		// Metadata.xml XPath constructor reference: path="/api/package[@name='test.me']/class[@name='TestInterfaceImplementation']/constructor[@name='TestInterfaceImplementation' and count(parameter)=0]"
		[Register (".ctor", "()V", "")]
		public unsafe TestInterfaceImplementation ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				if (((object) this).GetType () != typeof (TestInterfaceImplementation)) {
					SetHandle (
							global::Android.Runtime.JNIEnv.StartCreateInstance (((object) this).GetType (), "()V"),
							JniHandleOwnership.TransferLocalRef);
					global::Android.Runtime.JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, "()V");
					return;
				}

				if (id_ctor == IntPtr.Zero)
					id_ctor = JNIEnv.GetMethodID (class_ref, "<init>", "()V");
				SetHandle (
						global::Android.Runtime.JNIEnv.StartCreateInstance (class_ref, id_ctor),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, class_ref, id_ctor);
			} finally {
			}
		}

		static Delegate cb_getSpanFlags_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetGetSpanFlags_Ljava_lang_Object_Handler ()
		{
			if (cb_getSpanFlags_Ljava_lang_Object_ == null)
				cb_getSpanFlags_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr, int>) n_GetSpanFlags_Ljava_lang_Object_);
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
				cb_append_Ljava_lang_CharSequence_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_Append_Ljava_lang_CharSequence_);
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
				cb_identity_Ljava_lang_CharSequence_ = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr, IntPtr>) n_Identity_Ljava_lang_CharSequence_);
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

		protected override global::System.Type ThresholdType {
			get { return typeof (TestInterfaceImplementationInvoker); }
		}

		static IntPtr id_getSpanFlags_Ljava_lang_Object_;
		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[Register ("getSpanFlags", "(Ljava/lang/Object;)I", "GetGetSpanFlags_Ljava_lang_Object_Handler")]
		public override unsafe int GetSpanFlags (global::Java.Lang.Object tag)
		{
			if (id_getSpanFlags_Ljava_lang_Object_ == IntPtr.Zero)
				id_getSpanFlags_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "getSpanFlags", "(Ljava/lang/Object;)I");
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (tag);
				int __ret = JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_getSpanFlags_Ljava_lang_Object_, __args);
				return __ret;
			} finally {
			}
		}

		static IntPtr id_append_Ljava_lang_CharSequence_;
		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='append' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		[Register ("append", "(Ljava/lang/CharSequence;)V", "GetAppend_Ljava_lang_CharSequence_Handler")]
		public override unsafe void Append (global::Java.Lang.ICharSequence value)
		{
			if (id_append_Ljava_lang_CharSequence_ == IntPtr.Zero)
				id_append_Ljava_lang_CharSequence_ = JNIEnv.GetMethodID (class_ref, "append", "(Ljava/lang/CharSequence;)V");
			IntPtr native_value = CharSequence.ToLocalJniHandle (value);
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native_value);
				JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_append_Ljava_lang_CharSequence_, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_value);
			}
		}

		static IntPtr id_identity_Ljava_lang_CharSequence_;
		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='identity' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		[Register ("identity", "(Ljava/lang/CharSequence;)Ljava/lang/CharSequence;", "GetIdentity_Ljava_lang_CharSequence_Handler")]
		public override unsafe global::Java.Lang.ICharSequence IdentityFormatted (global::Java.Lang.ICharSequence value)
		{
			if (id_identity_Ljava_lang_CharSequence_ == IntPtr.Zero)
				id_identity_Ljava_lang_CharSequence_ = JNIEnv.GetMethodID (class_ref, "identity", "(Ljava/lang/CharSequence;)Ljava/lang/CharSequence;");
			IntPtr native_value = CharSequence.ToLocalJniHandle (value);
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native_value);
				global::Java.Lang.ICharSequence __ret = global::Java.Lang.Object.GetObject<Java.Lang.ICharSequence> (JNIEnv.CallObjectMethod (((global::Java.Lang.Object) this).Handle, id_identity_Ljava_lang_CharSequence_, __args), JniHandleOwnership.TransferLocalRef);
				return __ret;
			} finally {
				JNIEnv.DeleteLocalRef (native_value);
			}
		}

	}

}
