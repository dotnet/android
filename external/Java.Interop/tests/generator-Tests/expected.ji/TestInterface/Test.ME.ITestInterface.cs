using System;
using System.Collections.Generic;
using Java.Interop;

namespace Test.ME {

	[Register ("test/me/TestInterface", DoNotGenerateAcw=true)]
	public abstract class TestInterface : Java.Lang.Object {
		internal TestInterface ()
		{
		}

		// Metadata.xml XPath field reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/field[@name='SPAN_COMPOSING']"
		public const int SpanComposing = (int) 256;


		// Metadata.xml XPath field reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/field[@name='DEFAULT_FOO']"
		public static global::Java.Lang.Object DefaultFoo {
			get {
				const string __id = "DEFAULT_FOO.Ljava/lang/Object;";

				var __v = _members.StaticFields.GetObjectValue (__id);
				return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::Java.Lang.Object> (ref __v.Handle, JniObjectReferenceOptions.CopyAndDispose);
			}
		}

		static readonly JniPeerMembers _members = new JniPeerMembers ("test/me/TestInterface", typeof (TestInterface));

	}

	[Register ("test/me/TestInterface", DoNotGenerateAcw=true)]
	[global::System.Obsolete ("Use the 'TestInterface' type. This type will be removed in a future release.", error: true)]
	public abstract class TestInterfaceConsts : TestInterface {
		private TestInterfaceConsts ()
		{
		}

	}

	// Metadata.xml XPath interface reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']"
	[global::Java.Interop.JniTypeSignature ("test/me/TestInterface", GenerateJavaPeer=false)]
	public partial interface ITestInterface : IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		int GetSpanFlags (global::Java.Lang.Object tag);

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='append' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		void Append (global::Java.Lang.ICharSequence value);

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='identity' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		global::Java.Lang.ICharSequence IdentityFormatted (global::Java.Lang.ICharSequence value);

	}

	public static partial class ITestInterfaceExtensions {
		public static void Append (this Test.ME.ITestInterface self, string value)
		{
			var jls_value = value == null ? null : new global::Java.Lang.String (value);
			self.Append (jls_value);
			jls_value?.Dispose ();
		}

		public static string Identity (this Test.ME.ITestInterface self, string value)
		{
			var jls_value = value == null ? null : new global::Java.Lang.String (value);
			global::Java.Lang.ICharSequence __result = self.IdentityFormatted (jls_value);
			var __rsval = __result?.ToString ();
			jls_value?.Dispose ();
			return __rsval;
		}

	}
}
