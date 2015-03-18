using System;
using System.IO;
using System.Diagnostics;
using System.Linq;

using Java.Interop;

namespace Java.InteropTests {

	public abstract partial class JavaVMFixture {

		static JavaVMFixture ()
		{
			var c = new TestJVM (
				jars:           new[]{ "performance-test.jar" }
			);
			JavaVM.SetCurrent (c);
		}

		protected JavaVMFixture ()
		{
		}
	}
}

