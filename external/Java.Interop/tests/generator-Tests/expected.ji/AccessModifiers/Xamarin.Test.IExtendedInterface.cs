using System;
using System.Collections.Generic;
using Java.Interop;

namespace Xamarin.Test {

	// Metadata.xml XPath interface reference: path="/api/package[@name='xamarin.test']/interface[@name='ExtendedInterface']"
	[global::Java.Interop.JniTypeSignature ("xamarin/test/ExtendedInterface", GenerateJavaPeer=false)]
	public partial interface IExtendedInterface : IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/interface[@name='ExtendedInterface']/method[@name='extendedMethod' and count(parameter)=0]"
		[global::Java.Interop.JniMethodSignature ("extendedMethod", "()V")]
		void ExtendedMethod ();

		// Metadata.xml XPath method reference: path="/api/package[@name='xamarin.test']/interface[@name='BaseInterface']/method[@name='baseMethod' and count(parameter)=0]"
		[global::Java.Interop.JniMethodSignature ("baseMethod", "()V")]
		void BaseMethod ();

	}

	[global::Java.Interop.JniTypeSignature ("xamarin/test/ExtendedInterface", GenerateJavaPeer=false)]
	internal partial class IExtendedInterfaceInvoker : global::Java.Lang.Object, IExtendedInterface {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_xamarin_test_ExtendedInterface; }
		}

		static readonly JniPeerMembers _members_xamarin_test_BaseInterface = new JniPeerMembers ("xamarin/test/BaseInterface", typeof (IExtendedInterfaceInvoker));

		static readonly JniPeerMembers _members_xamarin_test_ExtendedInterface = new JniPeerMembers ("xamarin/test/ExtendedInterface", typeof (IExtendedInterfaceInvoker));

		public IExtendedInterfaceInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		public unsafe void ExtendedMethod ()
		{
			const string __id = "extendedMethod.()V";
			try {
				_members_xamarin_test_ExtendedInterface.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			} finally {
			}
		}

		public unsafe void BaseMethod ()
		{
			const string __id = "baseMethod.()V";
			try {
				_members_xamarin_test_BaseInterface.InstanceMethods.InvokeAbstractVoidMethod (__id, this, null);
			} finally {
			}
		}

	}
}
