// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools;

/// <summary>
/// Runs Android Virtual Device Manager (avdmanager) commands.
/// </summary>
public class AvdManagerRunner
{
	readonly string avdManagerPath;
	readonly IDictionary<string, string>? environmentVariables;
	readonly Action<TraceLevel, string> logger;

	/// <summary>
	/// Creates a new AvdManagerRunner with the full path to the avdmanager executable.
	/// </summary>
	/// <param name="avdManagerPath">Full path to avdmanager (e.g., "/path/to/sdk/cmdline-tools/latest/bin/avdmanager").</param>
	/// <param name="environmentVariables">Optional environment variables to pass to avdmanager processes.</param>
	/// <param name="logger">Optional logger callback for diagnostic messages.</param>
	public AvdManagerRunner (string avdManagerPath, IDictionary<string, string>? environmentVariables = null, Action<TraceLevel, string>? logger = null)
	{
		if (string.IsNullOrWhiteSpace (avdManagerPath))
			throw new ArgumentException ("Path to avdmanager must not be empty.", nameof (avdManagerPath));
		this.avdManagerPath = avdManagerPath;
		this.environmentVariables = environmentVariables;
		this.logger = logger ?? RunnerDefaults.NullLogger;
	}

	public async Task<IReadOnlyList<AvdInfo>> ListAvdsAsync (CancellationToken cancellationToken = default)
	{
		logger.Invoke (TraceLevel.Verbose, "Running: avdmanager list avd");
		var result = await RunAvdManagerAsync (["list", "avd"], false, cancellationToken).ConfigureAwait (false);
		if (result.ExitCode != 0)
			throw new InvalidOperationException ($"'avdmanager list avd' failed with exit code {result.ExitCode}. stderr:{Environment.NewLine}{result.StandardError} stdout:{Environment.NewLine}{result.StandardOutput}");
		return ParseAvdListOutput (result.StandardOutput);
	}

	/// <summary>
	/// Creates an AVD with the specified name and system image. If <paramref name="force"/> is <c>false</c>
	/// and an AVD with the same name already exists, returns the existing AVD without re-creating it.
	/// Newly created AVDs enable GPU acceleration with automatic renderer selection.
	/// </summary>
	public async Task<AvdInfo> GetOrCreateAvdAsync (string name, string systemImage, string? deviceProfile = null,
		bool force = false, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace (name))
			throw new ArgumentException ("Value cannot be null or whitespace.", nameof (name));
		if (string.IsNullOrWhiteSpace (systemImage))
			throw new ArgumentException ("Value cannot be null or whitespace.", nameof (systemImage));

		// Check if AVD already exists — return it instead of failing
		if (!force) {
			var existing = (await ListAvdsAsync (cancellationToken).ConfigureAwait (false))
				.FirstOrDefault (a => string.Equals (a.Name, name, StringComparison.OrdinalIgnoreCase));
			if (existing is not null) {
				logger.Invoke (TraceLevel.Verbose, $"AVD '{name}' already exists, returning existing");
				return existing;
			}
		}

		var args = new List<string> { "create", "avd", "-n", name, "-k", systemImage };
		if (deviceProfile is { Length: > 0 })
			args.AddRange (new [] { "-d", deviceProfile });
		if (force)
			args.Add ("--force");

		var result = await RunAvdManagerAsync (args, true, cancellationToken).ConfigureAwait (false);
		if (result.ExitCode != 0)
			throw new InvalidOperationException ($"'avdmanager create avd -n {name}' failed with exit code {result.ExitCode}. stderr:{Environment.NewLine}{result.StandardError} stdout:{Environment.NewLine}{result.StandardOutput}");

		// Re-list to get the actual path from avdmanager (respects ANDROID_USER_HOME/ANDROID_AVD_HOME)
		var avds = await ListAvdsAsync (cancellationToken).ConfigureAwait (false);
		var created = avds.FirstOrDefault (a => string.Equals (a.Name, name, StringComparison.OrdinalIgnoreCase));
		if (created is null)
			throw new InvalidOperationException ($"avdmanager reported success but AVD '{name}' was not found in the list.");

		if (string.IsNullOrWhiteSpace (created.Path))
			throw new InvalidOperationException ($"avdmanager did not report a path for AVD '{name}'.");

		var configPath = Path.Combine (created.Path, "config.ini");
		var lines = File.ReadAllLines (configPath).Where (line => {
			var separator = line.IndexOf ('=');
			if (separator < 0)
				return true;
			var key = line.Substring (0, separator).Trim ();
			return key != "hw.gpu.enabled" && key != "hw.gpu.mode";
		}).ToList ();
		lines.Add ("hw.gpu.enabled=yes");
		lines.Add ("hw.gpu.mode=auto");
		File.WriteAllLines (configPath, lines);
		return created;
	}

	async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAvdManagerAsync (
		IEnumerable<string> arguments, bool create, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested ();
		var psi = new ProcessStartInfo (avdManagerPath) {
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardInput = create,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		foreach (var argument in arguments)
			psi.ArgumentList.Add (argument);
		if (environmentVariables != null) {
			foreach (var variable in environmentVariables)
				psi.Environment [variable.Key] = variable.Value;
		}
		using var process = new Process { StartInfo = psi };
		process.Start ();
		using var execution = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
		if (!create)
			execution.CancelAfter (TimeSpan.FromSeconds (30));
		using var reading = CancellationTokenSource.CreateLinkedTokenSource (execution.Token);
		var exit = process.SafeHandle.WaitForExitOrKillOnCancellationAsync (execution.Token);
		var capture = CaptureAsync ();
		using var stdout = process.StandardOutput;
		using var stderr = process.StandardError;
		var input = create ? AnswerAsync () : Task.CompletedTask;
		var monitor = BoundDrainAsync ();
		try {
			var status = await exit.WaitAsync (cancellationToken).ConfigureAwait (false);
			await Task.WhenAll (capture, input).ConfigureAwait (false);
			var output = await capture.ConfigureAwait (false);
			cancellationToken.ThrowIfCancellationRequested ();
			execution.Token.ThrowIfCancellationRequested ();
			return (status.ExitCode, output.StandardOutput, output.StandardError);
		} catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
			throw new TimeoutException (execution.IsCancellationRequested
				? "avdmanager discovery did not complete within 30 seconds."
				: "avdmanager exited, but its redirected output did not close within 30 seconds.");
		} catch (OperationCanceledException ex) {
			throw new OperationCanceledException (ex.Message, ex, cancellationToken);
		} finally {
			execution.Cancel ();
			reading.Cancel ();
			try {
				await Task.WhenAll (capture, input).ConfigureAwait (false);
			} catch (OperationCanceledException) when (execution.IsCancellationRequested) {
				// Stop both owned streams and the creation answer without waiting for descendant EOF.
			} finally {
				try {
					await exit.WaitAsync (TimeSpan.FromSeconds (5)).ConfigureAwait (false);
				} finally {
					await monitor.ConfigureAwait (false);
				}
			}
		}

		async Task BoundDrainAsync ()
		{
			await exit.ConfigureAwait (false);
			reading.CancelAfter (TimeSpan.FromSeconds (30));
		}

		async Task<(string StandardOutput, string StandardError)> CaptureAsync ()
		{
			try {
				return await process.ReadAllTextAsync (reading.Token).ConfigureAwait (false);
			} catch (OperationCanceledException) when (reading.IsCancellationRequested) {
				throw;
			} catch {
				execution.Cancel ();
				throw;
			}
		}

		async Task AnswerAsync ()
		{
			try {
				try {
					using var writer = process.StandardInput;
					await writer.WriteLineAsync ("no".AsMemory (), execution.Token).ConfigureAwait (false);
				} catch (IOException ex) {
					logger.Invoke (TraceLevel.Warning, $"Failed to write to avdmanager stdin: {ex.Message}");
				}
			} catch {
				execution.Cancel ();
				throw;
			}
		}
	}

	public async Task DeleteAvdAsync (string name, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace (name))
			throw new ArgumentException ("Value cannot be null or whitespace.", nameof (name));

		// Idempotent: if the AVD doesn't exist, treat as success
		var avds = await ListAvdsAsync (cancellationToken).ConfigureAwait (false);
		if (!avds.Any (a => string.Equals (a.Name, name, StringComparison.OrdinalIgnoreCase))) {
			logger.Invoke (TraceLevel.Verbose, $"AVD '{name}' does not exist, nothing to delete");
			return;
		}

		var result = await RunAvdManagerAsync (["delete", "avd", "--name", name], false, cancellationToken).ConfigureAwait (false);
		if (result.ExitCode != 0)
			throw new InvalidOperationException ($"'avdmanager delete avd --name {name}' failed with exit code {result.ExitCode}. stderr:{Environment.NewLine}{result.StandardError}");
	}

	/// <summary>
	/// Lists available device profiles (hardware definitions) using <c>avdmanager list device --compact</c>.
	/// </summary>
	public async Task<IReadOnlyList<AvdDeviceProfile>> ListDeviceProfilesAsync (CancellationToken cancellationToken = default)
	{
		logger.Invoke (TraceLevel.Verbose, "Running: avdmanager list device --compact");
		var result = await RunAvdManagerAsync (["list", "device", "--compact"], false, cancellationToken).ConfigureAwait (false);
		if (result.ExitCode != 0)
			throw new InvalidOperationException ($"'avdmanager list device --compact' failed with exit code {result.ExitCode}. stderr:{Environment.NewLine}{result.StandardError} stdout:{Environment.NewLine}{result.StandardOutput}");
		return ParseCompactDeviceListOutput (result.StandardOutput);
	}

	internal static IReadOnlyList<AvdDeviceProfile> ParseCompactDeviceListOutput (string output)
	{
		var profiles = new List<AvdDeviceProfile> ();

		foreach (var line in output.Split ('\n')) {
			var trimmed = line.Trim ();
			if (trimmed.Length > 0)
				profiles.Add (new AvdDeviceProfile (trimmed));
		}

		return profiles;
	}

	internal static IReadOnlyList<AvdInfo> ParseAvdListOutput (string output)
	{
		var avds = new List<AvdInfo> ();
		string? currentName = null, currentDevice = null, currentPath = null;

		foreach (var line in output.Split ('\n')) {
			var trimmed = line.Trim ();
			if (trimmed.StartsWith ("Name:", StringComparison.OrdinalIgnoreCase)) {
				if (currentName is not null)
					avds.Add (new AvdInfo (currentName, currentDevice, currentPath));
				currentName = trimmed.Substring (5).Trim ();
				currentDevice = currentPath = null;
			}
			else if (trimmed.StartsWith ("Device:", StringComparison.OrdinalIgnoreCase))
				currentDevice = trimmed.Substring (7).Trim ();
			else if (trimmed.StartsWith ("Path:", StringComparison.OrdinalIgnoreCase))
				currentPath = trimmed.Substring (5).Trim ();
		}

		if (currentName is not null)
			avds.Add (new AvdInfo (currentName, currentDevice, currentPath));

		return avds;
	}

}
