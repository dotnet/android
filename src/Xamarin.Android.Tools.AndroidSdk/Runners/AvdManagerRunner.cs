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
		cancellationToken.ThrowIfCancellationRequested ();
		var psi = new ProcessStartInfo (avdManagerPath) {
			ArgumentList = { "list", "avd" },
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		if (environmentVariables != null) {
			foreach (var variable in environmentVariables)
				psi.Environment [variable.Key] = variable.Value;
		}
		logger.Invoke (TraceLevel.Verbose, "Running: avdmanager list avd");
		var result = await Process.RunAndCaptureTextAsync (psi, cancellationToken).ConfigureAwait (false);
		cancellationToken.ThrowIfCancellationRequested ();
		if (result.ExitStatus.ExitCode != 0)
			throw new InvalidOperationException ($"'avdmanager list avd' failed with exit code {result.ExitStatus.ExitCode}. stderr:{Environment.NewLine}{result.StandardError} stdout:{Environment.NewLine}{result.StandardOutput}");
		return ParseAvdListOutput (result.StandardOutput);
	}

	/// <summary>
	/// Creates an AVD with the specified name and system image. If <paramref name="force"/> is <c>false</c>
	/// and an AVD with the same name already exists, returns the existing AVD without re-creating it.
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

		cancellationToken.ThrowIfCancellationRequested ();
		var psi = new ProcessStartInfo (avdManagerPath) {
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		foreach (var argument in args)
			psi.ArgumentList.Add (argument);
		if (environmentVariables != null) {
			foreach (var variable in environmentVariables)
				psi.Environment [variable.Key] = variable.Value;
		}
		using var process = new Process { StartInfo = psi };
		process.Start ();
		using var execution = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
		var exit = process.SafeHandle.WaitForExitOrKillOnCancellationAsync (execution.Token);
		var capture = process.ReadAllTextAsync (execution.Token);
		using var stdout = process.StandardOutput;
		using var stderr = process.StandardError;
		using var input = process.StandardInput;

		// avdmanager prompts "Do you wish to create a custom hardware profile?" — answer "no"
		try {
			try {
				input.WriteLine ("no");
				input.Close ();
			} catch (IOException ex) {
				logger.Invoke (TraceLevel.Warning, $"Failed to write to avdmanager stdin: {ex.Message}");
			}
			var result = await capture.ConfigureAwait (false);
			var status = await exit.ConfigureAwait (false);
			cancellationToken.ThrowIfCancellationRequested ();
			if (status.ExitCode != 0)
				throw new InvalidOperationException ($"'avdmanager create avd -n {name}' failed with exit code {status.ExitCode}. stderr:{Environment.NewLine}{result.StandardError} stdout:{Environment.NewLine}{result.StandardOutput}");
		} finally {
			execution.Cancel ();
			await exit.WaitAsync (TimeSpan.FromSeconds (5)).ConfigureAwait (false);
		}

		// Re-list to get the actual path from avdmanager (respects ANDROID_USER_HOME/ANDROID_AVD_HOME)
		var avds = await ListAvdsAsync (cancellationToken).ConfigureAwait (false);
		var created = avds.FirstOrDefault (a => string.Equals (a.Name, name, StringComparison.OrdinalIgnoreCase));
		if (created is not null)
			return created;

		throw new InvalidOperationException ($"avdmanager reported success but AVD '{name}' was not found in the list.");
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

		var psi = new ProcessStartInfo (avdManagerPath) {
			ArgumentList = { "delete", "avd", "--name", name },
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		if (environmentVariables != null) {
			foreach (var variable in environmentVariables)
				psi.Environment [variable.Key] = variable.Value;
		}
		var result = await Process.RunAndCaptureTextAsync (psi, cancellationToken).ConfigureAwait (false);
		cancellationToken.ThrowIfCancellationRequested ();
		if (result.ExitStatus.ExitCode != 0)
			throw new InvalidOperationException ($"'avdmanager delete avd --name {name}' failed with exit code {result.ExitStatus.ExitCode}. stderr:{Environment.NewLine}{result.StandardError}");
	}

	/// <summary>
	/// Lists available device profiles (hardware definitions) using <c>avdmanager list device --compact</c>.
	/// </summary>
	public async Task<IReadOnlyList<AvdDeviceProfile>> ListDeviceProfilesAsync (CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested ();
		var psi = new ProcessStartInfo (avdManagerPath) {
			ArgumentList = { "list", "device", "--compact" },
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		if (environmentVariables != null) {
			foreach (var variable in environmentVariables)
				psi.Environment [variable.Key] = variable.Value;
		}
		logger.Invoke (TraceLevel.Verbose, "Running: avdmanager list device --compact");
		var result = await Process.RunAndCaptureTextAsync (psi, cancellationToken).ConfigureAwait (false);
		cancellationToken.ThrowIfCancellationRequested ();
		if (result.ExitStatus.ExitCode != 0)
			throw new InvalidOperationException ($"'avdmanager list device --compact' failed with exit code {result.ExitStatus.ExitCode}. stderr:{Environment.NewLine}{result.StandardError} stdout:{Environment.NewLine}{result.StandardOutput}");
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
