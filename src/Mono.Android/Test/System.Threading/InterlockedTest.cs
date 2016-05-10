using System;
using System.Threading;

using NUnit.Framework;

namespace System.ThreadingTests {

	[TestFixture]
	public class InterlockedTest {

		[Test]
		public void CAS64 ()
		{
			long i = 0, j = 42;

			j = Interlocked.CompareExchange (ref i, 42, 0);

			Assert.AreEqual (i, 42);
			Assert.AreEqual (j, 0);
		}
	}
}
