using System;

using Java.Interop;

namespace Java.InteropTests {

	static class JVM {
		public static readonly JavaVM Current = new JavaVMBuilder ().CreateJavaVM ();
	}
}

