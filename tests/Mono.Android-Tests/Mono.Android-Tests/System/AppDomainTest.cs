using System;
using System.Globalization;
using NUnit.Framework;

namespace SystemTests {

	[TestFixture]
	public class AppDomainTest {

		[Test]
		public void AppDomain_CreateDomain_Throws ()
		{
#pragma warning disable SYSLIB0024 // Verify that the unsupported AppDomain API throws.
			Assert.Throws<PlatformNotSupportedException> (() => AppDomain.CreateDomain ("other domain"));
#pragma warning restore SYSLIB0024
		}
	}
}
