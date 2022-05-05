using System;
using System.Reflection;

using Java.Interop;

namespace Java.BaseTests {

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