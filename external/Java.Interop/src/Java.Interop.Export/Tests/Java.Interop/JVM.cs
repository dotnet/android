using System;
using System.IO;
using System.Diagnostics;
using System.Linq;

using Java.Interop;

namespace Java.InteropTests {

	class JVM {

		public static readonly JavaVM Current = new TestJVM ("export-test.jar");
	}
}

