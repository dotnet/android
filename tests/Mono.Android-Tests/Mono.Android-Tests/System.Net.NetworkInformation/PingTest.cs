using System.Net.NetworkInformation;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.NetTests {

	[TestClass]
	public class PingTest
	{
		[TestMethod]
		public void PingLocalhost ()
		{
			using var ping = new Ping ();
			var reply = ping.Send ("127.0.0.1", 1000);
			Assert.AreEqual (IPStatus.Success, reply.Status, $"Ping to localhost failed with status: {reply.Status}");
		}
	}
}
