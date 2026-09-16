using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class AssemblyInfoTests
	{
		[Test]
		public void AssemblyVersion ()
		{
			var version = typeof (JniRuntime).Assembly.GetName ().Version;
			Assert.IsTrue (version == new Version (11, 0, 0, 0), $"Expected Java.Interop version 11.0.0.0, but was {version}.");
		}
	}
}
