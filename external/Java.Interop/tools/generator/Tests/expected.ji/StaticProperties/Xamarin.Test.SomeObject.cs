using System;
using System.Collections.Generic;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']"
	[global::Android.Runtime.Register ("xamarin/test/SomeObject", DoNotGenerateAcw=true)]
	public partial class SomeObject : global::Java.Lang.Object {

		internal static new readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/SomeObject", typeof (SomeObject));
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

		protected SomeObject (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		public static unsafe int SomeInteger {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeInteger' and count(parameter)=0]"
			[Register ("getSomeInteger", "()I", "")]
			get {
				const string __id = "getSomeInteger.()I";
				try {
					var __rm = _members.StaticMethods.InvokeInt32Method (__id, null);
					return __rm;
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeInteger' and count(parameter)=1 and parameter[1][@type='int']]"
			[Register ("setSomeInteger", "(I)V", "")]
			set {
				const string __id = "setSomeInteger.(I)V";
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (value);
					_members.StaticMethods.InvokeVoidMethod (__id, __args);
				} finally {
				}
			}
		}

		public static unsafe string SomeString {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeString' and count(parameter)=0]"
			[Register ("getSomeString", "()Ljava/lang/String;", "")]
			get {
				const string __id = "getSomeString.()Ljava/lang/String;";
				try {
					var __rm = _members.StaticMethods.InvokeObjectMethod (__id, null);
					return JNIEnv.GetString (__rm.Handle, JniHandleOwnership.TransferLocalRef);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeString' and count(parameter)=1 and parameter[1][@type='java.lang.String']]"
			[Register ("setSomeString", "(Ljava/lang/String;)V", "")]
			set {
				const string __id = "setSomeString.(Ljava/lang/String;)V";
				IntPtr native_value = JNIEnv.NewString (value);
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (native_value);
					_members.StaticMethods.InvokeVoidMethod (__id, __args);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getSomeObject' and count(parameter)=0]"
		[Register ("getSomeObject", "()Ljava/lang/Object;", "")]
		public static unsafe global::Java.Lang.Object GetSomeObject ()
		{
			const string __id = "getSomeObject.()Ljava/lang/Object;";
			try {
				var __rm = _members.StaticMethods.InvokeObjectMethod (__id, null);
				return global::Java.Lang.Object.GetObject<global::Java.Lang.Object> (__rm.Handle, JniHandleOwnership.TransferLocalRef);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='setSomeObject' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[Register ("setSomeObject", "(Ljava/lang/Object;)V", "")]
		public static unsafe void SetSomeObject (global::Java.Lang.Object newvalue)
		{
			const string __id = "setSomeObject.(Ljava/lang/Object;)V";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue ((newvalue == null) ? IntPtr.Zero : ((global::Java.Lang.Object) newvalue).Handle);
				_members.StaticMethods.InvokeVoidMethod (__id, __args);
			} finally {
			}
		}

	}
}
