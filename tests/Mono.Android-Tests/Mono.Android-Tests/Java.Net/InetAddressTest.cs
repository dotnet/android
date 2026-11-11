using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.NetTests {

	[TestClass]
	public class InetAddressTest {

		[TestMethod]
		[TestCategory ("ThresholdDispatch")]
		public void Inet4AddressReturnsAddressBytes ()
		{
			byte [] expected = [127, 0, 0, 1];
			using var address = Java.Net.InetAddress.GetByAddress (expected);

			Assert.IsInstanceOfType<Java.Net.Inet4Address> (address);
			CollectionAssert.AreEqual (expected, address.GetAddress ());
		}
	}
}
