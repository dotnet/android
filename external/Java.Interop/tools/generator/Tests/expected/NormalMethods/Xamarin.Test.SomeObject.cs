using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']"
	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	public partial class SomeObject : global::Java.Lang.Object {

		internal static IntPtr java_class_handle;
		internal static IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("xamarin/test/SomeObject", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (SomeObject); }
		}

		protected SomeObject (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static IntPtr id_ctor_Ljava_lang_Class_;
		// Metadata.xml XPath constructor reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/constructor[@name='SomeObject' and count(parameter)=1 and parameter[1][@type='java.lang.Class&lt;? extends xamarin.test.SomeObject&gt;']]"
		[Register (".ctor", "(Ljava/lang/Class;)V", "")]
		public unsafe SomeObject (global::Java.Lang.Class c)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (c);
				if (((object) this).GetType () != typeof (SomeObject)) {
					SetHandle (
							global::Android.Runtime.JNIEnv.StartCreateInstance (((object) this).GetType (), "(Ljava/lang/Class;)V", __args),
							JniHandleOwnership.TransferLocalRef);
					global::Android.Runtime.JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, "(Ljava/lang/Class;)V", __args);
					return;
				}

				if (id_ctor_Ljava_lang_Class_ == IntPtr.Zero)
					id_ctor_Ljava_lang_Class_ = JNIEnv.GetMethodID (class_ref, "<init>", "(Ljava/lang/Class;)V");
				SetHandle (
						global::Android.Runtime.JNIEnv.StartCreateInstance (class_ref, id_ctor_Ljava_lang_Class_, __args),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, class_ref, id_ctor_Ljava_lang_Class_, __args);
			} finally {
			}
		}

		static Delegate cb_getType;
#pragma warning disable 0169
		static Delegate GetGetTypeHandler ()
		{
			if (cb_getType == null)
				cb_getType = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr>) n_GetType);
			return cb_getType;
		}

		static IntPtr n_GetType (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.NewArray (__this.GetType ());
		}
#pragma warning restore 0169

		static IntPtr id_getType;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getType' and count(parameter)=0]"
		[Register ("getType", "()[I", "GetGetTypeHandler")]
		public virtual unsafe int[] GetType ()
		{
			if (id_getType == IntPtr.Zero)
				id_getType = JNIEnv.GetMethodID (class_ref, "getType", "()[I");
			try {

				if (((object) this).GetType () == ThresholdType)
					return (int[]) JNIEnv.GetArray (JNIEnv.CallObjectMethod (((global::Java.Lang.Object) this).Handle, id_getType), JniHandleOwnership.TransferLocalRef, typeof (int));
				else
					return (int[]) JNIEnv.GetArray (JNIEnv.CallNonvirtualObjectMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "getType", "()[I")), JniHandleOwnership.TransferLocalRef, typeof (int));
			} finally {
			}
		}

		static Delegate cb_handle_Ljava_lang_Object_Ljava_lang_Throwable_;
#pragma warning disable 0169
		static Delegate GetHandle_Ljava_lang_Object_Ljava_lang_Throwable_Handler ()
		{
			if (cb_handle_Ljava_lang_Object_Ljava_lang_Throwable_ == null)
				cb_handle_Ljava_lang_Object_Ljava_lang_Throwable_ = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr, IntPtr, int>) n_Handle_Ljava_lang_Object_Ljava_lang_Throwable_);
			return cb_handle_Ljava_lang_Object_Ljava_lang_Throwable_;
		}

		static int n_Handle_Ljava_lang_Object_Ljava_lang_Throwable_ (IntPtr jnienv, IntPtr native__this, IntPtr native_o, IntPtr native_t)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			global::Java.Lang.Object o = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_o, JniHandleOwnership.DoNotTransfer);
			global::Java.Lang.Throwable t = global::Java.Lang.Object.GetObject<global::Java.Lang.Throwable> (native_t, JniHandleOwnership.DoNotTransfer);
			int __ret = __this.Handle (o, t);
			return __ret;
		}
#pragma warning restore 0169

		static IntPtr id_handle_Ljava_lang_Object_Ljava_lang_Throwable_;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='handle' and count(parameter)=2 and parameter[1][@type='java.lang.Object'] and parameter[2][@type='java.lang.Throwable']]"
		[Register ("handle", "(Ljava/lang/Object;Ljava/lang/Throwable;)I", "GetHandle_Ljava_lang_Object_Ljava_lang_Throwable_Handler")]
		public new virtual unsafe int Handle (global::Java.Lang.Object o, global::Java.Lang.Throwable t)
		{
			if (id_handle_Ljava_lang_Object_Ljava_lang_Throwable_ == IntPtr.Zero)
				id_handle_Ljava_lang_Object_Ljava_lang_Throwable_ = JNIEnv.GetMethodID (class_ref, "handle", "(Ljava/lang/Object;Ljava/lang/Throwable;)I");
			try {
				JValue* __args = stackalloc JValue [2];
				__args [0] = new JValue (o);
				__args [1] = new JValue (t);

				int __ret;
				if (((object) this).GetType () == ThresholdType)
					__ret = JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_handle_Ljava_lang_Object_Ljava_lang_Throwable_, __args);
				else
					__ret = JNIEnv.CallNonvirtualIntMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "handle", "(Ljava/lang/Object;Ljava/lang/Throwable;)I"), __args);
				return __ret;
			} finally {
			}
		}

		static Delegate cb_IntegerMethod;
#pragma warning disable 0169
		static Delegate GetIntegerMethodHandler ()
		{
			if (cb_IntegerMethod == null)
				cb_IntegerMethod = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, int>) n_IntegerMethod);
			return cb_IntegerMethod;
		}

		static int n_IntegerMethod (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.IntegerMethod ();
		}
#pragma warning restore 0169

		static IntPtr id_IntegerMethod;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='IntegerMethod' and count(parameter)=0]"
		[Register ("IntegerMethod", "()I", "GetIntegerMethodHandler")]
		public virtual unsafe int IntegerMethod ()
		{
			if (id_IntegerMethod == IntPtr.Zero)
				id_IntegerMethod = JNIEnv.GetMethodID (class_ref, "IntegerMethod", "()I");
			try {

				if (((object) this).GetType () == ThresholdType)
					return JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_IntegerMethod);
				else
					return JNIEnv.CallNonvirtualIntMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "IntegerMethod", "()I"));
			} finally {
			}
		}

		static Delegate cb_VoidMethod;
#pragma warning disable 0169
		static Delegate GetVoidMethodHandler ()
		{
			if (cb_VoidMethod == null)
				cb_VoidMethod = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr>) n_VoidMethod);
			return cb_VoidMethod;
		}

		static void n_VoidMethod (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.VoidMethod ();
		}
#pragma warning restore 0169

		static IntPtr id_VoidMethod;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='VoidMethod' and count(parameter)=0]"
		[Register ("VoidMethod", "()V", "GetVoidMethodHandler")]
		public virtual unsafe void VoidMethod ()
		{
			if (id_VoidMethod == IntPtr.Zero)
				id_VoidMethod = JNIEnv.GetMethodID (class_ref, "VoidMethod", "()V");
			try {

				if (((object) this).GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_VoidMethod);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "VoidMethod", "()V"));
			} finally {
			}
		}

		static Delegate cb_StringMethod;
#pragma warning disable 0169
		static Delegate GetStringMethodHandler ()
		{
			if (cb_StringMethod == null)
				cb_StringMethod = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr>) n_StringMethod);
			return cb_StringMethod;
		}

		static IntPtr n_StringMethod (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.NewString (__this.StringMethod ());
		}
#pragma warning restore 0169

		static IntPtr id_StringMethod;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='StringMethod' and count(parameter)=0]"
		[Register ("StringMethod", "()Ljava/lang/String;", "GetStringMethodHandler")]
		public virtual unsafe string StringMethod ()
		{
			if (id_StringMethod == IntPtr.Zero)
				id_StringMethod = JNIEnv.GetMethodID (class_ref, "StringMethod", "()Ljava/lang/String;");
			try {

				if (((object) this).GetType () == ThresholdType)
					return JNIEnv.GetString (JNIEnv.CallObjectMethod (((global::Java.Lang.Object) this).Handle, id_StringMethod), JniHandleOwnership.TransferLocalRef);
				else
					return JNIEnv.GetString (JNIEnv.CallNonvirtualObjectMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "StringMethod", "()Ljava/lang/String;")), JniHandleOwnership.TransferLocalRef);
			} finally {
			}
		}

		static Delegate cb_ObjectMethod;
#pragma warning disable 0169
		static Delegate GetObjectMethodHandler ()
		{
			if (cb_ObjectMethod == null)
				cb_ObjectMethod = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr>) n_ObjectMethod);
			return cb_ObjectMethod;
		}

		static IntPtr n_ObjectMethod (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToLocalJniHandle (__this.ObjectMethod ());
		}
#pragma warning restore 0169

		static IntPtr id_ObjectMethod;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='ObjectMethod' and count(parameter)=0]"
		[Register ("ObjectMethod", "()Ljava/lang/Object;", "GetObjectMethodHandler")]
		public virtual unsafe global::Java.Lang.Object ObjectMethod ()
		{
			if (id_ObjectMethod == IntPtr.Zero)
				id_ObjectMethod = JNIEnv.GetMethodID (class_ref, "ObjectMethod", "()Ljava/lang/Object;");
			try {

				if (((object) this).GetType () == ThresholdType)
					return global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (JNIEnv.CallObjectMethod (((global::Java.Lang.Object) this).Handle, id_ObjectMethod), JniHandleOwnership.TransferLocalRef);
				else
					return global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (JNIEnv.CallNonvirtualObjectMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "ObjectMethod", "()Ljava/lang/Object;")), JniHandleOwnership.TransferLocalRef);
			} finally {
			}
		}

		static Delegate cb_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetVoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_Handler ()
		{
			if (cb_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_ == null)
				cb_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr, int, IntPtr>) n_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_);
			return cb_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_;
		}

		static void n_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_astring, int anint, IntPtr native_anObject)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			string astring = JNIEnv.GetString (native_astring, JniHandleOwnership.DoNotTransfer);
			global::Java.Lang.Object anObject = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_anObject, JniHandleOwnership.DoNotTransfer);
			__this.VoidMethodWithParams (astring, anint, anObject);
		}
#pragma warning restore 0169

		static IntPtr id_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='VoidMethodWithParams' and count(parameter)=3 and parameter[1][@type='java.lang.String'] and parameter[2][@type='int'] and parameter[3][@type='java.lang.Object']]"
		[Register ("VoidMethodWithParams", "(Ljava/lang/String;ILjava/lang/Object;)V", "GetVoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_Handler")]
		public virtual unsafe void VoidMethodWithParams (string astring, int anint, global::Java.Lang.Object anObject)
		{
			if (id_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_ == IntPtr.Zero)
				id_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "VoidMethodWithParams", "(Ljava/lang/String;ILjava/lang/Object;)V");
			IntPtr native_astring = JNIEnv.NewString (astring);
			try {
				JValue* __args = stackalloc JValue [3];
				__args [0] = new JValue (native_astring);
				__args [1] = new JValue (anint);
				__args [2] = new JValue (anObject);

				if (((object) this).GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_, __args);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "VoidMethodWithParams", "(Ljava/lang/String;ILjava/lang/Object;)V"), __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_astring);
			}
		}

		static Delegate cb_ObsoleteMethod;
#pragma warning disable 0169
		static Delegate GetObsoleteMethodHandler ()
		{
			if (cb_ObsoleteMethod == null)
				cb_ObsoleteMethod = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, int>) n_ObsoleteMethod);
			return cb_ObsoleteMethod;
		}

		static int n_ObsoleteMethod (IntPtr jnienv, IntPtr native__this)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.ObsoleteMethod ();
		}
#pragma warning restore 0169

		static IntPtr id_ObsoleteMethod;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='ObsoleteMethod' and count(parameter)=0]"
		[Obsolete (@"Deprecated please use IntegerMethod instead")]
		[Register ("ObsoleteMethod", "()I", "GetObsoleteMethodHandler")]
		public virtual unsafe int ObsoleteMethod ()
		{
			if (id_ObsoleteMethod == IntPtr.Zero)
				id_ObsoleteMethod = JNIEnv.GetMethodID (class_ref, "ObsoleteMethod", "()I");
			try {

				if (((object) this).GetType () == ThresholdType)
					return JNIEnv.CallIntMethod (((global::Java.Lang.Object) this).Handle, id_ObsoleteMethod);
				else
					return JNIEnv.CallNonvirtualIntMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "ObsoleteMethod", "()I"));
			} finally {
			}
		}

		static Delegate cb_ArrayListTest_Ljava_util_ArrayList_;
#pragma warning disable 0169
		static Delegate GetArrayListTest_Ljava_util_ArrayList_Handler ()
		{
			if (cb_ArrayListTest_Ljava_util_ArrayList_ == null)
				cb_ArrayListTest_Ljava_util_ArrayList_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_ArrayListTest_Ljava_util_ArrayList_);
			return cb_ArrayListTest_Ljava_util_ArrayList_;
		}

		static void n_ArrayListTest_Ljava_util_ArrayList_ (IntPtr jnienv, IntPtr native__this, IntPtr native_p0)
		{
			global::Xamarin.Test.SomeObject __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var p0 = global::Android.Runtime.JavaList<global::Java.Lang.Integer>.FromJniHandle (native_p0, JniHandleOwnership.DoNotTransfer);
			__this.ArrayListTest (p0);
		}
#pragma warning restore 0169

		static IntPtr id_ArrayListTest_Ljava_util_ArrayList_;
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='ArrayListTest' and count(parameter)=1 and parameter[1][@type='java.util.ArrayList&lt;java.lang.Integer&gt;']]"
		[Register ("ArrayListTest", "(Ljava/util/ArrayList;)V", "GetArrayListTest_Ljava_util_ArrayList_Handler")]
		public virtual unsafe void ArrayListTest (global::System.Collections.Generic.IList<global::Java.Lang.Integer> p0)
		{
			if (id_ArrayListTest_Ljava_util_ArrayList_ == IntPtr.Zero)
				id_ArrayListTest_Ljava_util_ArrayList_ = JNIEnv.GetMethodID (class_ref, "ArrayListTest", "(Ljava/util/ArrayList;)V");
			IntPtr native_p0 = global::Android.Runtime.JavaList<global::Java.Lang.Integer>.ToLocalJniHandle (p0);
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native_p0);

				if (((object) this).GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_ArrayListTest_Ljava_util_ArrayList_, __args);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "ArrayListTest", "(Ljava/util/ArrayList;)V"), __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_p0);
			}
		}

	}
}
