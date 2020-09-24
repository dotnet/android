using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']"
	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	public abstract partial class SomeObject : global::Java.Lang.Object {
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

		static Delegate cb_getSomeInteger;
#pragma warning disable 0169
		static Delegate GetGetSomeIntegerHandler ()
		{
			if (cb_getSomeInteger == null)
				cb_getSomeInteger = JNINativeWrapper.CreateDelegate ((_JniMarshal_PP_I) n_GetSomeInteger);
			return cb_getSomeInteger;
		}

		static int n_GetSomeInteger (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return __this.SomeInteger;
		}
#pragma warning restore 0169

		static Delegate cb_setSomeInteger_I;
#pragma warning disable 0169
		static Delegate GetSetSomeInteger_IHandler ()
		{
			if (cb_setSomeInteger_I == null)
				cb_setSomeInteger_I = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPI_V) n_SetSomeInteger_I);
			return cb_setSomeInteger_I;
		}

		static void n_SetSomeInteger_I (IntPtr jnienv, IntPtr native__this, int newvalue)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.SomeInteger = newvalue;
		}
#pragma warning restore 0169

		public abstract int SomeInteger {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeInteger' and count(parameter)=0]"
			[Register ("getSomeInteger", "()I", "GetGetSomeIntegerHandler")]
			get; 

			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeInteger' and count(parameter)=1 and parameter[1][@type='int']]"
			[Register ("setSomeInteger", "(I)V", "GetSetSomeInteger_IHandler")]
			set; 
		}

		static Delegate cb_getSomeObjectProperty;
#pragma warning disable 0169
		static Delegate GetGetSomeObjectPropertyHandler ()
		{
			if (cb_getSomeObjectProperty == null)
				cb_getSomeObjectProperty = JNINativeWrapper.CreateDelegate ((_JniMarshal_PP_L) n_GetSomeObjectProperty);
			return cb_getSomeObjectProperty;
		}

		static IntPtr n_GetSomeObjectProperty (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToLocalJniHandle (__this.SomeObjectProperty);
		}
#pragma warning restore 0169

		static Delegate cb_setSomeObjectProperty_Ljava_lang_Object_;
#pragma warning disable 0169
		static Delegate GetSetSomeObjectProperty_Ljava_lang_Object_Handler ()
		{
			if (cb_setSomeObjectProperty_Ljava_lang_Object_ == null)
				cb_setSomeObjectProperty_Ljava_lang_Object_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPL_V) n_SetSomeObjectProperty_Ljava_lang_Object_);
			return cb_setSomeObjectProperty_Ljava_lang_Object_;
		}

		static void n_SetSomeObjectProperty_Ljava_lang_Object_ (IntPtr jnienv, IntPtr native__this, IntPtr native_newvalue)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var newvalue = global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (native_newvalue, JniHandleOwnership.DoNotTransfer);
			__this.SomeObjectProperty = newvalue;
		}
#pragma warning restore 0169

		public abstract global::Java.Lang.Object SomeObjectProperty {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeObjectProperty' and count(parameter)=0]"
			[Register ("getSomeObjectProperty", "()Ljava/lang/Object;", "GetGetSomeObjectPropertyHandler")]
			get; 

			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeObjectProperty' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
			[Register ("setSomeObjectProperty", "(Ljava/lang/Object;)V", "GetSetSomeObjectProperty_Ljava_lang_Object_Handler")]
			set; 
		}

		static Delegate cb_getSomeString;
#pragma warning disable 0169
		static Delegate GetGetSomeStringHandler ()
		{
			if (cb_getSomeString == null)
				cb_getSomeString = JNINativeWrapper.CreateDelegate ((_JniMarshal_PP_L) n_GetSomeString);
			return cb_getSomeString;
		}

		static IntPtr n_GetSomeString (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.NewString (__this.SomeString);
		}
#pragma warning restore 0169

		static Delegate cb_setSomeString_Ljava_lang_String_;
#pragma warning disable 0169
		static Delegate GetSetSomeString_Ljava_lang_String_Handler ()
		{
			if (cb_setSomeString_Ljava_lang_String_ == null)
				cb_setSomeString_Ljava_lang_String_ = JNINativeWrapper.CreateDelegate ((_JniMarshal_PPL_V) n_SetSomeString_Ljava_lang_String_);
			return cb_setSomeString_Ljava_lang_String_;
		}

		static void n_SetSomeString_Ljava_lang_String_ (IntPtr jnienv, IntPtr native__this, IntPtr native_newvalue)
		{
			var __this = global::Java.Lang.Object.GetObject<global::Xamarin.Test.SomeObject> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var newvalue = JNIEnv.GetString (native_newvalue, JniHandleOwnership.DoNotTransfer);
			__this.SomeString = newvalue;
		}
#pragma warning restore 0169

		public abstract string SomeString {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeString' and count(parameter)=0]"
			[Register ("getSomeString", "()Ljava/lang/String;", "GetGetSomeStringHandler")]
			get; 

			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeString' and count(parameter)=1 and parameter[1][@type='java.lang.String']]"
			[Register ("setSomeString", "(Ljava/lang/String;)V", "GetSetSomeString_Ljava_lang_String_Handler")]
			set; 
		}

	}

	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	internal partial class SomeObjectInvoker : SomeObject {
		public SomeObjectInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/SomeObject", typeof (SomeObjectInvoker));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		protected override global::System.Type ThresholdType {
			get { return _members.ManagedPeerType; }
		}

		public override unsafe int SomeInteger {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeInteger' and count(parameter)=0]"
			[Register ("getSomeInteger", "()I", "GetGetSomeIntegerHandler")]
			get {
				const string __id = "getSomeInteger.()I";
				try {
					var __rm = _members.InstanceMethods.InvokeAbstractInt32Method (__id, this, null);
					return __rm;
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeInteger' and count(parameter)=1 and parameter[1][@type='int']]"
			[Register ("setSomeInteger", "(I)V", "GetSetSomeInteger_IHandler")]
			set {
				const string __id = "setSomeInteger.(I)V";
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (value);
					_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
				} finally {
				}
			}
		}

		public override unsafe global::Java.Lang.Object SomeObjectProperty {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeObjectProperty' and count(parameter)=0]"
			[Register ("getSomeObjectProperty", "()Ljava/lang/Object;", "GetGetSomeObjectPropertyHandler")]
			get {
				const string __id = "getSomeObjectProperty.()Ljava/lang/Object;";
				try {
					var __rm = _members.InstanceMethods.InvokeAbstractObjectMethod (__id, this, null);
					return global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (__rm.Handle, JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeObjectProperty' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
			[Register ("setSomeObjectProperty", "(Ljava/lang/Object;)V", "GetSetSomeObjectProperty_Ljava_lang_Object_Handler")]
			set {
				const string __id = "setSomeObjectProperty.(Ljava/lang/Object;)V";
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue ((value == null) ? IntPtr.Zero : ((global::Java.Lang.Object) value).Handle);
					_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
				} finally {
					global::System.GC.KeepAlive (value);
				}
			}
		}

		public override unsafe string SomeString {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeString' and count(parameter)=0]"
			[Register ("getSomeString", "()Ljava/lang/String;", "GetGetSomeStringHandler")]
			get {
				const string __id = "getSomeString.()Ljava/lang/String;";
				try {
					var __rm = _members.InstanceMethods.InvokeAbstractObjectMethod (__id, this, null);
					return JNIEnv.GetString (__rm.Handle, JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeString' and count(parameter)=1 and parameter[1][@type='java.lang.String']]"
			[Register ("setSomeString", "(Ljava/lang/String;)V", "GetSetSomeString_Ljava_lang_String_Handler")]
			set {
				const string __id = "setSomeString.(Ljava/lang/String;)V";
				IntPtr native_value = JNIEnv.NewString (value);
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (native_value);
					_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

	}
}
