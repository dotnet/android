// Java allows a type to redeclare a member that a base type or base interface already
// declares, including members that are hand-bound instead of generated.
#pragma warning disable 0108, 0114
// Java types may declare a `finalize()` method, which is bound as `Finalize()`.
#pragma warning disable 0465
// Java deprecates types and members independently of the APIs that use, declare or
// override them, so a binding that is not deprecated can still reference one that is.
#pragma warning disable 0618, 0672, 0809
// Java nullness annotations are not required to agree between a member and the member
// it overrides or implements, and are absent from much of the API surface.
#pragma warning disable 8603, 8604, 8625, 8764, 8765, 8766, 8767, 8768

using System;
using System.Collections.Generic;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']"
	[global::Java.Interop.JniTypeSignature ("xamarin/test/SomeObject", GenerateJavaPeer=false)]
	public partial class SomeObject : global::Java.Lang.Object {
		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/SomeObject", typeof (SomeObject));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected SomeObject (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/constructor[@name='SomeObject' and count(parameter)=1 and parameter[1][@type='java.lang.Class&lt;? extends xamarin.test.SomeObject&gt;']]"
		public unsafe SomeObject (global::Java.Lang.Class c) : base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			const string __id = "(Ljava/lang/Class;)V";

			if (PeerReference.IsValid)
				return;

			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (c);
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), __args);
				Construct (ref __r, JniObjectReferenceOptions.CopyAndDispose);
				_members.InstanceMethods.FinishCreateInstance (__id, this, __args);
			} finally {
				global::System.GC.KeepAlive (c);
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='getType' and count(parameter)=0]"
		public new virtual unsafe global::Java.Interop.JavaInt32Array GetType ()
		{
			const string __id = "getType.()[I";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
				return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::Java.Interop.JavaInt32Array>(ref __rm, JniObjectReferenceOptions.CopyAndDispose);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='handle' and count(parameter)=2 and parameter[1][@type='java.lang.Object'] and parameter[2][@type='java.lang.Throwable']]"
		public new virtual unsafe int Handle (global::Java.Lang.Object o, global::Java.Lang.Throwable t)
		{
			const string __id = "handle.(Ljava/lang/Object;Ljava/lang/Throwable;)I";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [2];
				__args [0] = new JniArgumentValue (o);
				__args [1] = new JniArgumentValue (t);
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, __args);
				return __rm;
			} finally {
				global::System.GC.KeepAlive (o);
				global::System.GC.KeepAlive (t);
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='IntegerMethod' and count(parameter)=0]"
		public virtual unsafe int IntegerMethod ()
		{
			const string __id = "IntegerMethod.()I";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, null);
				return __rm;
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='VoidMethod' and count(parameter)=0]"
		public virtual unsafe void VoidMethod ()
		{
			const string __id = "VoidMethod.()V";
			try {
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, null);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='StringMethod' and count(parameter)=0]"
		public virtual unsafe string StringMethod ()
		{
			const string __id = "StringMethod.()Ljava/lang/String;";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
				return global::Java.Interop.JniEnvironment.Strings.ToString (ref __rm, JniObjectReferenceOptions.CopyAndDispose);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='ObjectMethod' and count(parameter)=0]"
		public virtual unsafe global::Java.Lang.Object ObjectMethod ()
		{
			const string __id = "ObjectMethod.()Ljava/lang/Object;";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
				return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::Java.Lang.Object> (ref __rm, JniObjectReferenceOptions.CopyAndDispose);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='VoidMethodWithParams' and count(parameter)=3 and parameter[1][@type='java.lang.String'] and parameter[2][@type='int'] and parameter[3][@type='java.lang.Object']]"
		public virtual unsafe void VoidMethodWithParams (string astring, int anint, global::Java.Lang.Object anObject)
		{
			const string __id = "VoidMethodWithParams.(Ljava/lang/String;ILjava/lang/Object;)V";
			var native_astring = global::Java.Interop.JniEnvironment.Strings.NewString (astring);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [3];
				__args [0] = new JniArgumentValue (native_astring);
				__args [1] = new JniArgumentValue (anint);
				__args [2] = new JniArgumentValue (anObject);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
			} finally {
				global::Java.Interop.JniObjectReference.Dispose (ref native_astring);
				global::System.GC.KeepAlive (anObject);
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='ObsoleteMethod' and count(parameter)=0]"
		[global::System.Obsolete (@"Deprecated please use IntegerMethod instead")]
		public virtual unsafe int ObsoleteMethod ()
		{
			const string __id = "ObsoleteMethod.()I";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualInt32Method (__id, this, null);
				return __rm;
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='SomeObject']/method[@name='ArrayListTest' and count(parameter)=1 and parameter[1][@type='java.util.ArrayList&lt;java.lang.Integer&gt;']]"
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
