using NUnit.Framework;

namespace Java.NetTests {

	[TestFixture]
	public class InetAddressTest {

		[Test]
		[Category ("ThresholdDispatch")]
		public void Inet4AddressReturnsAddressBytes ()
		{
			byte [] expected = [127, 0, 0, 1];
			using var address = Java.Net.InetAddress.GetByAddress (expected);

			Assert.IsInstanceOf<Java.Net.Inet4Address> (address);
			CollectionAssert.AreEqual (expected, address.GetAddress ());
		}
	}
}
