using System;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	public class JniRuntimeTest
	{
		[TestMethod]
		public void CreateJavaVM ()
		{
			Assert.AreSame (JniRuntime.CurrentRuntime, JniRuntime.CurrentRuntime);
			Assert.IsTrue (JniRuntime.CurrentRuntime.InvocationPointer != IntPtr.Zero);
			Assert.IsTrue (JniEnvironment.EnvironmentPointer != IntPtr.Zero);
		}

		[TestMethod]
		public void CreateJavaVMWithNullBuilder ()
		{
			Assert.ThrowsExactly<ArgumentNullException> (() => new JavaVMWithNullBuilder ());
		}

		class JavaVMWithNullBuilder : JniRuntime {
			public JavaVMWithNullBuilder ()
				: base ((JniRuntime.CreationOptions) null)
			{
			}
		}

		[TestMethod]
		public void GetRegisteredJavaVM_ExistingInstance ()
		{
			Assert.AreEqual (JniRuntime.CurrentRuntime, JniRuntime.GetRegisteredRuntime (JniRuntime.CurrentRuntime.InvocationPointer));
		}
	}
}
