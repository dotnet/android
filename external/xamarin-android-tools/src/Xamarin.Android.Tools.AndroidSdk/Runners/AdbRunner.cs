// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools;

/// <summary>
/// Runs Android Debug Bridge (adb) commands.
/// Parsing logic ported from dotnet/android GetAvailableAndroidDevices task.
/// </summary>
public class AdbRunner
{
	readonly string adbPath;
	readonly IDictionary<string, string>? environmentVariables;
	readonly Action<TraceLevel, string> logger;

	// Pattern to match device lines: <serial> <state> [key:value ...]
	// Uses \s+ to match one or more whitespace characters (spaces or tabs) between fields.
	// Explicit state list prevents false positives from non-device lines.
	static readonly Regex AdbDevicesRegex = new Regex (
		@"^([^\s]+)\s+(device|offline|unauthorized|authorizing|no permissions|recovery|sideload|bootloader|connecting|host)\s*(.*)$",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);
	static readonly Regex ApiRegex = new Regex (@"\bApi\b", RegexOptions.Compiled);

	/// <summary>
	/// Creates a new AdbRunner with the full path to the adb executable.
	/// </summary>
	/// <param name="adbPath">Full path to the adb executable (e.g., "/path/to/sdk/platform-tools/adb").</param>
	/// <param name="environmentVariables">Optional environment variables to pass to adb processes.</param>
	/// <param name="logger">Optional logger callback for diagnostic messages.</param>
	public AdbRunner (string adbPath, IDictionary<string, string>? environmentVariables = null, Action<TraceLevel, string>? logger = null)
	{
		if (string.IsNullOrWhiteSpace (adbPath))
			throw new ArgumentException ("Path to adb must not be empty.", nameof (adbPath));
		this.adbPath = adbPath;
		this.environmentVariables = environmentVariables;
		this.logger = logger ?? RunnerDefaults.NullLogger;
	}

	/// <summary>
	/// Lists connected devices using 'adb devices -l'.
	/// For emulators, queries the AVD name using 'adb -s &lt;serial&gt; emu avd name'.
	/// </summary>
	public virtual async Task<IReadOnlyList<AdbDeviceInfo>> ListDevicesAsync (CancellationToken cancellationToken = default)
	{
		using var stdout = new StringWriter ();
		using var stderr = new StringWriter ();
		var psi = ProcessUtils.CreateProcessStartInfo (adbPath, "devices", "-l");
		var exitCode = await ProcessUtils.StartProcess (psi, stdout, stderr, cancellationToken, environmentVariables).ConfigureAwait (false);

		ProcessUtils.ThrowIfFailed (exitCode, "adb devices -l", stderr);

		var devices = ParseAdbDevicesOutput (stdout.ToString ().Split ('\n'));

		// For each emulator, try to get the AVD name
		foreach (var device in devices) {
			if (device.Type == AdbDeviceType.Emulator) {
				device.AvdName = await GetEmulatorAvdNameAsync (device.Serial, cancellationToken).ConfigureAwait (false);
				device.Description = BuildDeviceDescription (device);
			}
		}

		return devices;
	}

	/// <summary>
	/// Queries the emulator for its AVD name.
	/// Tries <c>adb -s &lt;serial&gt; emu avd name</c> first (emulator console protocol),
	/// then falls back to <c>adb shell getprop ro.boot.qemu.avd_name</c> which reads the
	/// boot property set by the emulator kernel. The fallback is needed because
	/// <c>emu avd name</c> can return empty output on some adb/emulator version
	/// combinations (observed with adb v36).
	/// </summary>
	internal async Task<string?> GetEmulatorAvdNameAsync (string serial, CancellationToken cancellationToken = default)
	{
		// Try 1: Console command (fast, works on most emulator versions)
		try {
			using var stdout = new StringWriter ();
			var psi = ProcessUtils.CreateProcessStartInfo (adbPath, "-s", serial, "emu", "avd", "name");
			await ProcessUtils.StartProcess (psi, stdout, null, cancellationToken, environmentVariables).ConfigureAwait (false);

			foreach (var line in stdout.ToString ().Split ('\n')) {
				var trimmed = line.Trim ();
				if (!string.IsNullOrEmpty (trimmed) &&
					!string.Equals (trimmed, "OK", StringComparison.OrdinalIgnoreCase)) {
					return trimmed;
				}
			}
		} catch (OperationCanceledException) {
			throw;
		} catch {
			// Fall through to getprop fallback
		}

		// Try 2: Shell property (fallback when 'adb emu avd name' returns empty on some adb/emulator versions)
		try {
			var avdName = await GetShellPropertyAsync (serial, "ro.boot.qemu.avd_name", cancellationToken).ConfigureAwait (false);
			if (avdName is { Length: > 0 } name && !string.IsNullOrWhiteSpace (name))
				return name.Trim ();
		} catch (OperationCanceledException) {
			throw;
		} catch {
			// Both methods failed
		}

		return null;
	}

	public async Task WaitForDeviceAsync (string? serial = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
	{
		var effectiveTimeout = timeout ?? TimeSpan.FromSeconds (60);

		if (effectiveTimeout <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException (nameof (timeout), effectiveTimeout, "Timeout must be a positive value.");

		var args = serial is { Length: > 0 } s
			? new [] { "-s", s, "wait-for-device" }
			: new [] { "wait-for-device" };

		var psi = ProcessUtils.CreateProcessStartInfo (adbPath, args);

		using var cts = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
		cts.CancelAfter (effectiveTimeout);

		using var stdout = new StringWriter ();
		using var stderr = new StringWriter ();

		try {
			var exitCode = await ProcessUtils.StartProcess (psi, stdout, stderr, cts.Token, environmentVariables).ConfigureAwait (false);
			ProcessUtils.ThrowIfFailed (exitCode, "adb wait-for-device", stderr, stdout);
		} catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
			throw new TimeoutException ($"Timed out waiting for device after {effectiveTimeout.TotalSeconds}s.");
		}
	}

	public async Task StopEmulatorAsync (string serial, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace (serial))
			throw new ArgumentException ("Serial must not be empty.", nameof (serial));

		var psi = ProcessUtils.CreateProcessStartInfo (adbPath, "-s", serial, "emu", "kill");
		using var stderr = new StringWriter ();
		var exitCode = await ProcessUtils.StartProcess (psi, null, stderr, cancellationToken, environmentVariables).ConfigureAwait (false);
		ProcessUtils.ThrowIfFailed (exitCode, $"adb -s {serial} emu kill", stderr);
	}

	/// <summary>
	/// Gets a system property from a device via 'adb -s &lt;serial&gt; shell getprop &lt;property&gt;'.
	/// Returns the property value (first non-empty line of stdout), or <c>null</c> on failure.
	/// </summary>
	public virtual async Task<string?> GetShellPropertyAsync (string serial, string propertyName, CancellationToken cancellationToken = default)
	{
		using var stdout = new StringWriter ();
		using var stderr = new StringWriter ();
		var psi = ProcessUtils.CreateProcessStartInfo (adbPath, "-s", serial, "shell", "getprop", propertyName);
		var exitCode = await ProcessUtils.StartProcess (psi, stdout, stderr, cancellationToken, environmentVariables).ConfigureAwait (false);
		if (exitCode != 0) {
			var stderrText = stderr.ToString ().Trim ();
			if (stderrText.Length > 0)
				logger.Invoke (TraceLevel.Warning, $"adb shell getprop {propertyName} failed (exit {exitCode}): {stderrText}");
			return null;
		}
		return FirstNonEmptyLine (stdout.ToString ());
	}

	/// <summary>
	/// Runs a shell command on a device via 'adb -s &lt;serial&gt; shell &lt;command&gt;'.
	/// Returns the full stdout output trimmed, or <c>null</c> on failure.
	/// </summary>
	/// <remarks>
	/// The <paramref name="command"/> is passed as a single argument to <c>adb shell</c>,
	/// which means the device's shell interprets it (shell expansion, pipes, semicolons are active).
	/// Do not pass untrusted or user-supplied input without proper validation.
	/// </remarks>
	public virtual async Task<string?> RunShellCommandAsync (string serial, string command, CancellationToken cancellationToken)
	{
		using var stdout = new StringWriter ();
		using var stderr = new StringWriter ();
		var psi = ProcessUtils.CreateProcessStartInfo (adbPath, "-s", serial, "shell", command);
		var exitCode = await ProcessUtils.StartProcess (psi, stdout, stderr, cancellationToken, environmentVariables).ConfigureAwait (false);
		if (exitCode != 0) {
			var stderrText = stderr.ToString ().Trim ();
			if (stderrText.Length > 0)
				logger.Invoke (TraceLevel.Warning, $"adb shell {command} failed (exit {exitCode}): {stderrText}");
			return null;
		}
		var output = stdout.ToString ().Trim ();
		return output.Length > 0 ? output : null;
	}

	/// <summary>
	/// Runs a shell command on a device via <c>adb -s &lt;serial&gt; shell &lt;command&gt; &lt;args&gt;</c>.
	/// Returns the full stdout output trimmed, or <c>null</c> on failure.
	/// </summary>
	/// <remarks>
	/// When <c>adb shell</c> receives the command and arguments as separate tokens, it uses
	/// <c>exec()</c> directly on the device — bypassing the device's shell interpreter.
	/// This avoids shell expansion, pipes, and injection risks, making it safer for dynamic input.
	/// </remarks>
	public virtual async Task<string?> RunShellCommandAsync (string serial, string command, string[] args, CancellationToken cancellationToken = default)
	{
		using var stdout = new StringWriter ();
		using var stderr = new StringWriter ();
		// Build: adb -s <serial> shell <command> <arg1> <arg2> ...
		var allArgs = new string [3 + 1 + args.Length];
		allArgs [0] = "-s";
		allArgs [1] = serial;
		allArgs [2] = "shell";
		allArgs [3] = command;
		Array.Copy (args, 0, allArgs, 4, args.Length);
		var psi = ProcessUtils.CreateProcessStartInfo (adbPath, allArgs);
		var exitCode = await ProcessUtils.StartProcess (psi, stdout, stderr, cancellationToken, environmentVariables).ConfigureAwait (false);
		if (exitCode != 0) {
			var stderrText = stderr.ToString ().Trim ();
			if (stderrText.Length > 0)
				logger.Invoke (TraceLevel.Warning, $"adb shell {command} failed (exit {exitCode}): {stderrText}");
			return null;
		}
		var output = stdout.ToString ().Trim ();
		return output.Length > 0 ? output : null;
	}

	internal static string? FirstNonEmptyLine (string output)
	{
		foreach (var line in output.Split ('\n')) {
			var trimmed = line.Trim ();
			if (trimmed.Length > 0)
				return trimmed;
		}
		return null;
	}

	/// <summary>
	/// Parses the output lines from 'adb devices -l'.
	/// Accepts an <see cref="IEnumerable{T}"/> to avoid allocating a joined string.
	/// </summary>
	public static IReadOnlyList<AdbDeviceInfo> ParseAdbDevicesOutput (IEnumerable<string> lines)
	{
		var devices = new List<AdbDeviceInfo> ();

		foreach (var line in lines) {
			var trimmed = line.Trim ();
			if (string.IsNullOrEmpty (trimmed) ||
				trimmed.IndexOf ("List of devices", StringComparison.OrdinalIgnoreCase) >= 0 ||
				trimmed.StartsWith ("*", StringComparison.Ordinal))
				continue;

			var match = AdbDevicesRegex.Match (trimmed);
			if (!match.Success)
				continue;

			var serial = match.Groups [1].Value.Trim ();
			var state = match.Groups [2].Value.Trim ();
			var properties = match.Groups [3].Value.Trim ();

			// Parse key:value pairs from the properties string
			var propDict = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
			if (!string.IsNullOrEmpty (properties)) {
				var pairs = properties.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (var pair in pairs) {
					var colonIndex = pair.IndexOf (':');
					if (colonIndex > 0 && colonIndex < pair.Length - 1) {
						var key = pair.Substring (0, colonIndex);
						var value = pair.Substring (colonIndex + 1);
						propDict [key] = value;
					}
				}
			}

			var deviceType = serial.StartsWith ("emulator-", StringComparison.OrdinalIgnoreCase)
				? AdbDeviceType.Emulator
				: AdbDeviceType.Device;

			var device = new AdbDeviceInfo {
				Serial = serial,
				Type = deviceType,
				Status = MapAdbStateToStatus (state),
			};

			if (propDict.TryGetValue ("model", out var model))
				device.Model = model;
			if (propDict.TryGetValue ("product", out var product))
				device.Product = product;
			if (propDict.TryGetValue ("device", out var deviceCodeName))
				device.Device = deviceCodeName;
			if (propDict.TryGetValue ("transport_id", out var transportId))
				device.TransportId = transportId;

			// Build description (will be updated later if emulator AVD name is available)
			device.Description = BuildDeviceDescription (device);

			devices.Add (device);
		}

		return devices;
	}

	/// <summary>
	/// Maps adb device states to status values.
	/// Ported from dotnet/android GetAvailableAndroidDevices.MapAdbStateToStatus.
	/// </summary>
	public static AdbDeviceStatus MapAdbStateToStatus (string adbState) => adbState.ToLowerInvariant () switch {
		"device" => AdbDeviceStatus.Online,
		"offline" => AdbDeviceStatus.Offline,
		"unauthorized" => AdbDeviceStatus.Unauthorized,
		"no permissions" => AdbDeviceStatus.NoPermissions,
		_ => AdbDeviceStatus.Unknown,
	};

	/// <summary>
	/// Builds a human-friendly description for a device.
	/// Priority: AVD name (for emulators) > model > product > device > serial.
	/// Ported from dotnet/android GetAvailableAndroidDevices.BuildDeviceDescription.
	/// </summary>
	public static string BuildDeviceDescription (AdbDeviceInfo device, Action<TraceLevel, string>? logger = null)
	{
		logger ??= RunnerDefaults.NullLogger;
		if (device.Type == AdbDeviceType.Emulator && device.AvdName is { Length: > 0 } avdName) {
			logger.Invoke (TraceLevel.Verbose, $"Emulator {device.Serial}, original AVD name: {avdName}");
			var formatted = FormatDisplayName (avdName);
			logger.Invoke (TraceLevel.Verbose, $"Emulator {device.Serial}, formatted AVD display name: {formatted}");
			return formatted;
		}

		if (device.Model is { Length: > 0 } model)
			return model.Replace ('_', ' ');

		if (device.Product is { Length: > 0 } product)
			return product.Replace ('_', ' ');

		if (device.Device is { Length: > 0 } deviceName)
			return deviceName.Replace ('_', ' ');

		return device.Serial;
	}

	/// <summary>
	/// Formats an AVD name into a user-friendly display name.
	/// Replaces underscores with spaces, applies title case, and capitalizes "API".
	/// ToTitleCase naturally preserves fully-uppercase segments (e.g. "XL", "SE").
	/// Ported from dotnet/android GetAvailableAndroidDevices.FormatDisplayName.
	/// </summary>
	public static string FormatDisplayName (string avdName)
	{
		if (string.IsNullOrEmpty (avdName))
			return avdName ?? string.Empty;

		var textInfo = CultureInfo.InvariantCulture.TextInfo;
		avdName = textInfo.ToTitleCase (avdName.Replace ('_', ' '));

		// Replace "Api" with "API"
		avdName = ApiRegex.Replace (avdName, "API");
		return avdName;
	}

	/// <summary>
	/// Merges devices from adb with available emulators from 'emulator -list-avds'.
	/// Running emulators are not duplicated. Non-running emulators are added with Status=NotRunning.
	/// Ported from dotnet/android GetAvailableAndroidDevices.MergeDevicesAndEmulators.
	/// </summary>
	public static IReadOnlyList<AdbDeviceInfo> MergeDevicesAndEmulators (IReadOnlyList<AdbDeviceInfo> adbDevices, IReadOnlyList<string> availableEmulators, Action<TraceLevel, string>? logger = null)
	{
		logger ??= RunnerDefaults.NullLogger;
		var result = new List<AdbDeviceInfo> (adbDevices);

		// Build a set of AVD names that are already running
		var runningAvdNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		foreach (var device in adbDevices) {
			if (device.AvdName is { Length: > 0 } avdName)
				runningAvdNames.Add (avdName);
		}

		logger.Invoke (TraceLevel.Verbose, $"Running emulators AVD names: {string.Join (", ", runningAvdNames)}");

		// Add non-running emulators
		foreach (var avdName in availableEmulators) {
			if (runningAvdNames.Contains (avdName)) {
				logger.Invoke (TraceLevel.Verbose, $"Emulator '{avdName}' is already running, skipping");
				continue;
			}

			var displayName = FormatDisplayName (avdName);
			result.Add (new AdbDeviceInfo {
				Serial = avdName,
				Description = displayName + " (Not Running)",
				Type = AdbDeviceType.Emulator,
				Status = AdbDeviceStatus.NotRunning,
				AvdName = avdName,
			});
			logger.Invoke (TraceLevel.Verbose, $"Added non-running emulator: {avdName}");
		}

		// Sort: online devices first, then not-running emulators, alphabetically by description
		result.Sort ((a, b) => {
			var aNotRunning = a.Status == AdbDeviceStatus.NotRunning;
			var bNotRunning = b.Status == AdbDeviceStatus.NotRunning;

			if (aNotRunning != bNotRunning)
				return aNotRunning ? 1 : -1;

			return string.Compare (a.Description, b.Description, StringComparison.OrdinalIgnoreCase);
		});

		return result;
	}
}

