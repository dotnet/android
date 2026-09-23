using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	public class JniRuntimeJniTypeManagerTests : JavaVMFixture {

		[Test]
		public void ReplacementMethodInfoToStringConvertsUtf8Pointers ()
		{
			var type = Marshal.StringToCoTaskMemUTF8 ("java/lang/Object");
			var name = Marshal.StringToCoTaskMemUTF8 ("toString");
			var signature = Marshal.StringToCoTaskMemUTF8 ("()Ljava/lang/String;");
			try {
				var info = new JniRuntime.ReplacementMethodInfo {
					TargetJniTypeUtf8            = type,
					TargetJniMethodNameUtf8      = name,
					TargetJniMethodSignatureUtf8 = signature,
				};

				var value = info.ToString ();

				Assert.That (value, Does.Contain ("TargetJniTypeUtf8 = \"java/lang/Object\""));
				Assert.That (value, Does.Contain ("TargetJniMethodNameUtf8 = \"toString\""));
				Assert.That (value, Does.Contain ("TargetJniMethodSignatureUtf8 = \"()Ljava/lang/String;\""));
			} finally {
				Marshal.ZeroFreeCoTaskMemUTF8 (type);
				Marshal.ZeroFreeCoTaskMemUTF8 (name);
				Marshal.ZeroFreeCoTaskMemUTF8 (signature);
			}
		}

		[Test]
		public void ReplacementTypeInfoSupportsStringAndUtf8Results ()
		{
			using var stringManager = new StringReplacementTypeManager ();
			stringManager.GetReplacementTypeInfo ("java/lang/String", out var stringReplacement, out var stringReplacementUtf8);
			Assert.AreEqual ("java/lang/Object", stringReplacement);
			Assert.AreEqual (IntPtr.Zero, stringReplacementUtf8);
			Assert.AreEqual (1, stringManager.LookupCount);

			var utf8Value = Marshal.StringToCoTaskMemUTF8 ("java/lang/Object");
			try {
				using var utf8Manager = new Utf8ReplacementTypeManager (utf8Value);
				utf8Manager.GetReplacementTypeInfo ("java/lang/Double", out var missingReplacement, out var missingReplacementUtf8);
				Assert.IsNull (missingReplacement);
				Assert.AreEqual (IntPtr.Zero, missingReplacementUtf8);
				utf8Manager.GetReplacementTypeInfo ("java/lang/String", out var replacement, out var replacementUtf8);
				Assert.IsNull (replacement);
				Assert.AreEqual (utf8Value, replacementUtf8);
				Assert.AreEqual ("java/lang/Object", utf8Manager.GetReplacementType ("java/lang/String"));
			} finally {
				Marshal.ZeroFreeCoTaskMemUTF8 (utf8Value);
			}
		}

#if !__ANDROID__
		[Test]
		[RequiresDynamicCode ("This test uses ReflectionJniTypeManager, which is reflection-based and not NativeAOT-compatible.")]
		[RequiresUnreferencedCode ("This test uses ReflectionJniTypeManager, which is reflection-based and not trimming-compatible.")]
		public void GetInvokerType ()
		{
			using (var vm  = new MyTypeManager ()) {
				// Concrete type; no invoker
				Assert.IsNull (vm.GetInvokerType (typeof (JavaObject)));

				// Not a bound abstract Java type; no invoker
				Assert.IsNull (vm.GetInvokerType (typeof (System.ICloneable)));

				// Bound abstract Java type; has an invoker
				Assert.AreSame (typeof (IJavaInterfaceInvoker), vm.GetInvokerType (typeof (IJavaInterface)));
			}
		}

		[RequiresDynamicCode ("MyTypeManager uses ReflectionJniTypeManager, which is reflection-based and not NativeAOT-compatible.")]
		[RequiresUnreferencedCode ("MyTypeManager uses ReflectionJniTypeManager, which is reflection-based and not trimming-compatible.")]
		class MyTypeManager : JniRuntime.ReflectionJniTypeManager {
			public MyTypeManager ()
			{
			}
		}
#endif  // !__ANDROID__

		class StringReplacementTypeManager : JniRuntime.JniTypeManager {

			public int LookupCount { get; private set; }

			protected override string GetReplacementTypeCore (string jniSimpleReference)
			{
				LookupCount++;
				return jniSimpleReference == "java/lang/String" ? "java/lang/Object" : null;
			}
		}

		class Utf8ReplacementTypeManager : JniRuntime.JniTypeManager {

			readonly IntPtr replacement;

			public Utf8ReplacementTypeManager (IntPtr replacement)
			{
				this.replacement = replacement;
			}

			protected override void GetReplacementTypeInfoCore (string jniSimpleReference, out string replacement, out IntPtr replacementUtf8)
			{
				replacement = jniSimpleReference == "java/lang/String" ? "ignored/string/value" : null;
				replacementUtf8 = jniSimpleReference == "java/lang/String" ? this.replacement : IntPtr.Zero;
			}
		}
	}
}
