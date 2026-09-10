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
			Assert.AreEqual (new Version (11, 0, 0, 0), typeof (JniRuntime).Assembly.GetName ().Version);
		}
	}
}
