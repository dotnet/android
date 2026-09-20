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

	// Metadata.xml XPath interface reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']"
	[global::Java.Interop.JniTypeSignature ("test/me/TestInterface", GenerateJavaPeer=false, InvokerType=typeof (Test.ME.ITestInterfaceInvoker))]
	public partial interface ITestInterface : IJavaPeerable {
		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='getSpanFlags' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
		[global::Java.Interop.JniMethodSignature ("getSpanFlags", "(Ljava/lang/Object;)I")]
		int GetSpanFlags (global::Java.Lang.Object tag);

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='append' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		[global::Java.Interop.JniMethodSignature ("append", "(Ljava/lang/CharSequence;)V")]
		void Append (global::Java.Lang.ICharSequence value);

		// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/interface[@name='TestInterface']/method[@name='identity' and count(parameter)=1 and parameter[1][@type='java.lang.CharSequence']]"
		[global::Java.Interop.JniMethodSignature ("identity", "(Ljava/lang/CharSequence;)Ljava/lang/CharSequence;")]
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

	[global::Java.Interop.JniTypeSignature ("test/me/TestInterface", GenerateJavaPeer=false)]
	internal partial class ITestInterfaceInvoker : global::Java.Lang.Object, ITestInterface {
		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members_test_me_TestInterface; }
		}

		static readonly JniPeerMembers _members_test_me_TestInterface = new JniPeerMembers ("test/me/TestInterface", typeof (ITestInterfaceInvoker));

		public ITestInterfaceInvoker (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		public unsafe int GetSpanFlags (global::Java.Lang.Object tag)
		{
			const string __id = "getSpanFlags.(Ljava/lang/Object;)I";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (tag);
				var __rm = _members_test_me_TestInterface.InstanceMethods.InvokeAbstractInt32Method (__id, this, __args);
				return __rm;
			} finally {
				global::System.GC.KeepAlive (tag);
			}
		}

		public unsafe void Append (global::Java.Lang.ICharSequence value)
		{
			const string __id = "append.(Ljava/lang/CharSequence;)V";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (value);
				_members_test_me_TestInterface.InstanceMethods.InvokeAbstractVoidMethod (__id, this, __args);
			} finally {
				global::System.GC.KeepAlive (value);
			}
		}

		public unsafe global::Java.Lang.ICharSequence IdentityFormatted (global::Java.Lang.ICharSequence value)
		{
			const string __id = "identity.(Ljava/lang/CharSequence;)Ljava/lang/CharSequence;";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (value);
				var __rm = _members_test_me_TestInterface.InstanceMethods.InvokeAbstractObjectMethod (__id, this, __args);
				return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<Java.Lang.ICharSequence>(ref __rm, JniObjectReferenceOptions.CopyAndDispose);
			} finally {
				global::System.GC.KeepAlive (value);
			}
		}

	}
}
