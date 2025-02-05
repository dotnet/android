using System;
using System.Globalization;
using NUnit.Framework;

namespace SystemTests {

	[TestFixture]
	public class AppDomainTest {

		[Test]
		public void AppDomain_CreateDomain_Throws ()
		{
			Assert.Throws<PlatformNotSupportedException> (() => AppDomain.CreateDomain ("other domain"));
		}
	}
}
