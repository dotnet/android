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
			var runtime = JniRuntime.CurrentRuntime;
			Assert.AreSame (runtime, JniRuntime.CurrentRuntime);
			Assert.IsTrue (JniRuntime.CurrentRuntime.InvocationPointer != IntPtr.Zero);
			Assert.IsTrue (JniEnvironment.EnvironmentPointer != IntPtr.Zero);
		}

		[TestMethod]
		public void CreateJavaVMWithNullBuilder ()
		{
			// SAFETY: The base constructor rejects null options before accessing any native state.
			Assert.ThrowsExactly<ArgumentNullException> (() => unsafe (new JavaVMWithNullBuilder ()));
		}

		class JavaVMWithNullBuilder : JniRuntime {
			public unsafe JavaVMWithNullBuilder ()
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
