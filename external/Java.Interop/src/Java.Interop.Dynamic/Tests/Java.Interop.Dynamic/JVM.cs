using System;
using System.IO;
using System.Diagnostics;
using System.Linq;

using Java.Interop;
using Java.InteropTests;

namespace Java.Interop.DynamicTests {

	class JVM {

		public static readonly JavaVM Current = new TestJVM ();
	}
}

