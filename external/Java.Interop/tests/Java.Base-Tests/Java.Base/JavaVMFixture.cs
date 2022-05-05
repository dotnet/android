using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;

using Java.Interop;

using Java.InteropTests;

namespace Java.BaseTests {

	partial class JavaVMFixture {

		static partial void CreateJavaVM ()
		{
			var c = new TestJVM (
					jars:           new[]{ "java.base-tests.jar" },
					typeMappings:   new Dictionary<string, Type> () {
						["example/MyRunnable"]      = typeof (MyRunnable),
						[JavaInvoker.JniTypeName]   = typeof (JavaInvoker),
					}
			);
			JniRuntime.SetCurrent (c);
		}
	}
}
