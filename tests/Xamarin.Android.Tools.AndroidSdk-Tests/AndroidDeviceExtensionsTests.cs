using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Xamarin.AndroidTools.Debugging;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
public class AndroidDeviceExtensionsTests
{
	[Test]
	public async Task GetProcessIDAsyncRetriesUntilProcessAppears ()
	{
		var processIds = new Queue<int> (new [] { 0, 0, 1234 });

		var processId = await AndroidDeviceExtensions.GetProcessIDAsync (
			_ => Task.FromResult (processIds.Dequeue ()),
			maxAttempts: 20,
			timeBetweenAttempts: 0,
			token: CancellationToken.None
		);

		Assert.AreEqual (1234, processId);
		Assert.AreEqual (0, processIds.Count);
	}

	[Test]
	public async Task GetProcessIDAsyncPerformsInitialAttemptAndMaximumRetries ()
	{
		var attempts = 0;

		var processId = await AndroidDeviceExtensions.GetProcessIDAsync (
			_ => {
				attempts++;
				return Task.FromResult (0);
			},
			maxAttempts: 2,
			timeBetweenAttempts: 0,
			token: CancellationToken.None
		);

		Assert.AreEqual (0, processId);
		Assert.AreEqual (3, attempts);
	}

	[Test]
	public void GetProcessIDAsyncHonorsCancellation ()
	{
		using (var cancellationTokenSource = new CancellationTokenSource ()) {
			cancellationTokenSource.Cancel ();

			Assert.CatchAsync<OperationCanceledException> (() => AndroidDeviceExtensions.GetProcessIDAsync (
				_ => Task.FromResult (0),
				maxAttempts: 20,
				timeBetweenAttempts: 1,
				token: cancellationTokenSource.Token
			));
		}
	}

	[Test]
	public async Task ConnectJdwpLogsAnonymousOperationsInOrder ()
	{
		var messages = new List<string> ();
		var operations = new List<DebuggingExtensions.JdwpOperation> ();
		var missingProcess = new InvalidOperationException ("secret.package");

		await DebuggingExtensions.ConnectJdwpAsync ((operation, token) => {
			operations.Add (operation);
			return Task.FromResult (true);
		}, messages.Add, missingProcess, CancellationToken.None);

		CollectionAssert.AreEqual (new [] {
			DebuggingExtensions.JdwpOperation.PidLookup,
			DebuggingExtensions.JdwpOperation.Forward,
			DebuggingExtensions.JdwpOperation.ClientConnect,
			DebuggingExtensions.JdwpOperation.Hold,
			DebuggingExtensions.JdwpOperation.Disconnect,
			DebuggingExtensions.JdwpOperation.KillForward,
		}, operations);
		CollectionAssert.AreEqual (new [] {
			"pid-lookup-begin", "pid-lookup-end",
			"forward-begin", "forward-end",
			"client-connect-begin", "client-connect-completed",
			"hold-begin", "hold-end",
			"disconnect-begin", "disconnect-end",
			"killforward-begin", "killforward-end",
		}, messages.Skip (1).Select (message => Regex.Match (message, @"^JDWP ([a-z-]+) elapsedMs=\d+(?: found=true| duration-ms=1400)?$").Groups [1].Value));
		var anchor = Regex.Match (messages [0], @"^JDWP start utc=(\S+) elapsedMs=0$");
		Assert.That (anchor.Success, Is.True);
		var timestamp = DateTimeOffset.ParseExact (anchor.Groups [1].Value, "O", CultureInfo.InvariantCulture);
		Assert.That (timestamp.Offset, Is.EqualTo (TimeSpan.Zero));
		Assert.That (messages.Count (message => message.StartsWith ("JDWP start ")), Is.EqualTo (1));
		Assert.That (messages.Any (message => Regex.IsMatch (message, @"^JDWP pid-lookup-end elapsedMs=\d+ found=true$")), Is.True);
		Assert.That (messages.Any (message => Regex.IsMatch (message, @"^JDWP hold-end elapsedMs=\d+ duration-ms=1400$")), Is.True);
		var elapsed = messages.Skip (1).Select (message => long.Parse (Regex.Match (message, @"elapsedMs=(\d+)").Groups [1].Value, CultureInfo.InvariantCulture)).ToArray ();
		Assert.That (elapsed, Is.Ordered);
		Assert.That (messages.Any (message => message.Contains ("secret.package")), Is.False);
	}

	[Test]
	public void ConnectJdwpLogsFailureAndCleansUpWithoutLeakingIdentity ()
	{
		var messages = new List<string> ();
		var operations = new List<DebuggingExtensions.JdwpOperation> ();
		var failure = new InvalidOperationException ("secret.package serial-canary port-49152 pid-8675309");
		var cleanupToken = new CancellationToken (true);
		using var cancellation = new CancellationTokenSource ();

		var thrown = Assert.ThrowsAsync<InvalidOperationException> (async () =>
			await DebuggingExtensions.ConnectJdwpAsync ((operation, token) => {
				operations.Add (operation);
				if (operation == DebuggingExtensions.JdwpOperation.KillForward)
					cleanupToken = token;
				if (operation == DebuggingExtensions.JdwpOperation.ClientConnect) {
					cancellation.Cancel ();
					throw failure;
				}
				return Task.FromResult (true);
			}, messages.Add, new InvalidOperationException ("missing"), cancellation.Token));

		Assert.That (thrown, Is.SameAs (failure));
		CollectionAssert.AreEqual (new [] {
			DebuggingExtensions.JdwpOperation.PidLookup,
			DebuggingExtensions.JdwpOperation.Forward,
			DebuggingExtensions.JdwpOperation.ClientConnect,
			DebuggingExtensions.JdwpOperation.KillForward,
		}, operations);
		Assert.That (cleanupToken, Is.EqualTo (CancellationToken.None));
		Assert.That (messages.Any (message => Regex.IsMatch (message, @"^JDWP client-connect-failed elapsedMs=\d+ exception-type=InvalidOperationException$")), Is.True);
		Assert.That (messages.Any (message => message.StartsWith ("JDWP client-connect-completed ")), Is.False);
		Assert.That (messages.Last (), Does.StartWith ("JDWP killforward-end "));
		foreach (var canary in new [] { "secret.package", "serial-canary", "49152", "8675309" })
			Assert.That (messages.Any (message => message.Contains (canary)), Is.False);
	}

	[Test]
	public void ConnectJdwpMissingProcessLogsFailureAndDoesNotForward ()
	{
		var messages = new List<string> ();
		var operations = new List<DebuggingExtensions.JdwpOperation> ();
		var missingProcess = new InvalidOperationException ("secret.package");

		var thrown = Assert.ThrowsAsync<InvalidOperationException> (async () =>
			await DebuggingExtensions.ConnectJdwpAsync ((operation, token) => {
				operations.Add (operation);
				return Task.FromResult (false);
			}, messages.Add, missingProcess, CancellationToken.None));

		Assert.That (thrown, Is.SameAs (missingProcess));
		CollectionAssert.AreEqual (new [] { DebuggingExtensions.JdwpOperation.PidLookup }, operations);
		Assert.That (messages.Any (message => Regex.IsMatch (message, @"^JDWP pid-lookup-end elapsedMs=\d+ found=false$")), Is.True);
		Assert.That (messages.Any (message => message.Contains ("secret.package")), Is.False);
	}

	[Test]
	public void ConnectJdwpDoesNotRemoveForwardWhenForwardFails ()
	{
		var messages = new List<string> ();
		var operations = new List<DebuggingExtensions.JdwpOperation> ();
		var failure = new InvalidOperationException ("sensitive endpoint");

		var thrown = Assert.ThrowsAsync<InvalidOperationException> (async () =>
			await DebuggingExtensions.ConnectJdwpAsync ((operation, token) => {
				operations.Add (operation);
				if (operation == DebuggingExtensions.JdwpOperation.Forward)
					throw failure;
				return Task.FromResult (true);
			}, messages.Add, new InvalidOperationException ("missing"), CancellationToken.None));

		Assert.That (thrown, Is.SameAs (failure));
		CollectionAssert.AreEqual (new [] { DebuggingExtensions.JdwpOperation.PidLookup, DebuggingExtensions.JdwpOperation.Forward }, operations);
		Assert.That (messages.Last (), Does.Contain ("forward-failed "));
		Assert.That (messages.Any (message => message.Contains ("sensitive endpoint")), Is.False);
	}

	[Test]
	public async Task ConnectJdwpAcceptsNoLogWriter ()
	{
		var operations = new List<DebuggingExtensions.JdwpOperation> ();
		await DebuggingExtensions.ConnectJdwpAsync ((operation, token) => {
			operations.Add (operation);
			return Task.FromResult (true);
		}, null, new InvalidOperationException ("missing"), CancellationToken.None);

		Assert.That (operations.Last (), Is.EqualTo (DebuggingExtensions.JdwpOperation.KillForward));
	}

	[Test]
	public async Task ConnectJdwpRunsLogcatSnapshotOnlyAfterKillForward ()
	{
		var messages = new List<string> ();
		var events = new List<string> ();

		await DebuggingExtensions.ConnectJdwpAsync ((operation, token) => {
			events.Add (operation.ToString ());
			return Task.FromResult (true);
		}, messages.Add, new InvalidOperationException ("missing"), CancellationToken.None, token => {
			events.Add ("snapshot");
			return Task.CompletedTask;
		});

		CollectionAssert.AreEqual (new [] { "PidLookup", "Forward", "ClientConnect", "Hold", "Disconnect", "KillForward", "snapshot" }, events);
		Assert.That (messages [messages.Count - 3], Does.StartWith ("JDWP killforward-end "));
		Assert.That (messages [messages.Count - 2], Does.Match (@"^JDWP logcat-snapshot-begin elapsedMs=\d+$"));
		Assert.That (messages [messages.Count - 1], Does.Match (@"^JDWP logcat-snapshot-end elapsedMs=\d+$"));
	}

	[Test]
	public void ConnectJdwpSnapshotFailureDoesNotMaskOriginalFailure ()
	{
		var failure = new InvalidOperationException ("connect");
		var snapshotRan = false;
		var messages = new List<string> ();

		var thrown = Assert.ThrowsAsync<InvalidOperationException> (async () =>
			await DebuggingExtensions.ConnectJdwpAsync ((operation, token) => {
				if (operation == DebuggingExtensions.JdwpOperation.ClientConnect)
					throw failure;
				return Task.FromResult (true);
			}, messages.Add, new InvalidOperationException ("missing"), CancellationToken.None, token => {
				snapshotRan = true;
				throw new IOException ("secret-message com.secret.app");
			}));

		Assert.That (thrown, Is.SameAs (failure));
		Assert.That (snapshotRan, Is.True);
		Assert.That (messages.Last (), Is.EqualTo ("JDWP logcat-failed phase=snapshot exception-type=IOException"));
		Assert.That (messages.Any (message => message.Contains ("secret-message") || message.Contains ("com.secret.app")), Is.False);
		Assert.That (messages.Any (message => message.StartsWith ("JDWP logcat-snapshot-end ")), Is.False);
	}

	[Test]
	public async Task ConnectJdwpReportsSnapshotFailureWithoutChangingSuccess ()
	{
		var messages = new List<string> ();

		await DebuggingExtensions.ConnectJdwpAsync ((operation, token) => Task.FromResult (true),
			messages.Add, new InvalidOperationException ("missing"), CancellationToken.None,
			token => throw new IOException ("secret-message com.secret.app"));

		Assert.That (messages.Last (), Is.EqualTo ("JDWP logcat-failed phase=snapshot exception-type=IOException"));
		Assert.That (messages.Any (message => message.Contains ("secret-message") || message.Contains ("com.secret.app")), Is.False);
		Assert.That (messages.Any (message => message.StartsWith ("JDWP logcat-snapshot-end ")), Is.False);
	}

	[Test]
	public void ConnectJdwpKeepsOriginalFailureWhenSnapshotCancels ()
	{
		using var cancellation = new CancellationTokenSource ();
		var failure = new InvalidOperationException ("connect");
		var messages = new List<string> ();

		var thrown = Assert.ThrowsAsync<InvalidOperationException> (async () =>
			await DebuggingExtensions.ConnectJdwpAsync ((operation, token) => {
				if (operation == DebuggingExtensions.JdwpOperation.ClientConnect)
					throw failure;
				return Task.FromResult (true);
			}, messages.Add, new InvalidOperationException ("missing"), cancellation.Token, token => {
				cancellation.Cancel ();
				throw new OperationCanceledException (token);
			}));

		Assert.That (thrown, Is.SameAs (failure));
		Assert.That (messages.Last (), Is.EqualTo ("JDWP logcat-failed phase=snapshot exception-type=OperationCanceledException"));
	}

	[Test]
	public async Task ConnectJdwpSnapshotAndLoggerFailuresDoNotChangeSuccess ()
	{
		await DebuggingExtensions.ConnectJdwpAsync ((operation, token) => Task.FromResult (true),
			message => {
				if (message.StartsWith ("JDWP logcat-", StringComparison.Ordinal))
					throw new InvalidOperationException ("logger");
			}, new InvalidOperationException ("missing"), CancellationToken.None, token => throw new IOException ("snapshot"));
	}

	[Test]
	public void ConnectJdwpSkipsSnapshotWhenKillForwardFails ()
	{
		var failure = new InvalidOperationException ("killforward");
		var snapshotRan = false;

		var thrown = Assert.ThrowsAsync<InvalidOperationException> (async () =>
			await DebuggingExtensions.ConnectJdwpAsync ((operation, token) => {
				if (operation == DebuggingExtensions.JdwpOperation.KillForward)
					throw failure;
				return Task.FromResult (true);
			}, null, new InvalidOperationException ("missing"), CancellationToken.None, token => {
				snapshotRan = true;
				return Task.CompletedTask;
			}));

		Assert.That (thrown, Is.SameAs (failure));
		Assert.That (snapshotRan, Is.False);
	}

	[Test]
	public void ConnectJdwpPropagatesCancellationDuringSnapshot ()
	{
		using var cancellation = new CancellationTokenSource ();

		Assert.CatchAsync<OperationCanceledException> (async () =>
			await DebuggingExtensions.ConnectJdwpAsync ((operation, token) => Task.FromResult (true),
				null, new InvalidOperationException ("missing"), cancellation.Token, token => {
					cancellation.Cancel ();
					token.ThrowIfCancellationRequested ();
					return Task.CompletedTask;
				}));
	}

	[Test]
	public void ConnectJdwpSkipsSnapshotAfterBuildCancellation ()
	{
		using var cancellation = new CancellationTokenSource ();
		var snapshotRan = false;

		Assert.CatchAsync<OperationCanceledException> (async () =>
			await DebuggingExtensions.ConnectJdwpAsync ((operation, token) => {
				if (operation == DebuggingExtensions.JdwpOperation.Hold) {
					cancellation.Cancel ();
					token.ThrowIfCancellationRequested ();
				}
				return Task.FromResult (true);
			}, null, new InvalidOperationException ("missing"), cancellation.Token, token => {
				snapshotRan = true;
				return Task.CompletedTask;
			}));

		Assert.That (snapshotRan, Is.False);
	}
}
