#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// MSBuild task that queries available Android devices and emulators using 'adb devices -l'
/// and 'emulator -list-avds'. Merges the results and filters them for device selection:
/// when any online devices exist, only online devices are returned (enabling auto-selection
/// when a single device is running). When no online devices exist, all devices are returned
/// including non-running emulators (allowing the user to pick one to boot).
/// Returns a list of devices with metadata for device selection in dotnet run.
///
/// Parsing and merging logic is delegated to <see cref="AdbRunner"/> in Xamarin.Android.Tools.AndroidSdk.
/// </summary>
public class GetAvailableAndroidDevices : AndroidToolTask
{
    readonly List<string> output = [];

    public override string TaskPrefix => "GAAD";

    protected override string ToolName => OS.IsWindows ? "adb.exe" : "adb";

    protected override string GenerateFullPathToTool () => Path.Combine (ToolPath, ToolExe);

    protected override string GenerateCommandLineCommands () => "devices -l";

    /// <summary>
    /// Path to the emulator tool directory.
    /// </summary>
    public string EmulatorToolPath { get; set; } = "";

    /// <summary>
    /// Filename of the emulator executable (e.g., "emulator" or "emulator.exe").
    /// </summary>
    public string EmulatorToolExe { get; set; } = "";

    [Output]
    public ITaskItem [] Devices { get; set; } = [];

    protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
    {
        base.LogEventsFromTextOutput (singleLine, messageImportance);
        output.Add (singleLine);
    }

    protected override void LogToolCommand (string message) => Log.LogDebugMessage (message);

    public override bool RunTask ()
    {
        if (!base.RunTask ())
            return false;

        // Parse devices from adb using shared AdbRunner logic
        var adbDevices = AdbRunner.ParseAdbDevicesOutput (output);
        Log.LogDebugMessage ($"Found {adbDevices.Count} device(s) from adb");

        // For emulators, query AVD names
        var logger = this.CreateTaskLogger ();
        foreach (var device in adbDevices) {
            if (device.Type == AdbDeviceType.Emulator) {
                device.AvdName = GetEmulatorAvdName (device.Serial);
                device.Description = AdbRunner.BuildDeviceDescription (device, logger);
            }
        }

        // Get available emulators from 'emulator -list-avds'
        var availableEmulators = GetAvailableEmulators ();
        Log.LogDebugMessage ($"Found {availableEmulators.Count} available emulator(s) from 'emulator -list-avds'");

        // Merge using shared logic
        var mergedDevices = AdbRunner.MergeDevicesAndEmulators (adbDevices, availableEmulators, logger);

        // Filter: if any online devices exist, return only those so auto-selection works
        // when a single device is running. If none are online, return all (including
        // non-running emulators) so the user can pick one to boot.
        var filteredDevices = FilterDevicesForSelection (mergedDevices);
        Log.LogDebugMessage ($"Filtered from {mergedDevices.Count} to {filteredDevices.Count} device(s) (online devices take priority)");

        // Convert to ITaskItem array
        Devices = ConvertToTaskItems (filteredDevices);

        Log.LogDebugMessage ($"Total {Devices.Length} Android device(s)/emulator(s) after filtering");

        return !Log.HasLoggedErrors;
    }

    /// <summary>
    /// Filters the merged device list for device selection:
    /// - If any online devices exist, returns only those (so auto-selection works with a single running device)
    /// - If no online devices exist, returns all (including non-running emulators for user selection)
    /// </summary>
    internal static IReadOnlyList<AdbDeviceInfo> FilterDevicesForSelection (IReadOnlyList<AdbDeviceInfo> devices)
    {
        var onlineDevices = new List<AdbDeviceInfo> (devices.Count);
        foreach (var device in devices) {
            if (device.Status == AdbDeviceStatus.Online) {
                onlineDevices.Add (device);
            }
        }

        if (onlineDevices.Count == 0)
            return devices;

        return onlineDevices;
    }

    /// <summary>
    /// Converts AdbDeviceInfo list to ITaskItem array for MSBuild output.
    /// </summary>
    internal static ITaskItem [] ConvertToTaskItems (IReadOnlyList<AdbDeviceInfo> devices)
    {
        var items = new ITaskItem [devices.Count];
        for (int i = 0; i < devices.Count; i++) {
            var device = devices [i];
            var item = new TaskItem (device.Serial);
            item.SetMetadata ("Description", device.Description);
            item.SetMetadata ("Type", device.Type.ToString ());
            item.SetMetadata ("Status", device.Status.ToString ());

            if (!device.AvdName.IsNullOrEmpty ())
                item.SetMetadata ("AvdName", device.AvdName);
            if (!device.Model.IsNullOrEmpty ())
                item.SetMetadata ("Model", device.Model);
            if (!device.Product.IsNullOrEmpty ())
                item.SetMetadata ("Product", device.Product);
            if (!device.Device.IsNullOrEmpty ())
                item.SetMetadata ("Device", device.Device);
            if (!device.TransportId.IsNullOrEmpty ())
                item.SetMetadata ("TransportId", device.TransportId);

            items [i] = item;
        }
        return items;
    }

    /// <summary>
    /// Gets the list of available AVDs using 'emulator -list-avds'.
    /// </summary>
    protected virtual List<string> GetAvailableEmulators ()
    {
        var emulators = new List<string> ();

        if (EmulatorToolPath.IsNullOrEmpty () || EmulatorToolExe.IsNullOrEmpty ()) {
            Log.LogDebugMessage ("EmulatorToolPath or EmulatorToolExe not set, skipping emulator listing");
            return emulators;
        }

        var emulatorPath = Path.Combine (EmulatorToolPath, EmulatorToolExe);
        if (!File.Exists (emulatorPath)) {
            Log.LogDebugMessage ($"Emulator tool not found at: {emulatorPath}");
            return emulators;
        }

        try {
            Log.LogDebugMessage ($"Running process: {emulatorPath} -list-avds");
            var startInfo = new ProcessStartInfo (emulatorPath) {
                ArgumentList = { "-list-avds" },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var process = new Process { StartInfo = startInfo };
            process.Start ();
            var elapsed = Stopwatch.StartNew ();
            try {
                foreach (var line in process.ReadAllLines (TimeSpan.FromSeconds (30))) {
                    if (!line.StandardError && !line.Content.IsNullOrWhiteSpace ()) {
                        var avdName = line.Content.Trim ();
                        emulators.Add (avdName);
                        Log.LogDebugMessage ($"Found available emulator: {avdName}");
                    }
                }
                var remaining = TimeSpan.FromSeconds (30) - elapsed.Elapsed;
                if (!process.SafeHandle.TryWaitForExit (remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, out var status))
                    throw new TimeoutException ($"Timed out running '{emulatorPath} -list-avds'.");
                Log.LogDebugMessage ($"Running process: exit code == {status.ExitCode}");
                if (status.ExitCode != 0)
                    Log.LogDebugMessage ($"'emulator -list-avds' returned exit code: {status.ExitCode}");
            } finally {
                using var stdout = process.StandardOutput;
                using var stderr = process.StandardError;
                if (!process.HasExited) {
                    try {
                        process.Kill (entireProcessTree: true);
                    } catch (InvalidOperationException) when (process.HasExited) {
                        // The process can exit between HasExited and Kill.
                    }
                    if (!process.SafeHandle.TryWaitForExit (TimeSpan.FromSeconds (5), out _))
                        throw new TimeoutException ($"Emulator listing process did not exit after being killed: {emulatorPath}");
                }
            }
        } catch (Exception ex) {
            Log.LogDebugMessage ($"Failed to run 'emulator -list-avds': {ex.Message}");
        }

        return emulators;
    }

    /// <summary>
    /// Queries the emulator for its AVD name using 'adb -s &lt;serial&gt; emu avd name'.
    /// Returns the raw AVD name (not formatted).
    /// </summary>
    protected virtual string? GetEmulatorAvdName (string serial)
    {
        try {
            var adbPath = Path.Combine (ToolPath, ToolExe);
            Log.LogDebugMessage ($"Running process: {adbPath} -s {serial} emu avd name");
            using var process = new Process { StartInfo = new ProcessStartInfo (adbPath) {
                ArgumentList = { "-s", serial, "emu", "avd", "name" },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            } };
            var elapsed = Stopwatch.StartNew ();
            process.Start ();
            ProcessTextOutput result;
            try {
                var text = process.ReadAllText (TimeSpan.FromSeconds (30));
                var remaining = TimeSpan.FromSeconds (30) - elapsed.Elapsed;
                if (!process.SafeHandle.TryWaitForExit (remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, out var status))
                    throw new TimeoutException ($"Timed out retrieving the AVD name for '{serial}'.");
                result = new ProcessTextOutput (status, text.StandardOutput, text.StandardError, process.Id);
            } finally {
                using var stdout = process.StandardOutput;
                using var stderr = process.StandardError;
                if (!process.HasExited) {
                    try {
                        process.Kill (entireProcessTree: true);
                    } catch (InvalidOperationException) when (process.HasExited) {
                        // The process can exit between HasExited and Kill.
                    }
                    if (!process.WaitForExit (5000))
                        throw new TimeoutException ($"The AVD name process did not exit after being killed: {adbPath}");
                }
            }
            Log.LogDebugMessage ($"Running process: exit code == {result.ExitStatus.ExitCode}");
            using var reader = new StringReader (result.StandardOutput);
            string? line;
            while ((line = reader.ReadLine ()) != null && line.Length == 0) {
            }
            if (result.ExitStatus.ExitCode == 0 && line != null) {
                var avdName = line.Trim ();
                // Verify it's not the "OK" response
                if (!avdName.IsNullOrEmpty () && !avdName.Equals ("OK", StringComparison.OrdinalIgnoreCase)) {
                    Log.LogDebugMessage ($"Emulator {serial} has AVD name: {avdName}");
                    return avdName;
                }
            }
        } catch (Exception ex) {
            Log.LogDebugMessage ($"Failed to get AVD name for {serial}: {ex.Message}");
        }

        return null;
    }
}
