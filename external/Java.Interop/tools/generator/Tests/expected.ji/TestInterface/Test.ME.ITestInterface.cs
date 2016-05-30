using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Test.ME {

	[Register ("test/me/TestInterface", DoNotGenerateAcw=true)]
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

		static JniPeerMembers _members = new JniPeerMembers ("test/me/TestInterface", typeof (TestInterface));
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

	}

	[global::Android.Runtime.Register ("test/me/TestInterface", DoNotGenerateAcw=true)]
	internal class ITestInterfaceInvoker : global::Java.Lang.Object, ITestInterface {

		internal    new     static  readonly    JniPeerMembers  _members    = new JniPeerMembers ("test/me/TestInterface", typeof (ITestInterfaceInvoker));

		static IntPtr java_class_ref {
			get { return _members.JniPeerType.PeerReference.Handle; }
		}

		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
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
			__args [0] = new JValue ((tag == null) ? IntPtr.Zero : ((global::Java.Lang.Object) tag).Handle);
			int __ret = JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_getSpanFlags_Ljava_lang_Object_, __args);
			return __ret;
		}

	}

}
