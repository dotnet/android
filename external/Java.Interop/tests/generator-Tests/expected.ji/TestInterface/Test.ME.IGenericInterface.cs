using System;
using System.Collections.Generic;
using Java.Interop;

namespace Test.ME {

	// Metadata.xml XPath interface reference: path="/api/package[@name='test.me']/interface[@name='GenericInterface']"
	[global::Java.Interop.JniTypeSignature ("test/me/GenericInterface", GenerateJavaPeer=false)]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T"})]
	public partial interface IGenericInterface : IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='GenericInterface']/method[@name='SetObject' and count(parameter)=1 and parameter[1][@type='T']]"
		[global::Java.Interop.JniMethodSignature ("SetObject", "(Ljava/lang/Object;)V")]
		void SetObject (global::Java.Lang.Object value);

	}

	[global::Java.Interop.JniTypeSignature ("test/me/GenericInterface", GenerateJavaPeer=false)]
	internal partial class IGenericInterfaceInvoker : global::Java.Lang.Object, IGenericInterface {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_test_me_GenericInterface; }
		}

		static readonly JniPeerMembers _members_test_me_GenericInterface = new JniPeerMembers ("test/me/GenericInterface", typeof (IGenericInterfaceInvoker));

		public IGenericInterfaceInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		public unsafe void SetObject (global::Java.Lang.Object value)
		{
			const string __id = "SetObject.(Ljava/lang/Object;)V";
			var native_value = (value?.PeerReference ?? default);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_value);
				_members_test_me_GenericInterface.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
			} finally {
				global::System.GC.KeepAlive (value);
			}
		}

	}
}
