using System.Diagnostics;
using System.Net.NetworkInformation;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.NetTests {

	[TestClass]
	public class PingTest
	{
		// Ping.Send requires a finite timeout. Android apps cannot open raw ICMP sockets, so it runs
		// `/system/bin/ping -c 1 -W <timeout in seconds>` and, after Process.Start() returns, waits up to
		// the timeout for the child to exit; that covers child startup, the loopback ICMP round trip,
		// exit and SIGCHLD-driven reaping. Ping returns TimedOut if that wait expires or ping exits 1/2.
		// A 1 s budget failed intermittently on CI (#13642) for an unproven reason. Loopback normally
		// replies within milliseconds, so this budget only bounds the failure path; real failures
		// (crashes, missing ping utility, lost exit notifications, no ICMP reply) still fail.
		const int TimeoutMilliseconds = 30_000;

		[TestMethod]
		public void PingLocalhost ()
		{
			using var ping = new Ping ();
			var stopwatch = Stopwatch.StartNew ();
			var reply = ping.Send ("127.0.0.1", TimeoutMilliseconds);
			stopwatch.Stop ();
			Assert.AreEqual (IPStatus.Success, reply.Status, $"Ping to localhost failed with status: {reply.Status} after {stopwatch.ElapsedMilliseconds} ms");
		}

		// Exercises the Process.Start()/WaitForExit() path Ping.Send uses on Android (#11530) without
		// ICMP, so a PingLocalhost failure can be attributed to process handling or to ping itself.
		[TestMethod]
		public void ProcessStartAndWaitForExit ()
		{
			var startInfo = new ProcessStartInfo ("/system/bin/sh", "-c \"echo ok\"") {
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			using var process = Process.Start (startInfo);
			Assert.IsNotNull (process);
			var stopwatch = Stopwatch.StartNew ();
			var exited = process.WaitForExit (TimeoutMilliseconds);
			stopwatch.Stop ();
			if (!exited) {
				process.Kill ();
			}
			Assert.IsTrue (exited, $"Process did not exit within {stopwatch.ElapsedMilliseconds} ms");
			Assert.AreEqual (0, process.ExitCode);
			Assert.AreEqual ("ok", process.StandardOutput.ReadToEnd ().Trim ());
		}
	}
}
