#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
///
/// Boot logic is delegated to <see cref="EmulatorRunner.BootEmulatorAsync"/> and
/// <see cref="AdbRunner"/> in Xamarin.Android.Tools.AndroidSdk.
/// </summary>
public class BootAndroidEmulator : AsyncTask
{
	const int DefaultBootTimeoutSeconds = 300;

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

	public override async Task RunTaskAsync ()
	{
		if (BootTimeoutSeconds <= 0) {
			LogCodedError ("XA0145", Properties.Resources.XA0145, Device, BootTimeoutSeconds);
			return;
		}

		var adbPath = ResolveAdbPath ();
		var emulatorPath = ResolveEmulatorPath ();
		var logger = this.CreateTaskLogger ();

		var options = new EmulatorBootOptions {
			BootTimeout = TimeSpan.FromSeconds (BootTimeoutSeconds),
			AdditionalArgs = ParseExtraArguments (EmulatorExtraArguments),
		};

		var result = await ExecuteBootAsync (adbPath, emulatorPath, logger, Device, options, CancellationToken).ConfigureAwait (false);

		if (result.Success) {
			if (result.Serial.IsNullOrEmpty ()) {
				LogCodedError ("XA0143", Properties.Resources.XA0143, Device, "Boot reported success but no device serial was returned.");
				return;
			}
			ResolvedDevice = result.Serial;
			AdbTarget = $"-s {result.Serial}";
			LogMessage ($"Emulator '{Device}' ({result.Serial}) is fully booted and ready.");
			return;
		}

		switch (result.ErrorKind) {
		case EmulatorBootErrorKind.LaunchFailed:
			LogCodedError ("XA0143", Properties.Resources.XA0143, Device, result.ErrorMessage ?? "Unknown launch error");
			break;
		case EmulatorBootErrorKind.Cancelled:
			LogMessage ($"Emulator boot for '{Device}' was cancelled.");
			break;
		case EmulatorBootErrorKind.Timeout:
			LogCodedError ("XA0145", Properties.Resources.XA0145, Device, BootTimeoutSeconds);
			break;
		default:
			LogCodedError ("XA0144", Properties.Resources.XA0144, Device, result.ErrorKind, result.ErrorMessage ?? "Unknown error");
			break;
		}
	}

	/// <summary>
	/// Executes the full boot flow via <see cref="EmulatorRunner.BootEmulatorAsync"/>.
	/// Virtual so tests can return canned results without launching real processes.
	/// </summary>
	protected virtual async Task<EmulatorBootResult> ExecuteBootAsync (
		string adbPath,
		string emulatorPath,
		Action<TraceLevel, string> logger,
		string device,
		EmulatorBootOptions options,
		System.Threading.CancellationToken cancellationToken)
	{
		var adbRunner = new AdbRunner (adbPath, logger: logger);
		var emulatorRunner = new EmulatorRunner (emulatorPath, logger: logger);
		return await emulatorRunner.BootEmulatorAsync (device, adbRunner, options, cancellationToken).ConfigureAwait (false);
	}

	/// <summary>
	/// Parses extra arguments into a list suitable for <see cref="EmulatorBootOptions.AdditionalArgs"/>.
	/// Supports double-quoted segments to allow values with embedded spaces (e.g. <c>-gpu "swiftshader_indirect"</c>).
	/// Backslash-escaped quotes (<c>\"</c>) inside quoted values are preserved as literal quote characters.
	/// </summary>
	static List<string>? ParseExtraArguments (string? extraArgs)
	{
		if (extraArgs.IsNullOrEmpty ())
			return null;

		var args = new List<string> ();
		var current = new System.Text.StringBuilder ();
		bool inQuotes = false;

		for (int i = 0; i < extraArgs.Length; i++) {
			char c = extraArgs [i];

			if (c == '\\' && i + 1 < extraArgs.Length && extraArgs [i + 1] == '"') {
				current.Append ('"');
				i++;
			} else if (c == '"') {
				inQuotes = !inQuotes;
			} else if (char.IsWhiteSpace (c) && !inQuotes) {
				if (current.Length > 0) {
					args.Add (current.ToString ());
					current.Clear ();
				}
			} else {
				current.Append (c);
			}
		}

		if (current.Length > 0)
			args.Add (current.ToString ());

		return args.Count > 0 ? args : null;
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
}
