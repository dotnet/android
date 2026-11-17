using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SystemTests {

	[TestClass]
	public class AppDomainTest {

		[TestMethod]
		public void AppDomain_CreateDomain_Throws ()
		{
#pragma warning disable SYSLIB0024 // Verify that the unsupported AppDomain API throws.
			Assert.ThrowsExactly<PlatformNotSupportedException> (() => AppDomain.CreateDomain ("other domain"));
#pragma warning restore SYSLIB0024
		}
	}
}
