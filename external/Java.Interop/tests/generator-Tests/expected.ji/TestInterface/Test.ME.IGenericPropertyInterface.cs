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

namespace Test.ME {

	// Metadata.xml XPath interface reference: path="/api/package[@name='test.me']/interface[@name='GenericPropertyInterface']"
	[global::Java.Interop.JniTypeSignature ("test/me/GenericPropertyInterface", GenerateJavaPeer=false, InvokerType=typeof (Test.ME.IGenericPropertyInterfaceInvoker))]
	[global::Java.Interop.JavaTypeParameters (new string [] {"T"})]
	public partial interface IGenericPropertyInterface : IJavaPeerable {
		global::Java.Lang.Object Object {
			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='GenericPropertyInterface']/method[@name='getObject' and count(parameter)=0]"
			[global::Java.Interop.JniMethodSignature ("getObject", "()Ljava/lang/Object;")]
			get; 

			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='GenericPropertyInterface']/method[@name='setObject' and count(parameter)=1 and parameter[1][@type='T']]"
			[global::Java.Interop.JniMethodSignature ("setObject", "(Ljava/lang/Object;)V")]
			set; 
		}

	}

	[global::Java.Interop.JniTypeSignature ("test/me/GenericPropertyInterface", GenerateJavaPeer=false)]
	internal partial class IGenericPropertyInterfaceInvoker : global::Java.Lang.Object, IGenericPropertyInterface {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_test_me_GenericPropertyInterface; }
		}

		static readonly JniPeerMembers _members_test_me_GenericPropertyInterface = new JniPeerMembers ("test/me/GenericPropertyInterface", typeof (IGenericPropertyInterfaceInvoker));

		public IGenericPropertyInterfaceInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		public unsafe global::Java.Lang.Object Object {
			get {
				const string __id = "getObject.()Ljava/lang/Object;";
				try {
					var __rm = _members_test_me_GenericPropertyInterface.InstanceMethods.InvokeAbstractObjectMethod (__id, this, null);
					return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::Java.Lang.Object>(ref __rm, JniObjectReferenceOptions.CopyAndDispose);
				} finally {
				}
			}
			set {
				const string __id = "setObject.(Ljava/lang/Object;)V";
				var native_value = (value?.PeerReference ?? default);
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (native_value);
					_members_test_me_GenericPropertyInterface.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
				} finally {
					global::System.GC.KeepAlive (value);
				}
			}
		}

	}
}
