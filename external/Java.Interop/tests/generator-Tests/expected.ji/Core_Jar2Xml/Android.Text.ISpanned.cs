using System;
using System.Collections.Generic;
using Java.Interop;

namespace Android.Text {

	// Metadata.xml XPath interface reference: path="/api/package[@name='android.text']/interface[@name='Spanned']"
	[global::Java.Interop.JniTypeSignature ("android/text/Spanned", GenerateJavaPeer=false)]
	public partial interface ISpanned : IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='android.text']/interface[@name='Spanned']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[return:global::Android.Runtime.GeneratedEnum]
		global::Android.Text.SpanTypes GetSpanFlags (global::Java.Lang.Object tag);

	}
}
