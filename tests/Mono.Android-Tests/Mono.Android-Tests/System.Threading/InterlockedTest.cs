using System;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.ThreadingTests {

	[TestClass]
	public class InterlockedTest {

		[TestMethod]
		public void CAS64 ()
		{
			long i = 0, j = 42;

			j = Interlocked.CompareExchange (ref i, 42, 0);

			Assert.AreEqual (42, i);
			Assert.AreEqual (0, j);
		}
	}
}
