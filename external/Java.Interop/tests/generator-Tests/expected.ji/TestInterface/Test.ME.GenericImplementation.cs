using System;
using System.Collections.Generic;
using Java.Interop;

namespace Test.ME {

	// Metadata.xml XPath class reference: path="/api/package[@name='test.me']/class[@name='GenericImplementation']"
	[global::Java.Interop.JniTypeSignature ("test/me/GenericImplementation", GenerateJavaPeer=false)]
	public partial class GenericImplementation : global::Java.Lang.Object, global::Test.ME.IGenericInterface {
		static readonly JniPeerMembers _members = new JniPeerMembers ("test/me/GenericImplementation", typeof (GenericImplementation));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected GenericImplementation (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='test.me']/class[@name='GenericImplementation']/constructor[@name='GenericImplementation' and count(parameter)=0]"
		public unsafe GenericImplementation () : base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			const string __id = "()V";

			if (PeerReference.IsValid)
				return;

			try {
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), null);
				Construct (ref __r, JniObjectReferenceOptions.CopyAndDispose);
				_members.InstanceMethods.FinishCreateInstance (__id, this, null);
			} finally {
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='GenericImplementation']/method[@name='SetObject' and count(parameter)=1 and parameter[1][@type='byte[]']]"
		public virtual unsafe void SetObject (global::Java.Interop.JavaSByteArray value)
		{
			const string __id = "SetObject.([B)V";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (value);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
			} finally {
				global::System.GC.KeepAlive (value);
			}
		}

		// This method is explicitly implemented as a member of an instantiated Test.ME.IGenericInterface
		void global::Test.ME.IGenericInterface.SetObject (global::Java.Lang.Object value)
		{
			SetObject (global::Java.Interop.JavaObjectExtensions.JavaCast<global::Java.Interop.JavaSByteArray>(value));
		}

	}
}
