#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// MSBuild task that ensures an Android device identified by $(Device) is online and ready.
///
/// $(Device) may be either:
///   - An ADB serial for an already-running device/emulator (e.g. "emulator-5554", "0A041FDD400327")
///   - An AVD name for a not-yet-booted emulator (e.g. "Pixel_6_API_33")
///
/// The task detects which case applies:
///   - If $(Device) is already an online ADB serial, it passes through unchanged.
///   - If $(Device) is an AVD name (not found as a serial in 'adb devices'), the task boots
///     the emulator and waits for it to become fully ready.
///
/// On success, outputs the resolved ADB serial and AdbTarget for use by subsequent tasks.
/// </summary>
public class BootAndroidEmulator : AndroidTask
{
	const int DefaultBootTimeoutSeconds = 300;
	const int PollIntervalMilliseconds = 500;

	public override string TaskPrefix => "BAE";

	/// <summary>
	/// The device identifier from 'dotnet run' device selection. May be an ADB serial
	/// (e.g. "emulator-5554") for an already-running device, or an AVD name
	/// (e.g. "Pixel_6_API_33") for a not-running emulator.
	/// </summary>
	[Required]
	public string Device { get; set; } = "";

	/// <summary>
	/// Path to the Android SDK directory (e.g., "/usr/local/lib/android/sdk/").
	/// Used to compute default tool paths when EmulatorToolPath/AdbToolPath are not set.
	/// </summary>
	public string? AndroidSdkDirectory { get; set; }

	/// <summary>
	/// Path to the emulator tool directory.
	/// Defaults to $(AndroidSdkDirectory)/emulator/ if not set.
	/// </summary>
	public string? EmulatorToolPath { get; set; }

	/// <summary>
	/// Filename of the emulator executable (e.g., "emulator" or "emulator.exe").
	/// </summary>
	public string? EmulatorToolExe { get; set; }

	/// <summary>
	/// Path to the adb tool directory.
	/// Defaults to $(AndroidSdkDirectory)/platform-tools/ if not set.
	/// </summary>
	public string? AdbToolPath { get; set; }

	/// <summary>
	/// Filename of the adb executable (e.g., "adb" or "adb.exe").
	/// </summary>
	public string? AdbToolExe { get; set; }

	/// <summary>
	/// Maximum time in seconds to wait for the emulator to fully boot.
	/// Defaults to 300 seconds (5 minutes).
	/// </summary>
	public int BootTimeoutSeconds { get; set; } = DefaultBootTimeoutSeconds;

	/// <summary>
	/// Optional additional arguments to pass to the emulator command line (e.g. "-no-snapshot-load -gpu auto").
	/// </summary>
	public string? EmulatorExtraArguments { get; set; }

	/// <summary>
	/// The resolved ADB serial of the device (e.g. "emulator-5554").
	/// For already-running devices this equals DeviceId; for booted emulators this is the new serial.
	/// </summary>
	[Output]
	public string? ResolvedDevice { get; set; }

	/// <summary>
	/// The ADB target argument for use by subsequent tasks (e.g. "-s emulator-5554").
	/// </summary>
	[Output]
	public string? AdbTarget { get; set; }

	public override bool RunTask ()
	{
		var adbPath = ResolveAdbPath ();

		// Check if DeviceId is already a known online ADB serial
		if (IsOnlineAdbDevice (adbPath, Device)) {
			Log.LogMessage (MessageImportance.Normal, $"Device '{Device}' is already online.");
			ResolvedDevice = Device;
			AdbTarget = $"-s {Device}";
			return true;
		}

		// DeviceId is not an online serial — treat it as an AVD name and boot it
		Log.LogMessage (MessageImportance.Normal, $"Device '{Device}' is not an online ADB device. Treating as AVD name.");

		var emulatorPath = ResolveEmulatorPath ();
		var avdName = Device;

		// Check if this AVD is already running (but perhaps still booting)
		var existingSerial = FindRunningEmulatorForAvd (adbPath, avdName);
		if (existingSerial != null) {
			Log.LogMessage (MessageImportance.High, $"Emulator '{avdName}' is already running as '{existingSerial}'");
			ResolvedDevice = existingSerial;
			AdbTarget = $"-s {existingSerial}";
			return WaitForFullBoot (adbPath, avdName, existingSerial);
		}

		// Launch the emulator process in the background
		Log.LogMessage (MessageImportance.High, $"Booting emulator '{avdName}'...");
		using var emulatorProcess = LaunchEmulatorProcess (emulatorPath, avdName);
		if (emulatorProcess == null) {
			return false;
		}

		try {
			var timeout = TimeSpan.FromSeconds (BootTimeoutSeconds);
			var stopwatch = Stopwatch.StartNew ();

			// Phase 1: Wait for the emulator to appear in 'adb devices' as online
			Log.LogMessage (MessageImportance.Normal, "Waiting for emulator to appear in adb devices...");
			var serial = WaitForEmulatorOnline (adbPath, avdName, emulatorProcess, stopwatch, timeout);
			if (serial == null) {
				if (emulatorProcess.HasExited) {
					Log.LogCodedError ("XA0144", Properties.Resources.XA0144, avdName, emulatorProcess.ExitCode);
				} else {
					Log.LogCodedError ("XA0145", Properties.Resources.XA0145, avdName, BootTimeoutSeconds);
				}
				return false;
			}

			ResolvedDevice = serial;
			AdbTarget = $"-s {serial}";
			Log.LogMessage (MessageImportance.Normal, $"Emulator appeared as '{serial}'");

			// Phase 2: Wait for the device to fully boot
			return WaitForFullBoot (adbPath, avdName, serial);
		} finally {
			// Stop async reads and unsubscribe events; using var handles Dispose
			try {
				emulatorProcess.CancelOutputRead ();
				emulatorProcess.CancelErrorRead ();
			} catch (InvalidOperationException e) {
				// Async reads may not have been started or process already exited
				Log.LogDebugMessage ($"Failed to cancel async reads: {e}");
			}
			emulatorProcess.OutputDataReceived -= EmulatorOutputDataReceived;
			emulatorProcess.ErrorDataReceived -= EmulatorErrorDataReceived;
		}
	}

	/// <summary>
	/// Resolves the full path to the adb executable, using AdbToolPath/AdbToolExe
	/// if set, otherwise computing defaults from AndroidSdkDirectory.
	/// </summary>
	string ResolveAdbPath ()
	{
		var exe = AdbToolExe.IsNullOrEmpty () ? (OS.IsWindows ? "adb.exe" : "adb") : AdbToolExe;
		var dir = AdbToolPath;

		if (dir.IsNullOrEmpty () && !AndroidSdkDirectory.IsNullOrEmpty ()) {
			dir = Path.Combine (AndroidSdkDirectory, "platform-tools");
		}

		return dir.IsNullOrEmpty () ? exe : Path.Combine (dir, exe);
	}

	/// <summary>
	/// Resolves the full path to the emulator executable, using EmulatorToolPath/EmulatorToolExe
	/// if set, otherwise computing defaults from AndroidSdkDirectory.
	/// </summary>
	string ResolveEmulatorPath ()
	{
		var exe = EmulatorToolExe.IsNullOrEmpty () ? (OS.IsWindows ? "emulator.exe" : "emulator") : EmulatorToolExe;
		var dir = EmulatorToolPath;

		if (dir.IsNullOrEmpty () && !AndroidSdkDirectory.IsNullOrEmpty ()) {
			dir = Path.Combine (AndroidSdkDirectory, "emulator");
		}

		return dir.IsNullOrEmpty () ? exe : Path.Combine (dir, exe);
	}

	/// <summary>
	/// Checks whether the given deviceId is currently listed as an online device in 'adb devices'.
	/// </summary>
	protected virtual bool IsOnlineAdbDevice (string adbPath, string deviceId)
	{
		bool found = false;

		MonoAndroidHelper.RunProcess (
			adbPath, "devices",
			Log,
			onOutput: (sender, e) => {
				if (e.Data != null && e.Data.Contains ("device") && !e.Data.Contains ("List of devices")) {
					var parts = e.Data.Split (['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length >= 2 && parts [1] == "device" &&
					    string.Equals (parts [0], deviceId, StringComparison.OrdinalIgnoreCase)) {
						found = true;
					}
				}
			},
			logWarningOnFailure: false
		);

		return found;
	}

	/// <summary>
	/// Checks if an emulator with the specified AVD name is already running by querying
	/// 'adb devices' and then 'adb -s serial emu avd name' for each running emulator.
	/// </summary>
	protected virtual string? FindRunningEmulatorForAvd (string adbPath, string avdName)
	{
		var emulatorSerials = new List<string> ();

		MonoAndroidHelper.RunProcess (
			adbPath, "devices",
			Log,
			onOutput: (sender, e) => {
				if (e.Data != null && e.Data.StartsWith ("emulator-", StringComparison.OrdinalIgnoreCase) && e.Data.Contains ("device")) {
					var parts = e.Data.Split (['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length >= 2 && parts [1] == "device") {
						emulatorSerials.Add (parts [0]);
					}
				}
			},
			logWarningOnFailure: false
		);

		foreach (var serial in emulatorSerials) {
			var name = GetRunningAvdName (adbPath, serial);
			if (string.Equals (name, avdName, StringComparison.OrdinalIgnoreCase)) {
				return serial;
			}
		}

		return null;
	}

	/// <summary>
	/// Gets the AVD name from a running emulator via 'adb -s serial emu avd name'.
	/// </summary>
	protected virtual string? GetRunningAvdName (string adbPath, string serial)
	{
		string? avdName = null;
		try {
			var outputLines = new List<string> ();
			MonoAndroidHelper.RunProcess (
				adbPath, $"-s {serial} emu avd name",
				Log,
				onOutput: (sender, e) => {
					if (!e.Data.IsNullOrEmpty ()) {
						outputLines.Add (e.Data);
					}
				},
				logWarningOnFailure: false
			);

			if (outputLines.Count > 0) {
				var name = outputLines [0].Trim ();
				if (!name.IsNullOrEmpty () && !name.Equals ("OK", StringComparison.OrdinalIgnoreCase)) {
					avdName = name;
				}
			}
		} catch (Exception ex) {
			Log.LogDebugMessage ($"Failed to get AVD name for {serial}: {ex.Message}");
		}

		return avdName;
	}

	/// <summary>
	/// Launches the emulator process in the background. The emulator window is shown by default,
	/// but this can be customized (for example, by passing -no-window) via EmulatorExtraArguments.
	/// </summary>
	protected virtual Process? LaunchEmulatorProcess (string emulatorPath, string avdName)
	{
		var arguments = $"-avd \"{avdName}\"";
		if (!EmulatorExtraArguments.IsNullOrEmpty ()) {
			arguments += $" {EmulatorExtraArguments}";
		}

		Log.LogMessage (MessageImportance.Normal, $"Starting: {emulatorPath} {arguments}");

		try {
			var psi = new ProcessStartInfo {
				FileName = emulatorPath,
				Arguments = arguments,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
			};

			var process = new Process { StartInfo = psi };

			// Capture output for diagnostics but don't block on it
			process.OutputDataReceived += EmulatorOutputDataReceived;
			process.ErrorDataReceived += EmulatorErrorDataReceived;

			process.Start ();
			process.BeginOutputReadLine ();
			process.BeginErrorReadLine ();

			return process;
		} catch (Exception ex) {
			Log.LogCodedError ("XA0143", Properties.Resources.XA0143, avdName, ex.Message);
			return null;
		}
	}

	void EmulatorOutputDataReceived (object sender, DataReceivedEventArgs e)
	{
		if (e.Data != null) {
			Log.LogDebugMessage ($"emulator stdout: {e.Data}");
		}
	}

	void EmulatorErrorDataReceived (object sender, DataReceivedEventArgs e)
	{
		if (e.Data != null) {
			Log.LogDebugMessage ($"emulator stderr: {e.Data}");
		}
	}

	/// <summary>
	/// Polls 'adb devices' until a new emulator serial appears with state "device" (online).
	/// Returns the serial or null on timeout / emulator process exit.
	/// </summary>
	string? WaitForEmulatorOnline (string adbPath, string avdName, Process emulatorProcess, Stopwatch stopwatch, TimeSpan timeout)
	{
		while (stopwatch.Elapsed < timeout) {
			if (emulatorProcess.HasExited) {
				return null;
			}

			var serial = FindRunningEmulatorForAvd (adbPath, avdName);
			if (serial != null) {
				return serial;
			}

			Thread.Sleep (PollIntervalMilliseconds);
		}

		return null;
	}

	/// <summary>
	/// Waits for the emulator to fully boot by checking:
	/// 1. sys.boot_completed property equals "1"
	/// 2. Package manager is responsive (pm path android returns "package:")
	/// </summary>
	bool WaitForFullBoot (string adbPath, string avdName, string serial)
	{
		Log.LogMessage (MessageImportance.Normal, "Waiting for emulator to fully boot...");
		var stopwatch = Stopwatch.StartNew ();
		var timeout = TimeSpan.FromSeconds (BootTimeoutSeconds);

		// Phase 1: Wait for sys.boot_completed == 1
		while (stopwatch.Elapsed < timeout) {
			var bootCompleted = GetShellProperty (adbPath, serial, "sys.boot_completed");
			if (bootCompleted == "1") {
				Log.LogMessage (MessageImportance.Normal, "sys.boot_completed = 1");
				break;
			}

			Thread.Sleep (PollIntervalMilliseconds);
		}

		if (stopwatch.Elapsed >= timeout) {
			Log.LogCodedError ("XA0145", Properties.Resources.XA0145, avdName, BootTimeoutSeconds);
			return false;
		}

		var remaining = timeout - stopwatch.Elapsed;
		Log.LogMessage (MessageImportance.Normal, $"Phase 1 complete. {remaining.TotalSeconds:F0}s remaining for package manager.");

		// Phase 2: Wait for package manager to be responsive
		while (stopwatch.Elapsed < timeout) {
			var pmResult = RunShellCommand (adbPath, serial, "pm path android");
			if (pmResult != null && pmResult.StartsWith ("package:", StringComparison.OrdinalIgnoreCase)) {
				Log.LogMessage (MessageImportance.High, $"Emulator '{avdName}' ({serial}) is fully booted and ready.");
				return true;
			}

			Thread.Sleep (PollIntervalMilliseconds);
		}

		Log.LogCodedError ("XA0145", Properties.Resources.XA0145, avdName, BootTimeoutSeconds);
		return false;
	}

	/// <summary>
	/// Gets a system property from the device via 'adb -s serial shell getprop property'.
	/// </summary>
	protected virtual string? GetShellProperty (string adbPath, string serial, string propertyName)
	{
		string? value = null;
		try {
			MonoAndroidHelper.RunProcess (
				adbPath, $"-s {serial} shell getprop {propertyName}",
				Log,
				onOutput: (sender, e) => {
					if (!e.Data.IsNullOrEmpty ()) {
						value = e.Data.Trim ();
					}
				},
				logWarningOnFailure: false
			);
		} catch (Exception ex) {
			Log.LogDebugMessage ($"Failed to get property '{propertyName}' from {serial}: {ex.Message}");
		}

		return value;
	}

	/// <summary>
	/// Runs a shell command on the device and returns the first line of output.
	/// </summary>
	protected virtual string? RunShellCommand (string adbPath, string serial, string command)
	{
		string? result = null;
		try {
			MonoAndroidHelper.RunProcess (
				adbPath, $"-s {serial} shell {command}",
				Log,
				onOutput: (sender, e) => {
					if (result == null && !e.Data.IsNullOrEmpty ()) {
						result = e.Data.Trim ();
					}
				},
				logWarningOnFailure: false
			);
		} catch (Exception ex) {
			Log.LogDebugMessage ($"Failed to run shell command '{command}' on {serial}: {ex.Message}");
		}

		return result;
	}
}
