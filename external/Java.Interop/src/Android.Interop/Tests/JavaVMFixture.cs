using System;
using Java.Interop;

namespace Java.InteropTests {

	partial class JavaVMFixture {

		static partial void CreateJavaVM ()
		{
			var c = AndroidVM.Current;
			c.AddTypeMapping (TestType.JniTypeName, typeof (TestType));
			c.AddTypeMapping (GenericHolder<int>.JniTypeName,   typeof (GenericHolder<>));

			bool dalvik = (Java.Lang.JavaSystem.GetProperty ("java.vm.version") ?? "")
				.StartsWith ("1.", StringComparison.OrdinalIgnoreCase);
			CallNonvirtualVoidMethodSupportsDeclaringClassMismatch = dalvik;
		}
	}
}
