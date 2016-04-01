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

		internal static IntPtr java_class_handle;
		internal static IntPtr class_ref {
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
			if (Handle != IntPtr.Zero)
				return;

			try {
				if (GetType () != typeof (TestInterfaceImplementation)) {
					SetHandle (
							global::Android.Runtime.JNIEnv.StartCreateInstance (GetType (), "()V"),
							JniHandleOwnership.TransferLocalRef);
					global::Android.Runtime.JNIEnv.FinishCreateInstance (Handle, "()V");
					return;
				}

				if (id_ctor == IntPtr.Zero)
					id_ctor = JNIEnv.GetMethodID (class_ref, "<init>", "()V");
				SetHandle (
						global::Android.Runtime.JNIEnv.StartCreateInstance (class_ref, id_ctor),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, class_ref, id_ctor);
			} finally {
			}
		}

		static Delegate cb_defaultInterfaceMethod;
#pragma warning disable 0169
		static Delegate GetDefaultInterfaceMethodHandler ()
		{
			if (cb_defaultInterfaceMethod == null)
				cb_defaultInterfaceMethod = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr>) n_DefaultInterfaceMethod);
			return cb_defaultInterfaceMethod;
		}

		static void n_DefaultInterfaceMethod (IntPtr jnienv, IntPtr native__this)
		{
			global::Test.ME.TestInterfaceImplementation __this = global::Java.Lang.Object.GetObject<global::Test.ME.TestInterfaceImplementation> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.DefaultInterfaceMethod ();
		}
#pragma warning restore 0169

		static IntPtr id_defaultInterfaceMethod;
		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='TestInterfaceImplementation']/method[@name='defaultInterfaceMethod' and count(parameter)=0]"
		[Register ("defaultInterfaceMethod", "()V", "GetDefaultInterfaceMethodHandler")]
		public virtual unsafe void DefaultInterfaceMethod ()
		{
			if (id_defaultInterfaceMethod == IntPtr.Zero)
				id_defaultInterfaceMethod = JNIEnv.GetMethodID (class_ref, "defaultInterfaceMethod", "()V");
			try {

				if (GetType () == ThresholdType)
					JNIEnv.CallVoidMethod  (Handle, id_defaultInterfaceMethod);
				else
					JNIEnv.CallNonvirtualVoidMethod  (Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "defaultInterfaceMethod", "()V"));
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
			global::Test.ME.TestInterfaceImplementation __this = global::Java.Lang.Object.GetObject<global::Test.ME.TestInterfaceImplementation> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Java.Lang.Object tag = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_tag, JniHandleOwnership.DoNotTransfer);
			int __ret = __this.GetSpanFlags (tag);
			return __ret;
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='TestInterfaceImplementation']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[Register ("getSpanFlags", "(Ljava/lang/Object;)I", "GetGetSpanFlags_Ljava_lang_Object_Handler")]
		public abstract int GetSpanFlags (global::Java.Lang.Object tag);

	}

	[global::Android.Runtime.Register ("test/me/TestInterfaceImplementation", DoNotGenerateAcw=true)]
	internal partial class TestInterfaceImplementationInvoker : TestInterfaceImplementation {

		public TestInterfaceImplementationInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		protected override global::System.Type ThresholdType {
			get { return typeof (TestInterfaceImplementationInvoker); }
		}

		static IntPtr id_getSpanFlags_Ljava_lang_Object_;
		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='TestInterfaceImplementation']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[Register ("getSpanFlags", "(Ljava/lang/Object;)I", "GetGetSpanFlags_Ljava_lang_Object_Handler")]
		public override unsafe int GetSpanFlags (global::Java.Lang.Object tag)
		{
			if (id_getSpanFlags_Ljava_lang_Object_ == IntPtr.Zero)
				id_getSpanFlags_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "getSpanFlags", "(Ljava/lang/Object;)I");
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (tag);
				int __ret = JNIEnv.CallIntMethod  (Handle, id_getSpanFlags_Ljava_lang_Object_, __args);
				return __ret;
			} finally {
			}
		}

	}

}
