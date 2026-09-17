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
		[Category ("TrimmableTypeMapUnsupported")]
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
	}
}
