#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

/// <summary>
/// MSBuild task that queries available Android devices and emulators using 'adb devices -l'.
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

        var devices = ParseAdbDevicesOutput (output);
        Devices = devices.ToArray ();

        Log.LogDebugMessage ($"Found {Devices.Length} Android device(s)/emulator(s)");

        return !Log.HasLoggedErrors;
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
            if (line.Contains ("List of devices") || string.IsNullOrWhiteSpace (line))
                continue;

            var match = AdbDevicesRegex.Match (line);
            if (!match.Success)
                continue;

            var serial = match.Groups [1].Value.Trim ();
            var state = match.Groups [2].Value.Trim ();
            var properties = match.Groups [3].Value.Trim ();

            // Parse key:value pairs from the properties string
            var propDict = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace (properties)) {
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

            // Build a friendly description
            var description = BuildDeviceDescription (serial, propDict, deviceType);

            // Map adb state to device status
            var status = MapAdbStateToStatus (state);

            // Create the MSBuild item
            var item = new TaskItem (serial);
            item.SetMetadata ("Description", description);
            item.SetMetadata ("Type", deviceType.ToString ());
            item.SetMetadata ("Status", status);

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

    string BuildDeviceDescription (string serial, Dictionary<string, string> properties, DeviceType deviceType)
    {
        // Try to build a human-friendly description
        // Priority: AVD name (for emulators) > model > product > device > serial

        // For emulators, try to get the AVD display name
        if (deviceType == DeviceType.Emulator) {
            var avdName = GetEmulatorAvdDisplayName (serial);
            if (!string.IsNullOrEmpty (avdName))
                return avdName!;
        }

        if (properties.TryGetValue ("model", out var model) && !string.IsNullOrEmpty (model)) {
            // Clean up model name - replace underscores with spaces
            model = model.Replace ('_', ' ');
            return model;
        }

        if (properties.TryGetValue ("product", out var product) && !string.IsNullOrEmpty (product)) {
            product = product.Replace ('_', ' ');
            return product;
        }

        if (properties.TryGetValue ("device", out var device) && !string.IsNullOrEmpty (device)) {
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
    /// Queries the emulator for its AVD name using 'adb -s <serial> emu avd name'
    /// and formats it as a friendly display name.
    /// </summary>
    protected virtual string? GetEmulatorAvdDisplayName (string serial)
    {
        try {
            var adbPath = System.IO.Path.Combine (ToolPath, ToolExe);
            var outputLines = new List<string> ();

            var exitCode = MonoAndroidHelper.RunProcess (
                adbPath,
                $"-s {serial} emu avd name",
                Log,
                onOutput: (sender, e) => {
                    if (!string.IsNullOrEmpty (e.Data)) {
                        outputLines.Add (e.Data);
                        base.LogEventsFromTextOutput (e.Data, MessageImportance.Normal);
                    }
                },
                logWarningOnFailure: false
            );

            if (exitCode == 0 && outputLines.Count > 0) {
                var avdName = outputLines [0].Trim ();
                // Verify it's not the "OK" response
                if (!string.IsNullOrEmpty (avdName) && !avdName.Equals ("OK", StringComparison.OrdinalIgnoreCase)) {
                    return FormatDisplayName(serial, avdName);
                }
            }
        } catch (Exception ex) {
            Log.LogDebugMessage ($"Failed to get AVD display name for {serial}: {ex}");
        }

        return null;
    }

    /// <summary>
    /// Formats the AVD name into a more user-friendly display name. Replace underscores with spaces and title case.
    /// </summary>
    public string FormatDisplayName(string serial, string avdName)
    {
        Log.LogDebugMessage ($"Emulator {serial}, original AVD name: {avdName}");

        // Title case and replace underscores with spaces
        var textInfo = CultureInfo.InvariantCulture.TextInfo;
        avdName = textInfo.ToTitleCase(avdName.Replace ('_', ' '));

        // Replace "Api" with "API"
        avdName = ApiRegex.Replace (avdName, "API");
        Log.LogDebugMessage ($"Emulator {serial}, formatted AVD display name: {avdName}");
        return avdName;
    }
}
