using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Java.Interop;

namespace Java.InteropTests {

	public abstract partial class JavaVMFixture {

		[UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "JavaVMFixture intentionally uses reflection-backed managers for non-AOT tests.")]
		[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "JavaVMFixture intentionally uses reflection-backed managers for non-trimming tests.")]
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
