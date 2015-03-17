using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;

using Java.Interop;

namespace Java.InteropTests {

	partial class JavaVMFixture {

		static partial void CreateJavaVM ()
		{
			var c = new TestJVM (
					jars:           new[]{ "interop-test.jar" },
					typeMappings:   new Dictionary<string, Type> () {
						{ TestType.JniTypeName, typeof (TestType) },
					}
			);
			JavaVM.SetCurrent (c);
		}

	}
}

