using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JniRuntimeTest : JavaVMFixture
	{
		[Test]
		public void CreateJavaVM ()
		{
			Assert.AreSame (JniRuntime.CurrentRuntime, JniRuntime.CurrentRuntime);
			Assert.IsTrue (JniRuntime.CurrentRuntime.InvocationPointer != IntPtr.Zero);
			Assert.IsTrue (JniEnvironment.EnvironmentPointer != IntPtr.Zero);
		}

		[Test]
		public void CreateJavaVMWithNullBuilder ()
		{
			Assert.Throws<ArgumentNullException> (() => new JavaVMWithNullBuilder ());
		}

		class JavaVMWithNullBuilder : JniRuntime {
			public JavaVMWithNullBuilder ()
				: base ((JniRuntime.CreationOptions) null)
			{
			}
		}

		[Test]
		public void GetRegisteredJavaVM_ExistingInstance ()
		{
			Assert.AreEqual (JniRuntime.CurrentRuntime, JniRuntime.GetRegisteredRuntime (JniRuntime.CurrentRuntime.InvocationPointer));
		}
	}
}
