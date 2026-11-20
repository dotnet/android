using System.Diagnostics;
using System.Net.NetworkInformation;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.NetTests {

	[TestClass]
	public class PingTest
	{
		// Android apps cannot open raw ICMP sockets, so Ping.Send launches /system/bin/ping
		// and waits at most `timeout` for that process to start, ping and exit; a short timeout
		// returns TimedOut whenever process launch is slow (e.g. on overloaded emulators).
		// Loopback replies immediately, so this timeout only bounds the failure path.
		const int PingTimeoutMilliseconds = 30_000;

		[TestMethod]
		public void PingLocalhost ()
		{
			using var ping = new Ping ();
			var stopwatch = Stopwatch.StartNew ();
			var reply = ping.Send ("127.0.0.1", PingTimeoutMilliseconds);
			stopwatch.Stop ();
			Assert.AreEqual (IPStatus.Success, reply.Status, $"Ping to localhost failed with status: {reply.Status} after {stopwatch.ElapsedMilliseconds} ms");
		}
	}
}
