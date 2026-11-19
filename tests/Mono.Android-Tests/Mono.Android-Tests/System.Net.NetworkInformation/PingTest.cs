using System.Net.NetworkInformation;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.NetTests {

	[TestClass]
	public class PingTest
	{
		[TestMethod]
		[Ignore ("Flaky: https://github.com/dotnet/android/issues/13642")]
		public void PingLocalhost ()
		{
			using var ping = new Ping ();
			var reply = ping.Send ("127.0.0.1", 1000);
			Assert.AreEqual (IPStatus.Success, reply.Status, $"Ping to localhost failed with status: {reply.Status}");
		}
	}
}
