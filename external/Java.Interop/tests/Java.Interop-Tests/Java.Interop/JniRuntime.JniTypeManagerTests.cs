using System.Diagnostics.CodeAnalysis;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	public class JniRuntimeJniTypeManagerTests : JavaVMFixture {

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
