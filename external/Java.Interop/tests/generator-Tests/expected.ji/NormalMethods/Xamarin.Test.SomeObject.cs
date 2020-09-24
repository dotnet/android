using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']"
	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	public partial class SomeObject : global::Java.Lang.Object {
		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/SomeObject", typeof (SomeObject));

		internal static new IntPtr class_ref {
			get { return _members.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override IntPtr ThresholdClass {
			get { return _members.JniPeerType.PeerReference.Handle; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

		protected SomeObject (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer)
		{
		}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/constructor[@name='SomeObject' and count(parameter)=1 and parameter[1][@type='java.lang.Class&lt;? extends xamarin.test.SomeObject&gt;']]"
		[Register (".ctor", "(Ljava/lang/Class;)V", "")]
		public unsafe SomeObject (global::Java.Lang.Class c) : base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			const string __id = "(Ljava/lang/Class;)V";

			if (((global::Java.Lang.Object) this).Handle != IntPtr.Zero)
				return;

			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue ((c == null) ? IntPtr.Zero : ((global::Java.Lang.Object) c).Handle);
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), __args);
				SetHandle (__r.Handle, JniHandleOwnership.TransferLocalRef);
				_members.InstanceMethods.FinishCreateInstance (__id, this, __args);
			} finally {
				global::System.GC.KeepAlive (c);
			}
		}

		static Delegate cb_getType;
#pragma warning disable 0169
		static Delegate GetGetTypeHandler ()
		{
			if (cb_getType == null)
				cb_getType = JNINativeWrapper.CreateDelegate ((_JniMarshal_PP_L) n_GetType);
			return cb_getType;
		}

		static IntPtr n_GetType (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.NewArray (__this.GetType ());
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getType' and count(parameter)=0]"
		[Register ("getType", "()[I", "GetGetTypeHandler")]
		public new virtual unsafe int[] GetType ()
		{
			const string __id = "getType.()[I";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
				return (int[]) JNIEnv.GetArray (__rm.Handle, JniHandleOwnership.TransferLocalRef, typeof (int));
			} finally {
			}
		}

		static Delegate cb_handle_Ljava_lang_Object_Ljava_lang_Throwable_;
#pragma warning disable 0169
		static Delegate GetHandle_Ljava_lang_Object_Ljava_lang_Throwable_Handler ()
		{
			if (cb_handle_Ljava_lang_Object_Ljava_lang_Throwable_ == null)
				cb_handle_Ljava_lang_Object_Ljava_lang_Throwable_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPLL_I) n_Handle_Ljava_lang_Object_Ljava_lang_Throwable_);
			return cb_handle_Ljava_lang_Object_Ljava_lang_Throwable_;
		}

		static int n_Handle_Ljava_lang_Object_Ljava_lang_Throwable_ (IntPtr jnienv, IntPtr native__this, IntPtr native_o, IntPtr native_t)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var o = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_o, JniHandleOwnership.DoNotTransfer);
			var t = global::Java.Lang.Object.GetObject<global::Java.Lang.Throwable> (native_t, JniHandleOwnership.DoNotTransfer);
			int __ret = __this.Handle (o, t);
			return __ret;
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='handle' and count(parameter)=2 and parameter[1][@type='java.lang.Object'] and parameter[2][@type='java.lang.Throwable']]"
		[Register ("handle", "(Ljava/lang/Object;Ljava/lang/Throwable;)I", "GetHandle_Ljava_lang_Object_Ljava_lang_Throwable_Handler")]
		public new virtual unsafe int Handle (global::Java.Lang.Object o, global::Java.Lang.Throwable t)
		{
			const string __id = "handle.(Ljava/lang/Object;Ljava/lang/Throwable;)I";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [2];
				__args [0] = new JniArgumentValue ((o == null) ? IntPtr.Zero : ((global::Java.Lang.Object) o).Handle);
				__args [1] = new JniArgumentValue ((t == null) ? IntPtr.Zero : ((global::Java.Lang.Throwable) t).Handle);
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, __args);
				return __rm;
			} finally {
				global::System.GC.KeepAlive (o);
				global::System.GC.KeepAlive (t);
			}
		}

		static Delegate cb_IntegerMethod;
#pragma warning disable 0169
		static Delegate GetIntegerMethodHandler ()
		{
			if (cb_IntegerMethod == null)
				cb_IntegerMethod = JNINativeWrapper.CreateDelegate ((_JniMarshal_PP_I) n_IntegerMethod);
			return cb_IntegerMethod;
		}

		static int n_IntegerMethod (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.IntegerMethod ();
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='IntegerMethod' and count(parameter)=0]"
		[Register ("IntegerMethod", "()I", "GetIntegerMethodHandler")]
		public virtual unsafe int IntegerMethod ()
		{
			const string __id = "IntegerMethod.()I";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, null);
				return __rm;
			} finally {
			}
		}

		static Delegate cb_VoidMethod;
#pragma warning disable 0169
		static Delegate GetVoidMethodHandler ()
		{
			if (cb_VoidMethod == null)
				cb_VoidMethod = JNINativeWrapper.CreateDelegate ((_JniMarshal_PP_V) n_VoidMethod);
			return cb_VoidMethod;
		}

		static void n_VoidMethod (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.VoidMethod ();
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='VoidMethod' and count(parameter)=0]"
		[Register ("VoidMethod", "()V", "GetVoidMethodHandler")]
		public virtual unsafe void VoidMethod ()
		{
			const string __id = "VoidMethod.()V";
			try {
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, null);
			} finally {
			}
		}

		static Delegate cb_StringMethod;
#pragma warning disable 0169
		static Delegate GetStringMethodHandler ()
		{
			if (cb_StringMethod == null)
				cb_StringMethod = JNINativeWrapper.CreateDelegate ((_JniMarshal_PP_L) n_StringMethod);
			return cb_StringMethod;
		}

		static IntPtr n_StringMethod (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.NewString (__this.StringMethod ());
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='StringMethod' and count(parameter)=0]"
		[Register ("StringMethod", "()Ljava/lang/String;", "GetStringMethodHandler")]
		public virtual unsafe string StringMethod ()
		{
			const string __id = "StringMethod.()Ljava/lang/String;";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
				return JNIEnv.GetString (__rm.Handle, JniHandleOwnership.TransferLocalRef);
			} finally {
			}
		}

		static Delegate cb_ObjectMethod;
#pragma warning disable 0169
		static Delegate GetObjectMethodHandler ()
		{
			if (cb_ObjectMethod == null)
				cb_ObjectMethod = JNINativeWrapper.CreateDelegate ((_JniMarshal_PP_L) n_ObjectMethod);
			return cb_ObjectMethod;
		}

		static IntPtr n_ObjectMethod (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToLocalJniHandle (__this.ObjectMethod ());
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='ObjectMethod' and count(parameter)=0]"
		[Register ("ObjectMethod", "()Ljava/lang/Object;", "GetObjectMethodHandler")]
		public virtual unsafe global::Java.Lang.Object ObjectMethod ()
		{
			const string __id = "ObjectMethod.()Ljava/lang/Object;";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
				return global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (__rm.Handle, JniHandleOwnership.TransferLocalRef);
			} finally {
			}
		}

		static Delegate cb_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetVoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_Handler ()
		{
			if (cb_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_ == null)
				cb_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPLIL_V) n_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_);
			return cb_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_;
		}

		static void n_VoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_astring, int anint, IntPtr native_anObject)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var astring = JNIEnv.GetString (native_astring, JniHandleOwnership.DoNotTransfer);
			var anObject = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_anObject, JniHandleOwnership.DoNotTransfer);
			__this.VoidMethodWithParams (astring, anint, anObject);
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='VoidMethodWithParams' and count(parameter)=3 and parameter[1][@type='java.lang.String'] and parameter[2][@type='int'] and parameter[3][@type='java.lang.Object']]"
		[Register ("VoidMethodWithParams", "(Ljava/lang/String;ILjava/lang/Object;)V", "GetVoidMethodWithParams_Ljava_lang_String_ILjava_lang_Object_Handler")]
		public virtual unsafe void VoidMethodWithParams (string astring, int anint, global::Java.Lang.Object anObject)
		{
			const string __id = "VoidMethodWithParams.(Ljava/lang/String;ILjava/lang/Object;)V";
			IntPtr native_astring = JNIEnv.NewString (astring);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [3];
				__args [0] = new JniArgumentValue (native_astring);
				__args [1] = new JniArgumentValue (anint);
				__args [2] = new JniArgumentValue ((anObject == null) ? IntPtr.Zero : ((global::Java.Lang.Object) anObject).Handle);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_astring);
				global::System.GC.KeepAlive (anObject);
			}
		}

		static Delegate cb_ObsoleteMethod;
#pragma warning disable 0169
		[Obsolete]
		static Delegate GetObsoleteMethodHandler ()
		{
			if (cb_ObsoleteMethod == null)
				cb_ObsoleteMethod = JNINativeWrapper.CreateDelegate ((_JniMarshal_PP_I) n_ObsoleteMethod);
			return cb_ObsoleteMethod;
		}

		[Obsolete]
		static int n_ObsoleteMethod (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.ObsoleteMethod ();
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='ObsoleteMethod' and count(parameter)=0]"
		[Obsolete (@"Deprecated please use IntegerMethod instead")]
		[Register ("ObsoleteMethod", "()I", "GetObsoleteMethodHandler")]
		public virtual unsafe int ObsoleteMethod ()
		{
			const string __id = "ObsoleteMethod.()I";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, null);
				return __rm;
			} finally {
			}
		}

		static Delegate cb_ArrayListTest_Ljava_util_ArrayList_;
#pragma warning disable 0169
		static Delegate GetArrayListTest_Ljava_util_ArrayList_Handler ()
		{
			if (cb_ArrayListTest_Ljava_util_ArrayList_ == null)
				cb_ArrayListTest_Ljava_util_ArrayList_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPL_V) n_ArrayListTest_Ljava_util_ArrayList_);
			return cb_ArrayListTest_Ljava_util_ArrayList_;
		}

		static void n_ArrayListTest_Ljava_util_ArrayList_ (IntPtr jnienv, IntPtr native__this, IntPtr native_p0)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var p0 = global::Android.Runtime.JavaList<global::Java.Lang.Integer>.FromJniHandle (native_p0, JniHandleOwnership.DoNotTransfer);
			__this.ArrayListTest (p0);
		}
#pragma warning restore 0169

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='ArrayListTest' and count(parameter)=1 and parameter[1][@type='java.util.ArrayList&lt;java.lang.Integer&gt;']]"
		[Register ("ArrayListTest", "(Ljava/util/ArrayList;)V", "GetArrayListTest_Ljava_util_ArrayList_Handler")]
		public virtual unsafe void ArrayListTest (global::System.Collections.Generic.IList<global::Java.Lang.Integer> p0)
		{
			const string __id = "ArrayListTest.(Ljava/util/ArrayList;)V";
			IntPtr native_p0 = global::Android.Runtime.JavaList<global::Java.Lang.Integer>.ToLocalJniHandle (p0);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_p0);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_p0);
				global::System.GC.KeepAlive (p0);
			}
		}

	}
}
