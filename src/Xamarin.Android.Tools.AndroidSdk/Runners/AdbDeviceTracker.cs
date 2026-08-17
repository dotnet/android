// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools;

/// <summary>
/// Monitors ADB device connections in real-time via the <c>host:track-devices-l</c> socket protocol.
/// Pushes device list updates through a callback whenever devices connect, disconnect, or change state.
/// </summary>
public sealed class AdbDeviceTracker : IDisposable
{
	readonly object syncLock = new object ();
	readonly int port;
	readonly Action<TraceLevel, string> logger;
	readonly AdbClient adbClient;
	volatile IReadOnlyList<AdbDeviceInfo> currentDevices = Array.Empty<AdbDeviceInfo> ();
	CancellationTokenSource? trackingCts;
	bool isTracking;
	bool disposed;

	/// <summary>
	/// Creates a new AdbDeviceTracker.
	/// </summary>
	/// <param name="port">ADB daemon port (default 5037).</param>
	/// <param name="logger">Optional logger callback.</param>
	public AdbDeviceTracker (int port = 5037,
		Action<TraceLevel, string>? logger = null)
	{
		if (port <= 0 || port > 65535)
			throw new ArgumentOutOfRangeException (nameof (port), "Port must be between 1 and 65535.");
		this.port = port;
		this.logger = logger ?? RunnerDefaults.NullLogger;
		this.adbClient = new AdbClient ();
	}

	/// <summary>
	/// Current snapshot of tracked devices.
	/// </summary>
	public IReadOnlyList<AdbDeviceInfo> CurrentDevices => currentDevices;

	/// <summary>
	/// Starts tracking device changes. Calls <paramref name="onDevicesChanged"/> whenever
	/// the device list changes. Blocks until cancelled or disposed.
	/// Automatically reconnects on connection drops with exponential backoff.
	/// </summary>
	/// <param name="onDevicesChanged">Callback invoked with the updated device list on each change.</param>
	/// <param name="cancellationToken">Token to stop tracking.</param>
	/// <exception cref="InvalidOperationException">Thrown if tracking is already active.</exception>
	public async Task StartAsync (
		Action<IReadOnlyList<AdbDeviceInfo>> onDevicesChanged,
		CancellationToken cancellationToken = default)
	{
		if (onDevicesChanged == null)
			throw new ArgumentNullException (nameof (onDevicesChanged));

		CancellationTokenSource cts;
		lock (syncLock) {
			if (disposed)
				throw new ObjectDisposedException (nameof (AdbDeviceTracker));
			if (isTracking)
				throw new InvalidOperationException ("Tracking is already active. Cancel the token or dispose before starting again.");
			isTracking = true;
			cts = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
			trackingCts = cts;
		}

		var token = cts.Token;
		var backoffMs = InitialBackoffMs;

		try {
			while (!token.IsCancellationRequested) {
				try {
					await ConnectAndHandshakeAsync (token).ConfigureAwait (false);
					await ReadTrackingUpdatesAsync (
						onDevicesChanged,
						// Reset backoff only after the first payload is delivered — proof that the
						// session is actually usable, not just that the TCP socket accepted us.
						// Resetting at handshake (OKAY) would let a daemon that flaps OKAY-then-drop
						// pin reconnects at InitialBackoffMs forever instead of climbing to MaxBackoffMs.
						// host:track-devices-l always pushes the full device list immediately on
						// connect, so under healthy conditions this fires within milliseconds.
						onConnectionStable: () => backoffMs = InitialBackoffMs,
						token).ConfigureAwait (false);
				} catch (OperationCanceledException) when (token.IsCancellationRequested) {
					break;
				} catch (Exception ex) when (ex is IOException || ex is SocketException || ex is ObjectDisposedException) {
					if (token.IsCancellationRequested)
						break;
					logger.Invoke (TraceLevel.Warning, $"ADB tracking connection lost: {ex.Message}. Reconnecting in {backoffMs}ms...");
					try {
						await Task.Delay (backoffMs, token).ConfigureAwait (false);
					} catch (OperationCanceledException) {
						break;
					}
					backoffMs = Math.Min (backoffMs * 2, MaxBackoffMs);
				}
			}
		} finally {
			lock (syncLock) {
				isTracking = false;
				trackingCts = null;
			}
			cts.Dispose ();
		}
	}

	const int InitialBackoffMs = 500;
	const int MaxBackoffMs = 16000;

	async Task ConnectAndHandshakeAsync (CancellationToken cancellationToken)
	{
		await adbClient.ReconnectAsync (port, cancellationToken).ConfigureAwait (false);
		logger.Invoke (TraceLevel.Verbose, "Connected to ADB daemon, sending track-devices-l command");

		await adbClient.SendCommandAsync ("host:track-devices-l", cancellationToken).ConfigureAwait (false);
		await adbClient.EnsureOkayAsync (cancellationToken).ConfigureAwait (false);

		logger.Invoke (TraceLevel.Verbose, "ADB tracking active");
	}

	async Task ReadTrackingUpdatesAsync (
		Action<IReadOnlyList<AdbDeviceInfo>> onDevicesChanged,
		Action onConnectionStable,
		CancellationToken cancellationToken)
	{
		var connectionProven = false;
		// Read length-prefixed device list updates
		while (!cancellationToken.IsCancellationRequested) {
			var payload = await adbClient.ReadLengthPrefixedStringAsync (cancellationToken).ConfigureAwait (false);
			if (payload == null)
				throw new IOException ("ADB daemon closed the connection.");

			if (!connectionProven) {
				onConnectionStable ();
				connectionProven = true;
			}

			var lines = payload.Split ('\n');
			var devices = AdbRunner.ParseAdbDevicesOutput (lines);
			currentDevices = devices;
			onDevicesChanged (devices);
		}
	}

	public void Dispose ()
	{
		lock (syncLock) {
			if (disposed)
				return;
			disposed = true;
			trackingCts?.Cancel ();
			adbClient.Close ();
			trackingCts?.Dispose ();
		}
		adbClient.Dispose ();
	}
}
