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
}
