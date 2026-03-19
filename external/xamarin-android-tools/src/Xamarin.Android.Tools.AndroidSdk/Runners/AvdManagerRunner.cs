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
		using var stdout = new StringWriter ();
		using var stderr = new StringWriter ();
		var psi = ProcessUtils.CreateProcessStartInfo (avdManagerPath, "list", "avd");
		logger.Invoke (TraceLevel.Verbose, "Running: avdmanager list avd");
		var exitCode = await ProcessUtils.StartProcess (psi, stdout, stderr, cancellationToken, environmentVariables).ConfigureAwait (false);

		ProcessUtils.ThrowIfFailed (exitCode, "avdmanager list avd", stderr);

		return ParseAvdListOutput (stdout.ToString ());
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

		using var stdout = new StringWriter ();
		using var stderr = new StringWriter ();
		var psi = ProcessUtils.CreateProcessStartInfo (avdManagerPath, args.ToArray ());
		psi.RedirectStandardInput = true;

		// avdmanager prompts "Do you wish to create a custom hardware profile?" — answer "no"
		var exitCode = await ProcessUtils.StartProcess (psi, stdout, stderr, cancellationToken, environmentVariables,
			onStarted: p => {
				try {
					p.StandardInput.WriteLine ("no");
					p.StandardInput.Close ();
				} catch (IOException ex) {
					logger.Invoke (TraceLevel.Warning, $"Failed to write to avdmanager stdin: {ex.Message}");
				}
			}).ConfigureAwait (false);

		ProcessUtils.ThrowIfFailed (exitCode, $"avdmanager create avd -n {name}", stderr, stdout);

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

		using var stderr = new StringWriter ();
		var psi = ProcessUtils.CreateProcessStartInfo (avdManagerPath, "delete", "avd", "--name", name);
		var exitCode = await ProcessUtils.StartProcess (psi, null, stderr, cancellationToken, environmentVariables).ConfigureAwait (false);

		ProcessUtils.ThrowIfFailed (exitCode, $"avdmanager delete avd --name {name}", stderr);
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

