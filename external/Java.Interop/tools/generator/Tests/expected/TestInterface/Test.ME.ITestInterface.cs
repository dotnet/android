using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Test.ME {

	[Register ("test/me/TestInterface", DoNotGenerateAcw=true)]
	public abstract class TestInterface : Java.Lang.Object {

		internal TestInterface ()
		{
		}

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

		new static IntPtr class_ref = JNIEnv.FindClass ("test/me/TestInterface");
	}

	[Register ("test/me/TestInterface", DoNotGenerateAcw=true)]
	[global::System.Obsolete ("Use the 'TestInterface' type. This type will be removed in a future release.")]
	public abstract class TestInterfaceConsts : TestInterface {

		private TestInterfaceConsts ()
		{
		}
	}

	// Metadata.xml XPath interface reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']"
	[Register ("test/me/TestInterface", "", "Test.ME.ITestInterfaceInvoker")]
	public partial interface ITestInterface : IJavaObject {

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
			global::Java.Lang.String jls_value = value == null ? null : new global::Java.Lang.String (value);
			self.Append (jls_value);
			jls_value?.Dispose ();
		}

		public static string Identity (this Test.ME.ITestInterface self, string value)
		{
			global::Java.Lang.String jls_value = value == null ? null : new global::Java.Lang.String (value);
			global::Java.Lang.ICharSequence __result = self.IdentityFormatted (jls_value);
			var __rsval = __result?.ToString ();
			jls_value?.Dispose ();
			return __rsval;
		}
	}

	[global::Android.Runtime.Register ("test/me/TestInterface", DoNotGenerateAcw=true)]
	internal class ITestInterfaceInvoker : global::Java.Lang.Object, ITestInterface {

		static IntPtr java_class_ref = JNIEnv.FindClass ("test/me/TestInterface");

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (ITestInterfaceInvoker); }
		}

		IntPtr class_ref;

		public static ITestInterface GetObject (IntPtr handle, JniHandleOwnership transfer)
		{
			return global::Java.Lang.Object.GetObject<ITestInterface> (handle, transfer);
		}

		static IntPtr Validate (IntPtr handle)
		{
			if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
				throw new InvalidCastException (string.Format ("Unable to convert instance of type '{0}' to type '{1}'.",
							JNIEnv.GetClassNameFromInstance (handle), "test.me.TestInterface"));
			return handle;
		}

		protected override void Dispose (bool disposing)
		{
			if (this.class_ref != IntPtr.Zero)
				JNIEnv.DeleteGlobalRef (this.class_ref);
			this.class_ref = IntPtr.Zero;
			base.Dispose (disposing);
		}

		public ITestInterfaceInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
		{
			IntPtr local_ref = JNIEnv.GetObjectClass (((global::Java.Lang.Object) this).Handle);
			this.class_ref = JNIEnv.NewGlobalRef (local_ref);
			JNIEnv.DeleteLocalRef (local_ref);
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
			global::Test.ME.ITestInterface __this = global::Java.Lang.Object.GetObject<global::Test.ME.ITestInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Java.Lang.Object tag = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_tag, JniHandleOwnership.DoNotTransfer);
			int __ret = __this.GetSpanFlags (tag);
			return __ret;
		}
#pragma warning restore 0169

		IntPtr id_getSpanFlags_Ljava_lang_Object_;
		public unsafe int GetSpanFlags (global::Java.Lang.Object tag)
		{
			if (id_getSpanFlags_Ljava_lang_Object_ == IntPtr.Zero)
				id_getSpanFlags_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "getSpanFlags", "(Ljava/lang/Object;)I");
			JValue* __args = stackalloc JValue [1];
			__args [0] = new JValue (tag);
			int __ret = JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_getSpanFlags_Ljava_lang_Object_, __args);
			return __ret;
		}

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
			global::Test.ME.ITestInterface __this = global::Java.Lang.Object.GetObject<global::Test.ME.ITestInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Java.Lang.ICharSequence value = global::Java.Lang.Object.GetObject<global::Java.Lang.ICharSequence> (native_value, JniHandleOwnership.DoNotTransfer);
			__this.Append (value);
		}
#pragma warning restore 0169

		IntPtr id_append_Ljava_lang_CharSequence_;
		public unsafe void Append (global::Java.Lang.ICharSequence value)
		{
			if (id_append_Ljava_lang_CharSequence_ == IntPtr.Zero)
				id_append_Ljava_lang_CharSequence_ = JNIEnv.GetMethodID (class_ref, "append", "(Ljava/lang/CharSequence;)V");
			IntPtr native_value = CharSequence.ToLocalJniHandle (value);
			JValue* __args = stackalloc JValue [1];
			__args [0] = new JValue (native_value);
			JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_append_Ljava_lang_CharSequence_, __args);
			JNIEnv.DeleteLocalRef (native_value);
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
			global::Test.ME.ITestInterface __this = global::Java.Lang.Object.GetObject<global::Test.ME.ITestInterface> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Java.Lang.ICharSequence value = global::Java.Lang.Object.GetObject<global::Java.Lang.ICharSequence> (native_value, JniHandleOwnership.DoNotTransfer);
			IntPtr __ret = CharSequence.ToLocalJniHandle (__this.IdentityFormatted (value));
			return __ret;
		}
#pragma warning restore 0169

		IntPtr id_identity_Ljava_lang_CharSequence_;
		public unsafe global::Java.Lang.ICharSequence IdentityFormatted (global::Java.Lang.ICharSequence value)
		{
			if (id_identity_Ljava_lang_CharSequence_ == IntPtr.Zero)
				id_identity_Ljava_lang_CharSequence_ = JNIEnv.GetMethodID (class_ref, "identity", "(Ljava/lang/CharSequence;)Ljava/lang/CharSequence;");
			IntPtr native_value = CharSequence.ToLocalJniHandle (value);
			JValue* __args = stackalloc JValue [1];
			__args [0] = new JValue (native_value);
			global::Java.Lang.ICharSequence __ret = global::Java.Lang.Object.GetObject<Java.Lang.ICharSequence> (JNIEnv.CallObjectMethod (((global::Java.Lang.Object) this).Handle, id_identity_Ljava_lang_CharSequence_, __args), JniHandleOwnership.TransferLocalRef);
			JNIEnv.DeleteLocalRef (native_value);
			return __ret;
		}

	}

}
