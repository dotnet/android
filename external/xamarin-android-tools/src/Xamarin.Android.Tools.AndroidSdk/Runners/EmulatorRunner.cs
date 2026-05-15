// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools;

/// <summary>
/// Runs Android Emulator commands.
/// </summary>
public class EmulatorRunner
{
	readonly string emulatorPath;
	readonly IDictionary<string, string>? environmentVariables;
	readonly Action<TraceLevel, string> logger;

	/// <summary>
	/// Creates a new EmulatorRunner with the full path to the emulator executable.
	/// </summary>
	/// <param name="emulatorPath">Full path to the emulator executable (e.g., "/path/to/sdk/emulator/emulator").</param>
	/// <param name="environmentVariables">Optional environment variables to pass to emulator processes.</param>
	/// <param name="logger">Optional logger callback for diagnostic messages.</param>
	public EmulatorRunner (string emulatorPath, IDictionary<string, string>? environmentVariables = null, Action<TraceLevel, string>? logger = null)
	{
		if (string.IsNullOrWhiteSpace (emulatorPath))
			throw new ArgumentException ("Path to emulator must not be empty.", nameof (emulatorPath));
		this.emulatorPath = emulatorPath;
		this.environmentVariables = environmentVariables;
		this.logger = logger ?? RunnerDefaults.NullLogger;
	}

	/// <summary>
	/// Launches an emulator process for the specified AVD and returns immediately.
	/// The returned <see cref="Process"/> represents the running emulator — the caller
	/// is responsible for managing its lifetime (e.g., killing it on shutdown).
	/// This method does <b>not</b> wait for the emulator to finish booting.
	/// To launch <i>and</i> wait until the device is fully booted, use <see cref="BootEmulatorAsync"/> instead.
	/// </summary>
	/// <param name="avdName">Name of the AVD to launch (as shown by <c>emulator -list-avds</c>).</param>
	/// <param name="coldBoot">When <c>true</c>, forces a cold boot by passing <c>-no-snapshot-load</c>.</param>
	/// <param name="additionalArgs">Optional extra arguments to pass to the emulator command line.</param>
	/// <returns>The <see cref="Process"/> running the emulator. Stdout/stderr are redirected and forwarded to the logger.</returns>
	public Process LaunchEmulator (string avdName, bool coldBoot = false, List<string>? additionalArgs = null)
	{
		if (string.IsNullOrWhiteSpace (avdName))
			throw new ArgumentException ("AVD name must not be empty.", nameof (avdName));

		var args = new List<string> { "-avd", avdName };
		if (coldBoot)
			args.Add ("-no-snapshot-load");
		if (additionalArgs != null)
			args.AddRange (additionalArgs);

		ProcessStartInfo psi;
		if (OS.IsWindows) {
			psi = ProcessUtils.CreateProcessStartInfo (emulatorPath, args.ToArray ());
		} else {
			// On Unix, launch through a shell that ignores SIGINT before exec'ing
			// the emulator. This prevents Ctrl+C in the parent terminal from killing
			// the emulator process. 'trap "" INT' sets SIGINT to SIG_IGN, which POSIX
			// guarantees is preserved across exec:
			// https://pubs.opengroup.org/onlinepubs/9699919799/functions/exec.html
			var shellCmd = new StringBuilder ("trap '' INT; exec ");
			shellCmd.Append (ShellQuote (emulatorPath));
			foreach (var arg in args) {
				shellCmd.Append (' ');
				shellCmd.Append (ShellQuote (arg));
			}
			psi = ProcessUtils.CreateProcessStartInfo ("/bin/sh", "-c", shellCmd.ToString ());
		}

		if (environmentVariables != null) {
			foreach (var kvp in environmentVariables)
				psi.EnvironmentVariables[kvp.Key] = kvp.Value;
		}

		// Redirect stdout/stderr so the emulator process doesn't inherit the
		// caller's pipes. Without this, parent processes (e.g. VS Code spawn)
		// never see the 'close' event because the emulator holds the pipes open.
		psi.RedirectStandardOutput = true;
		psi.RedirectStandardError = true;

		logger.Invoke (TraceLevel.Verbose, $"Launching emulator AVD '{avdName}'");

		var process = new Process { StartInfo = psi };

		// Forward emulator output to the logger so crash messages (e.g. "HAX is
		// not working", "image not found") are captured instead of silently lost.
		process.OutputDataReceived += (_, e) => {
			if (e.Data != null)
				logger.Invoke (TraceLevel.Verbose, $"[emulator] {e.Data}");
		};
		process.ErrorDataReceived += (_, e) => {
			if (e.Data != null)
				logger.Invoke (TraceLevel.Warning, $"[emulator] {e.Data}");
		};

		if (!process.Start ()) {
			process.Dispose ();
			throw new InvalidOperationException ($"Failed to start emulator process '{emulatorPath}'.");
		}

		// Drain redirected streams asynchronously to prevent pipe buffer deadlocks
		process.BeginOutputReadLine ();
		process.BeginErrorReadLine ();

		return process;
	}

	public async Task<IReadOnlyList<string>> ListAvdNamesAsync (CancellationToken cancellationToken = default)
	{
		using var stdout = new StringWriter ();
		using var stderr = new StringWriter ();
		var psi = ProcessUtils.CreateProcessStartInfo (emulatorPath, "-list-avds");

		logger.Invoke (TraceLevel.Verbose, "Running: emulator -list-avds");
		var exitCode = await ProcessUtils.StartProcess (psi, stdout, stderr, cancellationToken, environmentVariables).ConfigureAwait (false);
		ProcessUtils.ThrowIfFailed (exitCode, "emulator -list-avds", stderr, stdout);

		return ParseListAvdsOutput (stdout.ToString ());
	}

	internal static List<string> ParseListAvdsOutput (string output)
	{
		var avds = new List<string> ();
		foreach (var line in output.Split ('\n')) {
			var trimmed = line.Trim ();
			if (!string.IsNullOrEmpty (trimmed))
				avds.Add (trimmed);
		}
		return avds;
	}

	/// <summary>
	/// Boots an emulator for the specified AVD and waits until it is fully ready to accept commands.
	/// <para>
	/// Unlike <see cref="LaunchEmulator"/>, which only spawns the emulator process, this method
	/// handles the full lifecycle: it checks whether the device is already online, launches
	/// the emulator if needed, then polls <c>sys.boot_completed</c> and <c>pm path android</c>
	/// until the Android OS is fully booted and the package manager is responsive.
	/// </para>
	/// <para>Ported from the dotnet/android <c>BootAndroidEmulator</c> MSBuild task.</para>
	/// </summary>
	/// <param name="deviceOrAvdName">
	/// Either an ADB device serial (e.g., <c>emulator-5554</c>) to wait for,
	/// or an AVD name (e.g., <c>Pixel_7_API_35</c>) to launch and boot.
	/// </param>
	/// <param name="adbRunner">An <see cref="AdbRunner"/> used to query device status and boot properties.</param>
	/// <param name="options">Optional boot configuration (timeout, poll interval, cold boot, extra args).</param>
	/// <param name="cancellationToken">Cancellation token to abort the operation.</param>
	/// <returns>
	/// An <see cref="EmulatorBootResult"/> indicating success or failure, including the device serial on success
	/// or an error message on timeout/failure.
	/// </returns>
	public async Task<EmulatorBootResult> BootEmulatorAsync (
		string deviceOrAvdName,
		AdbRunner adbRunner,
		EmulatorBootOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace (deviceOrAvdName))
			throw new ArgumentException ("Device or AVD name must not be empty.", nameof (deviceOrAvdName));
		if (adbRunner == null)
			throw new ArgumentNullException (nameof (adbRunner));

		options ??= new EmulatorBootOptions ();
		if (options.BootTimeout <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException (nameof (options), "BootTimeout must be positive.");
		if (options.PollInterval <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException (nameof (options), "PollInterval must be positive.");

		logger.Invoke (TraceLevel.Info, $"Booting emulator for '{deviceOrAvdName}'...");

		// Phase 1: Check if deviceOrAvdName is already an online ADB device by serial
		var devices = await adbRunner.ListDevicesAsync (cancellationToken).ConfigureAwait (false);
		var onlineDevice = devices.FirstOrDefault (d =>
			d.Status == AdbDeviceStatus.Online &&
			string.Equals (d.Serial, deviceOrAvdName, StringComparison.OrdinalIgnoreCase));

		if (onlineDevice != null) {
			logger.Invoke (TraceLevel.Info, $"Device '{deviceOrAvdName}' is already online.");
			return new EmulatorBootResult { Success = true, Serial = onlineDevice.Serial, ErrorKind = EmulatorBootErrorKind.None };
		}

		// Single timeout CTS for the entire boot operation (covers Phase 2 and Phase 3).
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
		timeoutCts.CancelAfter (options.BootTimeout);

		// Phase 2: Check if AVD is already running (possibly still booting)
		var runningSerial = FindRunningAvdSerial (devices, deviceOrAvdName);
		if (runningSerial != null) {
			logger.Invoke (TraceLevel.Info, $"AVD '{deviceOrAvdName}' is already running as '{runningSerial}', waiting for full boot...");
			try {
				return await WaitForFullBootAsync (adbRunner, runningSerial, options, timeoutCts.Token).ConfigureAwait (false);
			} catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
				return new EmulatorBootResult {
					Success = false,
					ErrorKind = EmulatorBootErrorKind.Timeout,
					ErrorMessage = $"Timed out waiting for emulator '{deviceOrAvdName}' to boot within {options.BootTimeout.TotalSeconds}s.",
				};
			}
		}

		// Phase 3: Launch the emulator
		logger.Invoke (TraceLevel.Info, $"Launching AVD '{deviceOrAvdName}'...");
		Process emulatorProcess;
		try {
			emulatorProcess = LaunchEmulator (deviceOrAvdName, options.ColdBoot, options.AdditionalArgs);
		} catch (Exception ex) {
			return new EmulatorBootResult {
				Success = false,
				ErrorKind = EmulatorBootErrorKind.LaunchFailed,
				ErrorMessage = $"Failed to launch emulator: {ex.Message}",
			};
		}

		// Poll for the new emulator serial to appear.
		// If the boot times out or is cancelled, terminate the process we spawned
		// to avoid leaving orphan emulator processes.
		//
		// On macOS, the emulator binary may fork the real QEMU process and exit with
		// code 0 immediately. The real emulator continues as a separate process and
		// will eventually appear in 'adb devices'. We only treat non-zero exit codes
		// as immediate failures; exit code 0 means we continue polling.
		//
		// Dispose the Process handle when done — the emulator process keeps running.
		using (emulatorProcess) {
			try {
				string? newSerial = null;
				bool processExitedWithZero = false;
				while (newSerial == null) {
					timeoutCts.Token.ThrowIfCancellationRequested ();

					// Detect early process exit for fast failure.
					// Guard against InvalidOperationException in case no OS process
					// is associated with the object (e.g. broken emulator binary).
					try {
						if (emulatorProcess.HasExited && !processExitedWithZero) {
							if (emulatorProcess.ExitCode != 0) {
								return new EmulatorBootResult {
									Success = false,
									ErrorKind = EmulatorBootErrorKind.LaunchFailed,
									ErrorMessage = $"Emulator process for '{deviceOrAvdName}' exited with code {emulatorProcess.ExitCode} before becoming available.",
								};
							}
							// Exit code 0: emulator likely forked (common on macOS).
							// The real emulator runs as a separate process — keep polling.
							logger.Invoke (TraceLevel.Verbose, $"Emulator launcher process exited with code 0 (likely forked). Continuing to poll adb devices.");
							processExitedWithZero = true;
						}
					} catch (InvalidOperationException ex) {
						return new EmulatorBootResult {
							Success = false,
							ErrorKind = EmulatorBootErrorKind.LaunchFailed,
							ErrorMessage = $"Emulator process for '{deviceOrAvdName}' is no longer available: {ex.Message}",
						};
					}

					await Task.Delay (options.PollInterval, timeoutCts.Token).ConfigureAwait (false);

					devices = await adbRunner.ListDevicesAsync (timeoutCts.Token).ConfigureAwait (false);
					newSerial = FindRunningAvdSerial (devices, deviceOrAvdName);
				}

				logger.Invoke (TraceLevel.Info, $"Emulator appeared as '{newSerial}', waiting for full boot...");
				return await WaitForFullBootAsync (adbRunner, newSerial, options, timeoutCts.Token).ConfigureAwait (false);
			} catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
				TryKillProcess (emulatorProcess);
				return new EmulatorBootResult {
					Success = false,
					ErrorKind = EmulatorBootErrorKind.Timeout,
					ErrorMessage = $"Timed out waiting for emulator '{deviceOrAvdName}' to boot within {options.BootTimeout.TotalSeconds}s.",
				};
			} catch {
				TryKillProcess (emulatorProcess);
				throw;
			}
		}
	}

	static string? FindRunningAvdSerial (IReadOnlyList<AdbDeviceInfo> devices, string avdName)
	{
		foreach (var d in devices) {
			if (d.Type == AdbDeviceType.Emulator &&
				!string.IsNullOrEmpty (d.AvdName) &&
				string.Equals (d.AvdName, avdName, StringComparison.OrdinalIgnoreCase)) {
				return d.Serial;
			}
		}
		return null;
	}

	void TryKillProcess (Process process)
	{
		try {
			process.Kill ();
		} catch (Exception ex) {
			// Best-effort: process may have already exited
			logger.Invoke (TraceLevel.Verbose, $"Failed to stop emulator process: {ex.Message}");
		}
	}

	async Task<EmulatorBootResult> WaitForFullBootAsync (
		AdbRunner adbRunner,
		string serial,
		EmulatorBootOptions options,
		CancellationToken cancellationToken)
	{
		// The caller is responsible for enforcing the overall boot timeout via
		// cancellationToken (a linked CTS with CancelAfter). This method simply
		// polls until boot completes or the token is cancelled.
		while (!cancellationToken.IsCancellationRequested) {
			var bootCompleted = await adbRunner.GetShellPropertyAsync (serial, "sys.boot_completed", cancellationToken).ConfigureAwait (false);
			if (string.Equals (bootCompleted, "1", StringComparison.Ordinal)) {
				var pmResult = await adbRunner.RunShellCommandAsync (serial, "pm path android", cancellationToken).ConfigureAwait (false);
				if (pmResult != null && pmResult.StartsWith ("package:", StringComparison.Ordinal)) {
					logger.Invoke (TraceLevel.Info, $"Emulator '{serial}' is fully booted.");
					return new EmulatorBootResult { Success = true, Serial = serial, ErrorKind = EmulatorBootErrorKind.None };
				}
			}

			await Task.Delay (options.PollInterval, cancellationToken).ConfigureAwait (false);
		}

		cancellationToken.ThrowIfCancellationRequested ();
		return new EmulatorBootResult { Success = false, ErrorKind = EmulatorBootErrorKind.Cancelled, ErrorMessage = "Boot cancelled." };
	}

	/// Quotes a string for safe use in a POSIX shell command.
	/// Wraps in single quotes and escapes embedded single quotes.
	static string ShellQuote (string arg) => "'" + arg.Replace ("'", "'\\''") + "'";
}

