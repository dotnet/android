using System;
using System.Collections.Generic;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath class reference: path="/api/package[@name='xamarin.test']/class[@name='AbsSpinner']"
	[global::Java.Interop.JniTypeSignature ("xamarin/test/AbsSpinner", GenerateJavaPeer=false)]
	public abstract partial class AbsSpinner : Xamarin.Test.AdapterView<Xamarin.Test.ISpinnerAdapter> {
		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/AbsSpinner", typeof (AbsSpinner));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected AbsSpinner (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		public override unsafe global::Xamarin.Test.ISpinnerAdapter Adapter {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='AbsSpinner']/method[@name='getAdapter' and count(parameter)=0]"
			get {
				const string __id = "getAdapter.()Lxamarin/test/SpinnerAdapter;";
				try {
					var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
					return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::Xamarin.Test.ISpinnerAdapter> (ref __rm, JniObjectReferenceOptions.CopyAndDispose);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='AbsSpinner']/method[@name='setAdapter' and count(parameter)=1 and parameter[1][@type='xamarin.test.SpinnerAdapter']]"
			set {
				const string __id = "setAdapter.(Lxamarin/test/SpinnerAdapter;)V";
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (value);
					_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
				} finally {
					global::System.GC.KeepAlive (value);
				}
			}
		}

	}

	[global::Java.Interop.JniTypeSignature ("xamarin/test/AbsSpinner", GenerateJavaPeer=false)]
	internal partial class AbsSpinnerInvoker : AbsSpinner {
		public AbsSpinnerInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		static readonly JniPeerMembers _members = new JniPeerMembers ("xamarin/test/AbsSpinner", typeof (AbsSpinnerInvoker));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected override unsafe global::Java.Lang.Object RawAdapter {
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='AdapterView']/method[@name='getAdapter' and count(parameter)=0]"
			get {
				const string __id = "getAdapter.()Lxamarin/test/Adapter;";
				try {
					var __rm = _members.InstanceMethods.InvokeAbstractObjectMethod (__id, this, null);
					return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::global::Java.Lang.Object>(ref __rm, JniObjectReferenceOptions.CopyAndDispose);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/class[@name='AdapterView']/method[@name='setAdapter' and count(parameter)=1 and parameter[1][@type='T']]"
			set {
				const string __id = "setAdapter.(Lxamarin/test/Adapter;)V";
				IntPtr native_value = JNIEnv.ToLocalJniHandle (value);
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (native_value);
					_members.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
					global::System.GC.KeepAlive (value);
				}
			}
		}

	}
}
