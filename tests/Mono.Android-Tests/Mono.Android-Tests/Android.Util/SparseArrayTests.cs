using NUnit.Framework;

using Android.Util;

namespace Android.UtilTests {

	[TestFixture]
	public class SparseArrayTests {

		[Test]
		public void GenericSparseArrayUsesSparseArrayClass ()
		{
			using var values = new SparseArray<string> ();

			values.Put (1, "one");

			Assert.AreEqual ("one", values.Get (1));
		}
	}
}
