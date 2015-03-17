using System;

namespace Java.InteropTests {

	public abstract partial class JavaVMFixture {

		static JavaVMFixture ()
		{
			CreateJavaVM ();
		}

		static partial void CreateJavaVM ();

		protected JavaVMFixture ()
		{
		}
	}
}

