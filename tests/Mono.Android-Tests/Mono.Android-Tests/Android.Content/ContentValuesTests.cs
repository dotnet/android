using NUnit.Framework;

using Android.Content;

namespace Android.ContentTests {

	[TestFixture]
	public class ContentValuesTests {

		[Test]
		public void PrimitiveValueRoundTrips ()
		{
			using var values = new ContentValues ();

			values.Put ("answer", 42);

			Assert.AreEqual (42, values.GetAsInteger ("answer"));
		}
	}
}
