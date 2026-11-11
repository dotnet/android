// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Tools.Tests;

[TestClass]
public class AdbDeviceTrackerTests
{
	[TestMethod]
	public void Constructor_InvalidPort_ThrowsArgumentOutOfRangeException ()
	{
		Assert.ThrowsExactly<ArgumentOutOfRangeException> (() => new AdbDeviceTracker (port: 0));
		Assert.ThrowsExactly<ArgumentOutOfRangeException> (() => new AdbDeviceTracker (port: -1));
		Assert.ThrowsExactly<ArgumentOutOfRangeException> (() => new AdbDeviceTracker (port: 70000));
	}

	[TestMethod]
	public void Constructor_ValidPort_Succeeds ()
	{
		using var tracker = new AdbDeviceTracker (port: 5037);
		Assert.IsNotNull (tracker);
		Assert.AreEqual (0, tracker.CurrentDevices.Count);
	}

	[TestMethod]
	public async Task StartAsync_NullCallback_ThrowsArgumentNullException ()
	{
		using var tracker = new AdbDeviceTracker ();
		await Assert.ThrowsExactlyAsync<ArgumentNullException> (() => tracker.StartAsync (null!));
	}

	[TestMethod]
	public async Task StartAsync_AfterDispose_ThrowsObjectDisposedException ()
	{
		var tracker = new AdbDeviceTracker ();
		tracker.Dispose ();
		await Assert.ThrowsExactlyAsync<ObjectDisposedException> (() => tracker.StartAsync (_ => { }));
	}

	[TestMethod]
	public async Task StartAsync_CalledTwice_ThrowsInvalidOperationException ()
	{
		// Use a port where nothing is listening so ConnectAsync yields quickly
		using var tracker = new AdbDeviceTracker (port: 59999);
		using var cts = new CancellationTokenSource ();

		// First call sets isTracking synchronously before the first await
		var trackingTask = tracker.StartAsync (_ => { }, cts.Token);

		// Second call should throw because tracking is already active
		await Assert.ThrowsExactlyAsync<InvalidOperationException> (
			() => tracker.StartAsync (_ => { }, cts.Token));

		cts.Cancel ();
		try { await trackingTask.ConfigureAwait (false); } catch (OperationCanceledException) { }
	}

	[TestMethod]
	public void Dispose_MultipleTimes_DoesNotThrow ()
	{
		var tracker = new AdbDeviceTracker ();
		tracker.Dispose ();
		tracker.Dispose ();
	}
}
