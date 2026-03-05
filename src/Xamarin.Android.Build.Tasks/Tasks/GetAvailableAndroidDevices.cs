#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

/// <summary>
/// MSBuild task that queries available Android devices and emulators using 'adb devices -l'
/// and 'emulator -list-avds'. Merges the results to provide a complete list of available
/// devices including emulators that are not currently running.
/// Returns a list of devices with metadata for device selection in dotnet run.
/// </summary>
public class GetAvailableAndroidDevices : AndroidAdb
{
    enum DeviceType
    {
        Device,
        Emulator
    }

    // Pattern to match device lines: <serial> <state> [key:value ...]
    // Example: emulator-5554          device product:sdk_gphone64_arm64 model:sdk_gphone64_arm64
    static readonly Regex AdbDevicesRegex = new(@"^([^\s]+)\s+(device|offline|unauthorized|no permissions)\s*(.*)$", RegexOptions.Compiled);
    static readonly Regex ApiRegex = new(@"\bApi\b", RegexOptions.Compiled);

    readonly List<string> output = [];

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

    public GetAvailableAndroidDevices ()
    {
        Command = "devices";
        Arguments = "-l";
    }

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

        // Parse devices from adb
        var adbDevices = ParseAdbDevicesOutput (output);
        Log.LogDebugMessage ($"Found {adbDevices.Count} device(s) from adb");

        // Get available emulators from 'emulator -list-avds'
        var availableEmulators = GetAvailableEmulators ();
        Log.LogDebugMessage ($"Found {availableEmulators.Count} available emulator(s) from 'emulator -list-avds'");

        // Merge the lists
        var mergedDevices = MergeDevicesAndEmulators (adbDevices, availableEmulators);
        Devices = mergedDevices.ToArray ();

        Log.LogDebugMessage ($"Total {Devices.Length} Android device(s)/emulator(s) after merging");

        return !Log.HasLoggedErrors;
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
            var exitCode = MonoAndroidHelper.RunProcess (
                emulatorPath,
                "-list-avds",
                Log,
                onOutput: (sender, e) => {
                    if (!e.Data.IsNullOrWhiteSpace ()) {
                        var avdName = e.Data.Trim ();
                        emulators.Add (avdName);
                        Log.LogDebugMessage ($"Found available emulator: {avdName}");
                    }
                },
                logWarningOnFailure: false
            );

            if (exitCode != 0) {
                Log.LogDebugMessage ($"'emulator -list-avds' returned exit code: {exitCode}");
            }
        } catch (Exception ex) {
            Log.LogDebugMessage ($"Failed to run 'emulator -list-avds': {ex.Message}");
        }

        return emulators;
    }

    /// <summary>
    /// Merges devices from adb with available emulators.
    /// Running emulators (already in adb list) are not duplicated.
    /// Non-running emulators are added with Status="NotRunning".
    /// Results are sorted: online devices first, then not-running emulators, alphabetically by description within each group.
    /// </summary>
    internal List<ITaskItem> MergeDevicesAndEmulators (List<ITaskItem> adbDevices, List<string> availableEmulators)
    {
        var result = new List<ITaskItem> (adbDevices);

        // Build a set of AVD names that are already running (from adb devices)
        var runningAvdNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
        foreach (var device in adbDevices) {
            var avdName = device.GetMetadata ("AvdName");
            if (!avdName.IsNullOrEmpty ()) {
                runningAvdNames.Add (avdName);
            }
        }

        Log.LogDebugMessage ($"Running emulators AVD names: {string.Join (", ", runningAvdNames)}");

        // Add non-running emulators
        foreach (var avdName in availableEmulators) {
            if (runningAvdNames.Contains (avdName)) {
                Log.LogDebugMessage ($"Emulator '{avdName}' is already running, skipping");
                continue;
            }

            // Create item for non-running emulator
            // Use the AVD name as the ItemSpec since there's no serial yet
            var item = new TaskItem (avdName);
            var displayName = FormatDisplayName (avdName, avdName);
            item.SetMetadata ("Description", $"{displayName} (Not Running)");
            item.SetMetadata ("Type", DeviceType.Emulator.ToString ());
            item.SetMetadata ("Status", "NotRunning");
            item.SetMetadata ("AvdName", avdName);

            result.Add (item);
            Log.LogDebugMessage ($"Added non-running emulator: {avdName}");
        }

        // Sort: online devices first, then not-running emulators, alphabetically by description within each group
        result.Sort ((a, b) => {
            var aNotRunning = string.Equals (a.GetMetadata ("Status"), "NotRunning", StringComparison.OrdinalIgnoreCase);
            var bNotRunning = string.Equals (b.GetMetadata ("Status"), "NotRunning", StringComparison.OrdinalIgnoreCase);

            if (aNotRunning != bNotRunning) {
                return aNotRunning ? 1 : -1;
            }

            return string.Compare (a.GetMetadata ("Description"), b.GetMetadata ("Description"), StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    /// <summary>
    /// Parses the output of 'adb devices -l' command.
    /// Example output:
    /// List of devices attached
    /// emulator-5554          device product:sdk_gphone64_arm64 model:sdk_gphone64_arm64 device:emu64a transport_id:1
    /// 0A041FDD400327         device usb:1-1 product:raven model:Pixel_6_Pro device:raven transport_id:2
    /// </summary>
    List<ITaskItem> ParseAdbDevicesOutput (List<string> lines)
    {
        var devices = new List<ITaskItem> ();

        foreach (var line in lines) {
            // Skip the header line "List of devices attached"
            if (line.Contains ("List of devices") || line.IsNullOrWhiteSpace ())
                continue;

            var match = AdbDevicesRegex.Match (line);
            if (!match.Success)
                continue;

            var serial = match.Groups [1].Value.Trim ();
            var state = match.Groups [2].Value.Trim ();
            var properties = match.Groups [3].Value.Trim ();

            // Parse key:value pairs from the properties string
            var propDict = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
            if (!properties.IsNullOrWhiteSpace ()) {
                // Split by whitespace and parse key:value pairs
                var pairs = properties.Split ([' '], StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs) {
                    var colonIndex = pair.IndexOf (':');
                    if (colonIndex > 0 && colonIndex < pair.Length - 1) {
                        var key = pair.Substring (0, colonIndex);
                        var value = pair.Substring (colonIndex + 1);
                        propDict [key] = value;
                    }
                }
            }

            // Determine device type: Emulator or Device
            var deviceType = serial.StartsWith ("emulator-", StringComparison.OrdinalIgnoreCase) ? DeviceType.Emulator : DeviceType.Device;

            // For emulators, get the AVD name for duplicate detection
            string? avdName = null;
            if (deviceType == DeviceType.Emulator) {
                avdName = GetEmulatorAvdName (serial);
            }

            // Build a friendly description
            var description = BuildDeviceDescription (serial, propDict, deviceType, avdName);

            // Map adb state to device status
            var status = MapAdbStateToStatus (state);

            // Create the MSBuild item
            var item = new TaskItem (serial);
            item.SetMetadata ("Description", description);
            item.SetMetadata ("Type", deviceType.ToString ());
            item.SetMetadata ("Status", status);

            // Add AVD name for emulators (used for duplicate detection)
            if (!avdName.IsNullOrEmpty ()) {
                item.SetMetadata ("AvdName", avdName);
            }

            // Add optional metadata for additional information
            if (propDict.TryGetValue ("model", out var model))
                item.SetMetadata ("Model", model);
            if (propDict.TryGetValue ("product", out var product))
                item.SetMetadata ("Product", product);
            if (propDict.TryGetValue ("device", out var device))
                item.SetMetadata ("Device", device);
            if (propDict.TryGetValue ("transport_id", out var transportId))
                item.SetMetadata ("TransportId", transportId);

            devices.Add (item);
        }

        return devices;
    }

    string BuildDeviceDescription (string serial, Dictionary<string, string> properties, DeviceType deviceType, string? avdName)
    {
        // Try to build a human-friendly description
        // Priority: AVD name (for emulators) > model > product > device > serial

        // For emulators, try to get the AVD display name
        if (deviceType == DeviceType.Emulator && !avdName.IsNullOrEmpty ()) {
            return FormatDisplayName (serial, avdName!);
        }

        if (properties.TryGetValue ("model", out var model) && !model.IsNullOrEmpty ()) {
            // Clean up model name - replace underscores with spaces
            model = model.Replace ('_', ' ');
            return model;
        }

        if (properties.TryGetValue ("product", out var product) && !product.IsNullOrEmpty ()) {
            product = product.Replace ('_', ' ');
            return product;
        }

        if (properties.TryGetValue ("device", out var device) && !device.IsNullOrEmpty ()) {
            device = device.Replace ('_', ' ');
            return device;
        }

        // Fallback to serial number
        return serial;
    }

    static string MapAdbStateToStatus (string adbState)
    {
        // Map adb device states to the spec's status values
        return adbState.ToLowerInvariant () switch {
            "device" => "Online",
            "offline" => "Offline",
            "unauthorized" => "Unauthorized",
            "no permissions" => "NoPermissions",
            _ => "Unknown",
        };
    }

    /// <summary>
    /// Queries the emulator for its AVD name using 'adb -s <serial> emu avd name'.
    /// Returns the raw AVD name (not formatted).
    /// </summary>
    protected virtual string? GetEmulatorAvdName (string serial)
    {
        try {
            var adbPath = Path.Combine (ToolPath, ToolExe);
            var outputLines = new List<string> ();

            var exitCode = MonoAndroidHelper.RunProcess (
                adbPath,
                $"-s {serial} emu avd name",
                Log,
                onOutput: (sender, e) => {
                    if (!e.Data.IsNullOrEmpty ()) {
                        outputLines.Add (e.Data);
                    }
                },
                logWarningOnFailure: false
            );

            if (exitCode == 0 && outputLines.Count > 0) {
                var avdName = outputLines [0].Trim ();
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

    /// <summary>
    /// Formats the AVD name into a more user-friendly display name. Replace underscores with spaces and title case.
    /// </summary>
    public string FormatDisplayName (string serial, string avdName)
    {
        Log.LogDebugMessage ($"Emulator {serial}, original AVD name: {avdName}");

        // Title case and replace underscores with spaces
        var textInfo = CultureInfo.InvariantCulture.TextInfo;
        avdName = textInfo.ToTitleCase (avdName.Replace ('_', ' '));

        // Replace "Api" with "API"
        avdName = ApiRegex.Replace (avdName, "API");
        Log.LogDebugMessage ($"Emulator {serial}, formatted AVD display name: {avdName}");
        return avdName;
    }
}
