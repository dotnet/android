using Java.Interop;

namespace Java.InteropTests {

	partial class JavaVMFixture {

		static partial void CreateJavaVM ()
		{
			var c = AndroidVM.Current;
			c.AddTypeMapping (TestType.JniTypeName, typeof (TestType));
		}
	}
}
