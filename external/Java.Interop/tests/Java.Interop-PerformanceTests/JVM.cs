using System;
using System.IO;
using System.Diagnostics;
using System.Linq;

using Java.Interop;
using Java.InteropTests;

namespace Java.Interop.PerformanceTests {

	class JVM {

		public static readonly JavaVM Current = new TestJVM ("performance-test.jar");
	}
}

