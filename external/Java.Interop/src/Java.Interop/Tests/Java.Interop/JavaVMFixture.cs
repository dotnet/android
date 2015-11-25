using System;
using System.Reflection;

using Java.Interop;

namespace Java.InteropTests {

	public abstract partial class JavaVMFixture {

		static JavaVMFixture ()
		{
			CreateJavaVM ();
		}

		static partial void CreateJavaVM ();

		// VM supports specifying a class to JNIEnv::CallNonvirtualVoidMethod()
		// that  isn't where the jmethodID came from.
		public  static  bool    CallNonvirtualVoidMethodSupportsDeclaringClassMismatch;

		public  static  readonly    bool    HaveSafeHandles = typeof (JniObjectReference).GetField ("gcHandle", BindingFlags.NonPublic | BindingFlags.Instance) != null;

		protected JavaVMFixture ()
		{
		}
	}
}

