using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Mono.AndroidTools;
using NUnit.Framework;
using Xamarin.AndroidTools.Debugging;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
public class JavaDebuggingLogcatCaptureTests
{
	const string Serial = "emulator-5554";
	const long WindowStart = 1700000000;
	const int Pid = 4321;

	static readonly string [] Canaries = { "com.secret.app", "4321", "5554", "1700000000", "secret-message" };

	[Test]
	public async Task BoundedShellCommandReadsLinesUntilEnd ()
	{
		using var server = new FakeAdbServer ((command, stream) => ReplyAsync (stream, "first\r\nsecond\nthird"));
		var lines = new List<string> ();

		var result = await server.Device.RunBoundedShellCommandAsync ("echo", 1024, 16, TimeSpan.FromSeconds (10), lines.Add, CancellationToken.None);

		Assert.That (result.Status, Is.EqualTo (BoundedShellCommandStatus.Completed));
		CollectionAssert.AreEqual (new [] { "first", "second", "third" }, lines);
		Assert.That (result.Lines, Is.EqualTo (3));
		Assert.That (result.Bytes, Is.EqualTo ("first\r\nsecond\nthird".Length));
		CollectionAssert.AreEqual (new [] { "host:transport:" + Serial, "shell:echo" }, await server.GetCommandsAsync ());
	}

	[Test]
	public async Task BoundedShellCommandStopsAtByteLimitForUnterminatedLine ()
	{
		using var server = new FakeAdbServer ((command, stream) => ReplyAsync (stream, new string ('x', 70 * 1024)));
		var lines = new List<string> ();

		var result = await server.Device.RunBoundedShellCommandAsync ("cat", 64 * 1024, 256, TimeSpan.FromSeconds (10), lines.Add, CancellationToken.None);

		Assert.That (result.Status, Is.EqualTo (BoundedShellCommandStatus.ByteLimit));
		Assert.That (result.Bytes, Is.EqualTo (64 * 1024));
		Assert.That (result.Lines, Is.EqualTo (0));
		Assert.That (lines, Is.Empty);
		Assert.That (await server.WaitForCloseAsync (), Is.True);
	}

	[Test]
	public async Task BoundedShellCommandStopsAtLineLimit ()
	{
		var output = string.Concat (Enumerable.Range (0, 300).Select (i => $"line {i}\n"));
		using var server = new FakeAdbServer ((command, stream) => ReplyAsync (stream, output));
		var lines = new List<string> ();

		var result = await server.Device.RunBoundedShellCommandAsync ("cat", 64 * 1024, 256, TimeSpan.FromSeconds (10), lines.Add, CancellationToken.None);

		Assert.That (result.Status, Is.EqualTo (BoundedShellCommandStatus.LineLimit));
		Assert.That (result.Lines, Is.EqualTo (256));
		Assert.That (lines.Last (), Is.EqualTo ("line 255"));
	}

	[Test]
	public async Task BoundedShellCommandTimesOutBlockedReadAndClosesConnection ()
	{
		using var server = new FakeAdbServer (async (command, stream) => {
			await WriteAsync (stream, "OKAY");
			await FakeAdbServer.ExpectPeerCloseAsync (stream);
		});
		var stopwatch = Stopwatch.StartNew ();

		var result = await server.Device.RunBoundedShellCommandAsync ("logcat", 1024, 16, TimeSpan.FromMilliseconds (300), _ => { }, CancellationToken.None);

		Assert.That (result.Status, Is.EqualTo (BoundedShellCommandStatus.Timeout));
		Assert.That (stopwatch.Elapsed, Is.LessThan (TimeSpan.FromSeconds (3)));
		Assert.That (await server.WaitForCloseAsync (), Is.True);
	}

	[Test]
	public async Task BoundedShellCommandDeadlineCoversTransportSetup ()
	{
		using var server = new FakeAdbServer ((command, stream) => Task.CompletedTask, silentTransport: true);
		var stopwatch = Stopwatch.StartNew ();

		var result = await server.Device.RunBoundedShellCommandAsync ("logcat", 1024, 16, TimeSpan.FromMilliseconds (300), _ => { }, CancellationToken.None);

		Assert.That (result.Status, Is.EqualTo (BoundedShellCommandStatus.Timeout));
		Assert.That (stopwatch.Elapsed, Is.LessThan (TimeSpan.FromSeconds (3)));
		Assert.That (await server.WaitForCloseAsync (), Is.True);
	}

	[Test]
	public async Task BoundedShellCommandPropagatesCallerCancellationAndClosesConnection ()
	{
		using var server = new FakeAdbServer (async (command, stream) => {
			await WriteAsync (stream, "OKAY");
			await FakeAdbServer.ExpectPeerCloseAsync (stream);
		});
		using var cancellation = new CancellationTokenSource (TimeSpan.FromMilliseconds (300));
		var stopwatch = Stopwatch.StartNew ();

		var thrown = Assert.CatchAsync<OperationCanceledException> (() =>
			server.Device.RunBoundedShellCommandAsync ("logcat", 1024, 16, TimeSpan.FromSeconds (30), _ => { }, cancellation.Token));

		Assert.That (thrown.CancellationToken, Is.EqualTo (cancellation.Token));
		Assert.That (stopwatch.Elapsed, Is.LessThan (TimeSpan.FromSeconds (5)));
		Assert.That (await server.WaitForCloseAsync (), Is.True);
	}

	[Test]
	public async Task BoundedShellCommandPropagatesCancellationFromFinalLine ()
	{
		using var server = new FakeAdbServer ((command, stream) => ReplyAsync (stream, "lastline"), halfCloseOutput: true);
		using var cancellation = new CancellationTokenSource ();
		var lines = new List<string> ();

		var thrown = Assert.CatchAsync<OperationCanceledException> (() =>
			server.Device.RunBoundedShellCommandAsync ("echo", 1024, 16, TimeSpan.FromSeconds (10), line => {
				lines.Add (line);
				cancellation.Cancel ();
			}, cancellation.Token));

		Assert.That (thrown.CancellationToken, Is.EqualTo (cancellation.Token));
		CollectionAssert.AreEqual (new [] { "lastline" }, lines);
		Assert.That (await server.WaitForCloseAsync (), Is.True);
	}

	[Test]
	public async Task BoundedShellCommandReportsDeadlineAfterFinalLineCallbackReturnsNormally ()
	{
		using var server = new FakeAdbServer ((command, stream) => ReplyAsync (stream, "lastline"), halfCloseOutput: true);
		var lines = new List<string> ();
		bool peerClosed = false;

		var result = await server.Device.RunBoundedShellCommandAsync ("echo", 1024, 16, TimeSpan.FromSeconds (5), line => {
			lines.Add (line);
			// EOF has arrived, but keep the callback active until the deadline closes the owned client.
			peerClosed = server.WaitForCloseAsync ().GetAwaiter ().GetResult ();
		}, CancellationToken.None);

		Assert.That (result.Status, Is.EqualTo (BoundedShellCommandStatus.Timeout));
		CollectionAssert.AreEqual (new [] { "lastline" }, lines);
		Assert.That (peerClosed, Is.True);
	}

	[Test]
	public void BoundedShellCommandDoesNotConnectWhenAlreadyCancelled ()
	{
		using var server = new FakeAdbServer ((command, stream) => ReplyAsync (stream, "unused"));

		Assert.CatchAsync<OperationCanceledException> (() =>
			server.Device.RunBoundedShellCommandAsync ("echo", 1024, 16, TimeSpan.FromSeconds (10), _ => { }, new CancellationToken (true)));

		Assert.That (server.Connections, Is.EqualTo (0));
	}

	[Test]
	public async Task BoundedShellCommandReportsCommandAndCallbackFailures ()
	{
		using (var server = new FakeAdbServer ((command, stream) => WriteAsync (stream, "FAIL0006closed"))) {
			var result = await server.Device.RunBoundedShellCommandAsync ("echo", 1024, 16, TimeSpan.FromSeconds (10), _ => { }, CancellationToken.None);
			Assert.That (result.Status, Is.EqualTo (BoundedShellCommandStatus.ReadFailure));
		}
		using (var server = new FakeAdbServer ((command, stream) => ReplyAsync (stream, "line\n"))) {
			var result = await server.Device.RunBoundedShellCommandAsync ("echo", 1024, 16, TimeSpan.FromSeconds (10), _ => throw new InvalidOperationException ("callback"), CancellationToken.None);
			Assert.That (result.Status, Is.EqualTo (BoundedShellCommandStatus.ReadFailure));
			Assert.That (await server.WaitForCloseAsync (), Is.True);
		}
	}

	[Test]
	public async Task CaptureIsNotStartedWithoutOptInJavaDebuggingAndLogWriter ()
	{
		using var server = new FakeAdbServer ((command, stream) => ReplyAsync (stream, WindowStart + "\n"));
		var messages = new List<string> ();

		var defaultConfiguration = new ExecutionConfiguration ("com.secret.app", null) { LogWiter = messages.Add };
		Assert.That (defaultConfiguration.CaptureJavaDebuggingLogcat, Is.False);
		Assert.That (await JavaDebuggingLogcatCapture.StartAsync (server.Device, defaultConfiguration, true, CancellationToken.None), Is.Null);

		var optedIn = new ExecutionConfiguration ("com.secret.app", null) { CaptureJavaDebuggingLogcat = true, LogWiter = messages.Add };
		Assert.That (await JavaDebuggingLogcatCapture.StartAsync (server.Device, optedIn, false, CancellationToken.None), Is.Null);

		var noLogWriter = new ExecutionConfiguration ("com.secret.app", null) { CaptureJavaDebuggingLogcat = true };
		Assert.That (await JavaDebuggingLogcatCapture.StartAsync (server.Device, noLogWriter, true, CancellationToken.None), Is.Null);

		Assert.That (server.Connections, Is.EqualTo (0));
		Assert.That (messages, Is.Empty);
	}

	[Test]
	public async Task CaptureLogsOnlyFixedMarkersRelativeToGuestWindow ()
	{
		var logcat = string.Join ("\n", new [] {
			"--------- beginning of system",
			$"{WindowStart - 1}.900  {Pid}  {Pid} I System.out: Sending WAIT chunk",
			$"{WindowStart,19}.250  {Pid}  {Pid} W ActivityThread: Application com.secret.app is waiting for the debugger ...",
			$"{WindowStart}.260  {Pid}  {Pid} I System.out: Sending WAIT chunk",
			$"{WindowStart}.270  9999  9999 I System.out: Debugger has connected",
			$"{WindowStart + 1}.300  {Pid}  {Pid} I System.out: Debugger has connected",
			$"{WindowStart + 1}.310  {Pid}  {Pid} I System.out: waiting for debugger to settle...",
			$"{WindowStart + 2}.700  {Pid}  {Pid} I System.out: debugger has settled (1390)",
			$"{WindowStart + 2}.800  {Pid}  {Pid} I System.out: secret-message",
			"",
		});
		using var server = new FakeAdbServer ((command, stream) => ReplyAsync (stream, command.StartsWith ("shell:date") ? WindowStart + "\n" : logcat));
		var messages = new List<string> ();
		var configuration = new ExecutionConfiguration ("com.secret.app", null) { CaptureJavaDebuggingLogcat = true, LogWiter = messages.Add };

		var capture = await JavaDebuggingLogcatCapture.StartAsync (server.Device, configuration, true, CancellationToken.None);
		Assert.That (capture, Is.Not.Null);
		await capture.CaptureAsync (Pid, CancellationToken.None);

		var commands = await server.GetCommandsAsync ();
		CollectionAssert.AreEqual (new [] {
			"host:transport:" + Serial, "shell:date +%s",
			"host:transport:" + Serial, $"shell:logcat -d -v threadtime -v epoch -t {WindowStart}.000 --pid={Pid} -b main,system -s ActivityThread:W System.out:I",
		}, commands);
		Assert.That (messages [0], Does.Match (@"^JDWP logcat-window result=completed host-utc-begin=\S+\+00:00 host-utc-end=\S+\+00:00 duration-ms=\d+ resolution=whole-second$"));
		CollectionAssert.AreEqual (new [] {
			"JDWP logcat-marker order=0 kind=app-waiting guest-ms=250",
			"JDWP logcat-marker order=1 kind=wait-chunk guest-ms=260",
			"JDWP logcat-marker order=2 kind=debugger-connected guest-ms=1300",
			"JDWP logcat-marker order=3 kind=debugger-settling guest-ms=1310",
			"JDWP logcat-marker order=4 kind=debugger-settled guest-ms=2700",
		}, messages.Skip (1).Take (5));
		Assert.That (messages.Last (), Does.Match (@"^JDWP logcat-snapshot result=completed host-utc-begin=\S+\+00:00 host-utc-end=\S+\+00:00 duration-ms=\d+" +
			@" bytes=\d+ lines=9 parsed=6 dividers=1 malformed=0 pid-mismatch=1 before-window=1 other=1 markers=5 marker-cap=false first-guest-ms=250 last-guest-ms=2800 limits=\S+$"));
		Assert.That (messages.Count, Is.EqualTo (7));
		foreach (var canary in Canaries)
			Assert.That (messages.Any (message => message.Contains (canary)), Is.False, canary);
	}

	[Test]
	public async Task CaptureRejectsActivityThreadLookalikes ()
	{
		var logcat = string.Join ("\n", new [] {
			$"{WindowStart}.100 {Pid} {Pid} W ActivityThread: Application com.secret.app is waiting for the debugger ...",
			$"{WindowStart}.200 {Pid} {Pid} W ActivityThread: Application com.secret.app is suspending. Debugger needs to resume to continue.",
			$"{WindowStart}.300 {Pid} {Pid} W ActivityThread: Application com.secret.app is waiting for the debugger on port 8100...",
			$"{WindowStart}.400 {Pid} {Pid} W ActivityThread: Application com.secret.app is waiting for the debugger ... secret-message",
			$"{WindowStart}.500 {Pid} {Pid} W ActivityThread: Application com.secret.app extra is waiting for the debugger ...",
			$"{WindowStart}.600 {Pid} {Pid} W ActivityThread: Application  is waiting for the debugger ...",
			$"{WindowStart}.700 {Pid} {Pid} W ActivityThread: Application com.secret.app extra is suspending. Debugger needs to resume to continue.",
		});
		using var server = new FakeAdbServer ((command, stream) => ReplyAsync (stream, command.StartsWith ("shell:date") ? WindowStart + "\n" : logcat));
		var messages = new List<string> ();
		var configuration = new ExecutionConfiguration ("com.secret.app", null) { CaptureJavaDebuggingLogcat = true, LogWiter = messages.Add };
		var capture = await JavaDebuggingLogcatCapture.StartAsync (server.Device, configuration, true, CancellationToken.None);
		Assert.That (capture, Is.Not.Null);

		await capture.CaptureAsync (Pid, CancellationToken.None);

		CollectionAssert.AreEqual (new [] {
			"JDWP logcat-marker order=0 kind=app-waiting guest-ms=100",
			"JDWP logcat-marker order=1 kind=app-suspending guest-ms=200",
		}, messages.Where (message => message.StartsWith ("JDWP logcat-marker ")));
		Assert.That (messages.Last (), Does.Contain ("other=5 markers=2 "));
		foreach (var canary in Canaries)
			Assert.That (messages.Any (message => message.Contains (canary)), Is.False, canary);
	}

	[Test]
	public async Task CaptureReportsUnexpectedClockFailureWithoutExceptionMessage ()
	{
		using var server = new FakeAdbServer ((command, stream) => ReplyAsync (stream, WindowStart + "\n"));
		var messages = new List<string> ();
		var configuration = new ExecutionConfiguration ("com.secret.app", null) {
			CaptureJavaDebuggingLogcat = true,
			LogWiter = message => {
				if (message.StartsWith ("JDWP logcat-window ", StringComparison.Ordinal))
					throw new IOException ("secret-message com.secret.app");
				messages.Add (message);
			},
		};

		Assert.That (await JavaDebuggingLogcatCapture.StartAsync (server.Device, configuration, true, CancellationToken.None), Is.Null);

		CollectionAssert.AreEqual (new [] { "JDWP logcat-failed phase=clock exception-type=IOException" }, messages);
	}

	[Test]
	public async Task CaptureClockFailureReceiptToleratesBrokenLogger ()
	{
		using var server = new FakeAdbServer ((command, stream) => ReplyAsync (stream, WindowStart + "\n"));
		var configuration = new ExecutionConfiguration ("com.secret.app", null) {
			CaptureJavaDebuggingLogcat = true,
			LogWiter = _ => throw new IOException ("secret-message"),
		};

		Assert.That (await JavaDebuggingLogcatCapture.StartAsync (server.Device, configuration, true, CancellationToken.None), Is.Null);
	}

	[Test]
	public async Task CaptureReportsMalformedOutputAndMarkerCap ()
	{
		var lines = Enumerable.Range (0, 40).Select (i => $"{WindowStart}.{i:000}  {Pid}  {Pid} I System.out: Sending WAIT chunk").ToList ();
		lines.Add ("logcat: secret-message");
		using var server = new FakeAdbServer ((command, stream) => ReplyAsync (stream, command.StartsWith ("shell:date") ? WindowStart + "\n" : string.Join ("\n", lines)));
		var messages = new List<string> ();
		var configuration = new ExecutionConfiguration ("com.secret.app", null) { CaptureJavaDebuggingLogcat = true, LogWiter = messages.Add };

		var capture = await JavaDebuggingLogcatCapture.StartAsync (server.Device, configuration, true, CancellationToken.None);
		Assert.That (capture, Is.Not.Null);
		await capture.CaptureAsync (Pid, CancellationToken.None);

		Assert.That (messages.Count (message => message.StartsWith ("JDWP logcat-marker ")), Is.EqualTo (JavaDebuggingLogcatCapture.MaxMarkers));
		Assert.That (messages.Last (), Does.Match (@"^JDWP logcat-snapshot result=malformed .* malformed=1 .* markers=40 marker-cap=true "));
		Assert.That (messages.Any (message => message.Contains ("secret-message")), Is.False);
	}

	[Test]
	public async Task CaptureReportsSnapshotTimeoutWithoutThrowing ()
	{
		using var server = new FakeAdbServer (async (command, stream) => {
			if (command.StartsWith ("shell:date")) {
				await ReplyAsync (stream, WindowStart + "\n");
				return;
			}
			await WriteAsync (stream, "OKAY");
			await FakeAdbServer.ExpectPeerCloseAsync (stream);
		});
		var messages = new List<string> ();
		var configuration = new ExecutionConfiguration ("com.secret.app", null) { CaptureJavaDebuggingLogcat = true, LogWiter = messages.Add };
		var capture = await JavaDebuggingLogcatCapture.StartAsync (server.Device, configuration, true, CancellationToken.None);
		Assert.That (capture, Is.Not.Null);
		var stopwatch = Stopwatch.StartNew ();

		await capture.CaptureAsync (Pid, CancellationToken.None);

		Assert.That (stopwatch.Elapsed, Is.LessThan (JavaDebuggingLogcatCapture.SnapshotTimeout + TimeSpan.FromSeconds (2)));
		Assert.That (messages.Last (), Does.Match (@"^JDWP logcat-snapshot result=timeout .* bytes=0 lines=0 .* markers=0 marker-cap=false first-guest-ms=none last-guest-ms=none "));
		Assert.That (await server.WaitForCloseAsync (), Is.True);
	}

	[TestCase ("not-a-clock\n", "malformed")]
	[TestCase ("1700000000\n1700000001\n", "malformed")]
	[TestCase ("1700000000.5\n", "malformed")]
	[TestCase (null, "read-failure")]
	public async Task CaptureIsSkippedWhenGuestClockIsUnavailable (string clockOutput, string expected)
	{
		using var server = new FakeAdbServer ((command, stream) => clockOutput == null ? WriteAsync (stream, "FAIL0004nope") : ReplyAsync (stream, clockOutput));
		var messages = new List<string> ();
		var configuration = new ExecutionConfiguration ("com.secret.app", null) { CaptureJavaDebuggingLogcat = true, LogWiter = messages.Add };

		var capture = await JavaDebuggingLogcatCapture.StartAsync (server.Device, configuration, true, CancellationToken.None);

		Assert.That (capture, Is.Null);
		Assert.That (messages.Single (), Does.StartWith ($"JDWP logcat-window result={expected} "));
		Assert.That (messages.Single (), Does.Not.Contain ("1700000000"));
	}

	[Test]
	public async Task CaptureClockReadIsBoundedByOneSecond ()
	{
		using var server = new FakeAdbServer ((command, stream) => Task.CompletedTask, silentTransport: true);
		var messages = new List<string> ();
		var configuration = new ExecutionConfiguration ("com.secret.app", null) { CaptureJavaDebuggingLogcat = true, LogWiter = messages.Add };
		var stopwatch = Stopwatch.StartNew ();

		var capture = await JavaDebuggingLogcatCapture.StartAsync (server.Device, configuration, true, CancellationToken.None);

		Assert.That (capture, Is.Null);
		Assert.That (stopwatch.Elapsed, Is.LessThan (JavaDebuggingLogcatCapture.ClockTimeout + TimeSpan.FromSeconds (2)));
		Assert.That (messages.Single (), Does.StartWith ("JDWP logcat-window result=timeout "));
		Assert.That (await server.WaitForCloseAsync (), Is.True);
	}

	[Test]
	public async Task CaptureClockReadPropagatesBuildCancellation ()
	{
		using var server = new FakeAdbServer (async (command, stream) => {
			await WriteAsync (stream, "OKAY");
			await FakeAdbServer.ExpectPeerCloseAsync (stream);
		});
		var configuration = new ExecutionConfiguration ("com.secret.app", null) { CaptureJavaDebuggingLogcat = true, LogWiter = _ => { } };
		using var cancellation = new CancellationTokenSource (TimeSpan.FromMilliseconds (200));

		Assert.CatchAsync<OperationCanceledException> (() => JavaDebuggingLogcatCapture.StartAsync (server.Device, configuration, true, cancellation.Token));
		Assert.That (await server.WaitForCloseAsync (), Is.True);
	}

	[Test]
	public async Task CaptureWithoutPidDoesNotQueryDevice ()
	{
		using var server = new FakeAdbServer ((command, stream) => ReplyAsync (stream, WindowStart + "\n"));
		var messages = new List<string> ();
		var configuration = new ExecutionConfiguration ("com.secret.app", null) { CaptureJavaDebuggingLogcat = true, LogWiter = messages.Add };
		var capture = await JavaDebuggingLogcatCapture.StartAsync (server.Device, configuration, true, CancellationToken.None);
		Assert.That (capture, Is.Not.Null);

		await capture.CaptureAsync (0, CancellationToken.None);

		Assert.That (server.Connections, Is.EqualTo (1));
		Assert.That (messages.Last (), Is.EqualTo ("JDWP logcat-snapshot result=skipped reason=no-pid"));
	}

	static Task ReplyAsync (Stream stream, string output) => WriteAsync (stream, "OKAY" + output);

	static async Task WriteAsync (Stream stream, string text)
	{
		var bytes = Encoding.UTF8.GetBytes (text);
		await stream.WriteAsync (bytes, 0, bytes.Length);
		await stream.FlushAsync ();
	}

	// A minimal ADB host-protocol server on loopback: it acknowledges `host:transport:<serial>` and
	// passes the following command to the handler, which writes the status and output.
	sealed class FakeAdbServer : IDisposable
	{
		readonly TcpListener listener;
		readonly Func<string, NetworkStream, Task> handler;
		readonly bool silentTransport;
		readonly bool halfCloseOutput;
		readonly List<string> commands = new List<string> ();
		readonly List<Task<bool>> connections = new List<Task<bool>> ();
		readonly Task acceptLoop;

		public FakeAdbServer (Func<string, NetworkStream, Task> handler, bool silentTransport = false, bool halfCloseOutput = false)
		{
			this.handler = handler;
			this.silentTransport = silentTransport;
			this.halfCloseOutput = halfCloseOutput;
			listener = new TcpListener (IPAddress.Loopback, 0);
			listener.Start ();
			Device = new AndroidDevice (Serial, "device", new AdbServer (IPAddress.Loopback, ((IPEndPoint) listener.LocalEndpoint).Port));
			acceptLoop = AcceptAsync ();
		}

		public AndroidDevice Device { get; }

		public int Connections {
			get {
				lock (connections)
					return connections.Count;
			}
		}

		public async Task<string []> GetCommandsAsync ()
		{
			Task<bool> [] pending;
			lock (connections)
				pending = connections.ToArray ();
			await Task.WhenAll (pending);
			lock (commands)
				return commands.ToArray ();
		}

		// True when every connection handler observed the client closing (or completed) within the limit.
		public async Task<bool> WaitForCloseAsync ()
		{
			Task<bool> [] pending;
			lock (connections)
				pending = connections.ToArray ();
			var all = Task.WhenAll (pending);
			if (await Task.WhenAny (all, Task.Delay (TimeSpan.FromSeconds (10))) != all)
				return false;
			return pending.Length > 0 && pending.All (t => t.Result);
		}

		async Task AcceptAsync ()
		{
			while (true) {
				TcpClient client;
				try {
					client = await listener.AcceptTcpClientAsync ();
				} catch (Exception) {
					return;
				}
				lock (connections)
					connections.Add (HandleAsync (client));
			}
		}

		async Task<bool> HandleAsync (TcpClient client)
		{
			using (client)
			using (var stream = client.GetStream ()) {
				try {
					await ReadCommandAsync (stream);
					if (silentTransport)
						return await WaitForPeerCloseAsync (stream);
					await WriteAsync (stream, "OKAY");
					var command = await ReadCommandAsync (stream);
					await handler (command, stream);
					if (halfCloseOutput) {
						client.Client.Shutdown (SocketShutdown.Send);
						return await WaitForPeerCloseAsync (stream);
					}
				} catch (TimeoutException) {
					return false;
				} catch (Exception) {
					// The client may close early, for example after a limit.
				}
				return true;
			}
		}

		public static async Task ExpectPeerCloseAsync (Stream stream)
		{
			if (!await WaitForPeerCloseAsync (stream))
				throw new TimeoutException ("The client did not close the connection.");
		}

		async Task<string> ReadCommandAsync (NetworkStream stream)
		{
			var length = int.Parse (Encoding.ASCII.GetString (await ReadExactAsync (stream, 4)), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			var command = Encoding.UTF8.GetString (await ReadExactAsync (stream, length));
			lock (commands)
				commands.Add (command);
			return command;
		}

		static async Task<byte []> ReadExactAsync (Stream stream, int count)
		{
			var buffer = new byte [count];
			for (int offset = 0; offset < count;) {
				int read = await stream.ReadAsync (buffer, offset, count - offset);
				if (read == 0)
					throw new EndOfStreamException ();
				offset += read;
			}
			return buffer;
		}

		static async Task<bool> WaitForPeerCloseAsync (Stream stream)
		{
			using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (10));
			var buffer = new byte [256];
			try {
				while (await stream.ReadAsync (buffer, 0, buffer.Length, timeout.Token) > 0) {
				}
				return true;
			} catch (IOException) {
				return true;
			} catch (OperationCanceledException) {
				return false;
			}
		}

		public void Dispose ()
		{
			listener.Stop ();
			acceptLoop.Wait (TimeSpan.FromSeconds (10));
		}
	}
}
