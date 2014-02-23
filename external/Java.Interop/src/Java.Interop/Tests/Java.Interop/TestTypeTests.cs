using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class TestTypeTests
	{
		[Test]
		public void TestCase ()
		{
			using (var t = new TestType ()) {
				t.RunTests ();
			}
		}
	}
}

