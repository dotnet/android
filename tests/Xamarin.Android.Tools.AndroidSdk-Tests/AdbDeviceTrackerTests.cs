// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
public class AdbDeviceTrackerTests
{
	[Test]
	public void Constructor_InvalidPort_ThrowsArgumentOutOfRangeException ()
	{
		Assert.Throws<ArgumentOutOfRangeException> (() => new AdbDeviceTracker (port: 0));
		Assert.Throws<ArgumentOutOfRangeException> (() => new AdbDeviceTracker (port: -1));
		Assert.Throws<ArgumentOutOfRangeException> (() => new AdbDeviceTracker (port: 70000));
	}

	[Test]
	public void Constructor_ValidPort_Succeeds ()
	{
		using var tracker = new AdbDeviceTracker (port: 5037);
		Assert.IsNotNull (tracker);
		Assert.AreEqual (0, tracker.CurrentDevices.Count);
	}

	[Test]
	public void StartAsync_NullCallback_ThrowsArgumentNullException ()
	{
		using var tracker = new AdbDeviceTracker ();
		Assert.ThrowsAsync<ArgumentNullException> (() => tracker.StartAsync (null!));
	}

	[Test]
	public void StartAsync_AfterDispose_ThrowsObjectDisposedException ()
	{
		var tracker = new AdbDeviceTracker ();
		tracker.Dispose ();
		Assert.ThrowsAsync<ObjectDisposedException> (() => tracker.StartAsync (_ => { }));
	}

	[Test]
	public async Task StartAsync_CalledTwice_ThrowsInvalidOperationException ()
	{
		// Use a port where nothing is listening so ConnectAsync yields quickly
		using var tracker = new AdbDeviceTracker (port: 59999);
		using var cts = new CancellationTokenSource ();

		// First call sets isTracking synchronously before the first await
		var trackingTask = tracker.StartAsync (_ => { }, cts.Token);

		// Second call should throw because tracking is already active
		Assert.ThrowsAsync<InvalidOperationException> (
			() => tracker.StartAsync (_ => { }, cts.Token));

		cts.Cancel ();
		try { await trackingTask.ConfigureAwait (false); } catch (OperationCanceledException) { }
	}

	[Test]
	public void Dispose_MultipleTimes_DoesNotThrow ()
	{
		var tracker = new AdbDeviceTracker ();
		tracker.Dispose ();
		Assert.DoesNotThrow (() => tracker.Dispose ());
	}
}
