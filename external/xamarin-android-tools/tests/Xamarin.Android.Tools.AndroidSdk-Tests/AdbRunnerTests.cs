// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests;

/// <summary>
/// Tests for AdbRunner parsing, formatting, and merging logic.
/// Ported from dotnet/android GetAvailableAndroidDevicesTests.
///
/// API consumer reference:
///   - ParseAdbDevicesOutput: used by dotnet/android GetAvailableAndroidDevices task
///   - BuildDeviceDescription: used by dotnet/android GetAvailableAndroidDevices task
///   - FormatDisplayName: used by dotnet/android GetAvailableAndroidDevices tests
///   - MergeDevicesAndEmulators: used by dotnet/android GetAvailableAndroidDevices task
///   - MapAdbStateToStatus: used internally by ParseAdbDevicesOutput, public for extensibility
///   - ListDevicesAsync: used by MAUI DevTools Adb provider (Providers/Android/Adb.cs)
///   - WaitForDeviceAsync: used by MAUI DevTools Adb provider
///   - StopEmulatorAsync: used by MAUI DevTools Adb provider
///   - GetEmulatorAvdNameAsync: internal, used by ListDevicesAsync only
/// </summary>
[TestFixture]
public class AdbRunnerTests
{
	// --- ParseAdbDevicesOutput tests ---
	// Consumer: dotnet/android GetAvailableAndroidDevices.cs, MAUI DevTools (via ListDevicesAsync)

	[Test]
	public void ParseAdbDevicesOutput_RealWorldData ()
	{
		var output =
			"List of devices attached\n" +
			"0A041FDD400327         device product:redfin model:Pixel_5 device:redfin transport_id:2\n" +
			"emulator-5554          device product:sdk_gphone64_x86_64 model:sdk_gphone64_x86_64 device:emu64xa transport_id:1\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (2, devices.Count);

		// Physical device
		Assert.AreEqual ("0A041FDD400327", devices [0].Serial);
		Assert.AreEqual (AdbDeviceType.Device, devices [0].Type);
		Assert.AreEqual (AdbDeviceStatus.Online, devices [0].Status);
		Assert.AreEqual ("Pixel 5", devices [0].Description);
		Assert.AreEqual ("Pixel_5", devices [0].Model);
		Assert.AreEqual ("redfin", devices [0].Product);
		Assert.AreEqual ("redfin", devices [0].Device);
		Assert.AreEqual ("2", devices [0].TransportId);
		Assert.IsFalse (devices [0].IsEmulator);

		// Emulator
		Assert.AreEqual ("emulator-5554", devices [1].Serial);
		Assert.AreEqual (AdbDeviceType.Emulator, devices [1].Type);
		Assert.AreEqual (AdbDeviceStatus.Online, devices [1].Status);
		Assert.AreEqual ("sdk gphone64 x86 64", devices [1].Description); // model with underscores replaced
		Assert.AreEqual ("sdk_gphone64_x86_64", devices [1].Model);
		Assert.AreEqual ("1", devices [1].TransportId);
		Assert.IsTrue (devices [1].IsEmulator);
	}

	[Test]
	public void ParseAdbDevicesOutput_EmptyOutput ()
	{
		var output = "List of devices attached\n\n";
		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));
		Assert.AreEqual (0, devices.Count);
	}

	[Test]
	public void ParseAdbDevicesOutput_SingleEmulator ()
	{
		var output =
			"List of devices attached\n" +
			"emulator-5554          device product:sdk_gphone64_arm64 model:sdk_gphone64_arm64 device:emu64a transport_id:1\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (1, devices.Count);
		Assert.AreEqual ("emulator-5554", devices [0].Serial);
		Assert.AreEqual (AdbDeviceType.Emulator, devices [0].Type);
		Assert.AreEqual (AdbDeviceStatus.Online, devices [0].Status);
		Assert.AreEqual ("sdk_gphone64_arm64", devices [0].Model);
		Assert.AreEqual ("1", devices [0].TransportId);
	}

	[Test]
	public void ParseAdbDevicesOutput_SinglePhysicalDevice ()
	{
		var output =
			"List of devices attached\n" +
			"0A041FDD400327         device usb:1-1 product:raven model:Pixel_6_Pro device:raven transport_id:2\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (1, devices.Count);
		Assert.AreEqual ("0A041FDD400327", devices [0].Serial);
		Assert.AreEqual (AdbDeviceType.Device, devices [0].Type);
		Assert.AreEqual (AdbDeviceStatus.Online, devices [0].Status);
		Assert.AreEqual ("Pixel 6 Pro", devices [0].Description);
		Assert.AreEqual ("Pixel_6_Pro", devices [0].Model);
		Assert.AreEqual ("raven", devices [0].Product);
		Assert.AreEqual ("2", devices [0].TransportId);
	}

	[Test]
	public void ParseAdbDevicesOutput_OfflineDevice ()
	{
		var output =
			"List of devices attached\n" +
			"emulator-5554          offline product:sdk_gphone64_arm64 model:sdk_gphone64_arm64 device:emu64a transport_id:1\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (1, devices.Count);
		Assert.AreEqual (AdbDeviceStatus.Offline, devices [0].Status);
	}

	[Test]
	public void ParseAdbDevicesOutput_UnauthorizedDevice ()
	{
		var output =
			"List of devices attached\n" +
			"0A041FDD400327         unauthorized usb:1-1\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (1, devices.Count);
		Assert.AreEqual ("0A041FDD400327", devices [0].Serial);
		Assert.AreEqual (AdbDeviceStatus.Unauthorized, devices [0].Status);
		Assert.AreEqual (AdbDeviceType.Device, devices [0].Type);
	}

	[Test]
	public void ParseAdbDevicesOutput_NoPermissionsDevice ()
	{
		var output =
			"List of devices attached\n" +
			"????????????????       no permissions usb:1-1\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (1, devices.Count);
		Assert.AreEqual ("????????????????", devices [0].Serial);
		Assert.AreEqual (AdbDeviceStatus.NoPermissions, devices [0].Status);
	}

	[Test]
	public void ParseAdbDevicesOutput_DeviceWithMinimalMetadata ()
	{
		var output =
			"List of devices attached\n" +
			"ABC123                 device\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (1, devices.Count);
		Assert.AreEqual ("ABC123", devices [0].Serial);
		Assert.AreEqual (AdbDeviceType.Device, devices [0].Type);
		Assert.AreEqual (AdbDeviceStatus.Online, devices [0].Status);
		Assert.AreEqual ("ABC123", devices [0].Description, "Should fall back to serial");
	}

	[Test]
	public void ParseAdbDevicesOutput_InvalidLines ()
	{
		var output =
			"List of devices attached\n" +
			"\n" +
			"   \n" +
			"Some random text\n" +
			"* daemon not running; starting now at tcp:5037\n" +
			"* daemon started successfully\n" +
			"emulator-5554          device product:sdk_gphone64_arm64 model:sdk_gphone64_arm64 device:emu64a transport_id:1\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (1, devices.Count, "Should only return valid device lines");
		Assert.AreEqual ("emulator-5554", devices [0].Serial);
	}

	[Test]
	public void ParseAdbDevicesOutput_MixedDeviceStates ()
	{
		var output =
			"List of devices attached\n" +
			"emulator-5554          device product:sdk_gphone64_arm64 model:Pixel_7 device:emu64a\n" +
			"emulator-5556          offline\n" +
			"0A041FDD400327         device usb:1-1 product:raven model:Pixel_6_Pro\n" +
			"0B123456789ABC         unauthorized usb:1-2\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (4, devices.Count);
		Assert.AreEqual (AdbDeviceStatus.Online, devices [0].Status);
		Assert.AreEqual (AdbDeviceStatus.Offline, devices [1].Status);
		Assert.AreEqual (AdbDeviceStatus.Online, devices [2].Status);
		Assert.AreEqual (AdbDeviceStatus.Unauthorized, devices [3].Status);
	}

	[Test]
	public void ParseAdbDevicesOutput_WindowsNewlines ()
	{
		var output =
			"List of devices attached\r\n" +
			"emulator-5554          device transport_id:1\r\n" +
			"\r\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (1, devices.Count);
		Assert.AreEqual ("emulator-5554", devices [0].Serial);
		Assert.IsTrue (devices [0].IsEmulator);
	}

	[Test]
	public void ParseAdbDevicesOutput_TabSeparator ()
	{
		var output =
			"List of devices attached\n" +
			"emulator-5554\tdevice\n" +
			"R5CR10YZQPJ\tdevice\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (2, devices.Count);
		Assert.AreEqual ("emulator-5554", devices [0].Serial);
		Assert.AreEqual ("R5CR10YZQPJ", devices [1].Serial);
	}

	[Test]
	public void ParseAdbDevicesOutput_IpPortDevice ()
	{
		var output =
			"List of devices attached\n" +
			"192.168.1.100:5555     device product:sdk_gphone64_arm64 model:Remote_Device\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (1, devices.Count);
		Assert.AreEqual ("192.168.1.100:5555", devices [0].Serial);
		Assert.AreEqual (AdbDeviceType.Device, devices [0].Type, "IP devices should be Device");
		Assert.AreEqual ("Remote Device", devices [0].Description);
	}

	[Test]
	public void ParseAdbDevicesOutput_AdbDaemonStarting ()
	{
		var output =
			"* daemon not running; starting now at tcp:5037\n" +
			"* daemon started successfully\n" +
			"List of devices attached\n" +
			"emulator-5554          device product:sdk_gphone64_arm64 model:sdk_gphone64_arm64 device:emu64a transport_id:1\n" +
			"0A041FDD400327         device usb:1-1 product:raven model:Pixel_6_Pro device:raven transport_id:2\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (2, devices.Count, "Should parse devices even with daemon startup messages");
	}

	// --- BuildDeviceDescription tests ---
	// Consumer: dotnet/android GetAvailableAndroidDevices.cs

	[Test]
	public void DescriptionPriorityOrder ()
	{
		// Model has highest priority
		var output1 = "List of devices attached\ndevice1                device product:product_name model:model_name device:device_name\n";
		var devices1 = AdbRunner.ParseAdbDevicesOutput (output1.Split ('\n'));
		Assert.AreEqual ("model name", devices1 [0].Description, "Model should have highest priority");

		// Product has second priority
		var output2 = "List of devices attached\ndevice2                device product:product_name device:device_name\n";
		var devices2 = AdbRunner.ParseAdbDevicesOutput (output2.Split ('\n'));
		Assert.AreEqual ("product name", devices2 [0].Description, "Product should have second priority");

		// Device code name has third priority
		var output3 = "List of devices attached\ndevice3                device device:device_name\n";
		var devices3 = AdbRunner.ParseAdbDevicesOutput (output3.Split ('\n'));
		Assert.AreEqual ("device name", devices3 [0].Description, "Device should have third priority");
	}

	// --- FormatDisplayName tests ---
	// Consumer: dotnet/android GetAvailableAndroidDevicesTests, BuildDeviceDescription (via AVD name formatting)

	[Test]
	public void FormatDisplayName_ReplacesUnderscoresWithSpaces ()
	{
		Assert.AreEqual ("Pixel 7 Pro", AdbRunner.FormatDisplayName ("pixel_7_pro"));
	}

	[Test]
	public void FormatDisplayName_AppliesTitleCase ()
	{
		Assert.AreEqual ("Pixel 7 Pro", AdbRunner.FormatDisplayName ("pixel 7 pro"));
	}

	[Test]
	public void FormatDisplayName_ReplacesApiWithAPIUppercase ()
	{
		Assert.AreEqual ("Pixel 5 API 34", AdbRunner.FormatDisplayName ("pixel_5_api_34"));
	}

	[Test]
	public void FormatDisplayName_HandlesMultipleApiOccurrences ()
	{
		Assert.AreEqual ("Test API Device API 35", AdbRunner.FormatDisplayName ("test_api_device_api_35"));
	}

	[Test]
	public void FormatDisplayName_HandlesMixedCaseInput ()
	{
		Assert.AreEqual ("Pixel 7 API 35", AdbRunner.FormatDisplayName ("PiXeL_7_API_35"));
	}

	[Test]
	public void FormatDisplayName_HandlesComplexNames ()
	{
		Assert.AreEqual ("Pixel 9 Pro Xl API 36", AdbRunner.FormatDisplayName ("pixel_9_pro_xl_api_36"));
	}

	[Test]
	public void FormatDisplayName_PreservesNumbersAndSpecialChars ()
	{
		Assert.AreEqual ("Pixel 7-Pro API 35", AdbRunner.FormatDisplayName ("pixel_7-pro_api_35"));
	}

	[Test]
	public void FormatDisplayName_HandlesEmptyString ()
	{
		Assert.AreEqual ("", AdbRunner.FormatDisplayName (""));
	}

	[Test]
	public void FormatDisplayName_HandlesSingleWord ()
	{
		Assert.AreEqual ("Pixel", AdbRunner.FormatDisplayName ("pixel"));
	}

	[Test]
	public void FormatDisplayName_DoesNotReplaceApiInsideWords ()
	{
		Assert.AreEqual ("Erapidevice", AdbRunner.FormatDisplayName ("erapidevice"));
	}

	// --- MapAdbStateToStatus tests ---
	// Consumer: ParseAdbDevicesOutput (internal mapping), public for custom consumers

	[Test]
	public void MapAdbStateToStatus_AllStates ()
	{
		Assert.AreEqual (AdbDeviceStatus.Online, AdbRunner.MapAdbStateToStatus ("device"));
		Assert.AreEqual (AdbDeviceStatus.Offline, AdbRunner.MapAdbStateToStatus ("offline"));
		Assert.AreEqual (AdbDeviceStatus.Unauthorized, AdbRunner.MapAdbStateToStatus ("unauthorized"));
		Assert.AreEqual (AdbDeviceStatus.NoPermissions, AdbRunner.MapAdbStateToStatus ("no permissions"));
		Assert.AreEqual (AdbDeviceStatus.Unknown, AdbRunner.MapAdbStateToStatus ("something-else"));
	}

	// --- MergeDevicesAndEmulators tests ---
	// Consumer: dotnet/android GetAvailableAndroidDevices.cs

	[Test]
	public void MergeDevicesAndEmulators_NoEmulators_ReturnsAdbDevicesOnly ()
	{
		var adbDevices = new List<AdbDeviceInfo> {
			new AdbDeviceInfo { Serial = "0A041FDD400327", Description = "Pixel 5", Type = AdbDeviceType.Device, Status = AdbDeviceStatus.Online },
		};

		var result = AdbRunner.MergeDevicesAndEmulators (adbDevices, new List<string> ());

		Assert.AreEqual (1, result.Count);
		Assert.AreEqual ("0A041FDD400327", result [0].Serial);
	}

	[Test]
	public void MergeDevicesAndEmulators_NoRunningEmulators_AddsAllAvailable ()
	{
		var adbDevices = new List<AdbDeviceInfo> {
			new AdbDeviceInfo { Serial = "0A041FDD400327", Description = "Pixel 5", Type = AdbDeviceType.Device, Status = AdbDeviceStatus.Online },
		};
		var available = new List<string> { "pixel_7_api_35", "pixel_9_api_36" };

		var result = AdbRunner.MergeDevicesAndEmulators (adbDevices, available);

		Assert.AreEqual (3, result.Count);

		// Online first
		Assert.AreEqual ("0A041FDD400327", result [0].Serial);

		// Non-running sorted alphabetically
		Assert.AreEqual ("pixel_7_api_35", result [1].Serial);
		Assert.AreEqual (AdbDeviceStatus.NotRunning, result [1].Status);
		Assert.AreEqual ("pixel_7_api_35", result [1].AvdName);
		Assert.AreEqual ("Pixel 7 API 35 (Not Running)", result [1].Description);

		Assert.AreEqual ("pixel_9_api_36", result [2].Serial);
		Assert.AreEqual (AdbDeviceStatus.NotRunning, result [2].Status);
		Assert.AreEqual ("Pixel 9 API 36 (Not Running)", result [2].Description);
	}

	[Test]
	public void MergeDevicesAndEmulators_RunningEmulator_NoDuplicate ()
	{
		var adbDevices = new List<AdbDeviceInfo> {
			new AdbDeviceInfo {
				Serial = "emulator-5554", Description = "Pixel 7 API 35",
				Type = AdbDeviceType.Emulator, Status = AdbDeviceStatus.Online,
				AvdName = "pixel_7_api_35"
			},
		};
		var available = new List<string> { "pixel_7_api_35" };

		var result = AdbRunner.MergeDevicesAndEmulators (adbDevices, available);

		Assert.AreEqual (1, result.Count, "Should not duplicate running emulator");
		Assert.AreEqual ("emulator-5554", result [0].Serial);
		Assert.AreEqual (AdbDeviceStatus.Online, result [0].Status);
	}

	[Test]
	public void MergeDevicesAndEmulators_MixedRunningAndNotRunning ()
	{
		var adbDevices = new List<AdbDeviceInfo> {
			new AdbDeviceInfo {
				Serial = "emulator-5554", Description = "Pixel 7 API 35",
				Type = AdbDeviceType.Emulator, Status = AdbDeviceStatus.Online,
				AvdName = "pixel_7_api_35"
			},
			new AdbDeviceInfo {
				Serial = "0A041FDD400327", Description = "Pixel 5",
				Type = AdbDeviceType.Device, Status = AdbDeviceStatus.Online,
			},
		};
		var available = new List<string> { "pixel_7_api_35", "pixel_9_api_36", "nexus_5_api_30" };

		var result = AdbRunner.MergeDevicesAndEmulators (adbDevices, available);

		Assert.AreEqual (4, result.Count);

		// Online devices first, sorted alphabetically
		Assert.AreEqual ("0A041FDD400327", result [0].Serial);
		Assert.AreEqual (AdbDeviceStatus.Online, result [0].Status);

		Assert.AreEqual ("emulator-5554", result [1].Serial);
		Assert.AreEqual (AdbDeviceStatus.Online, result [1].Status);

		// Non-running emulators second, sorted alphabetically
		Assert.AreEqual ("nexus_5_api_30", result [2].Serial);
		Assert.AreEqual (AdbDeviceStatus.NotRunning, result [2].Status);
		Assert.AreEqual ("Nexus 5 API 30 (Not Running)", result [2].Description);

		Assert.AreEqual ("pixel_9_api_36", result [3].Serial);
		Assert.AreEqual (AdbDeviceStatus.NotRunning, result [3].Status);
	}

	[Test]
	public void MergeDevicesAndEmulators_CaseInsensitiveAvdNameMatching ()
	{
		var adbDevices = new List<AdbDeviceInfo> {
			new AdbDeviceInfo {
				Serial = "emulator-5554", Description = "Pixel 7 API 35",
				Type = AdbDeviceType.Emulator, Status = AdbDeviceStatus.Online,
				AvdName = "Pixel_7_API_35"
			},
		};
		var available = new List<string> { "pixel_7_api_35" }; // lowercase

		var result = AdbRunner.MergeDevicesAndEmulators (adbDevices, available);

		Assert.AreEqual (1, result.Count, "Should match AVD names case-insensitively");
	}

	[Test]
	public void MergeDevicesAndEmulators_EmptyAdbDevices_ReturnsAllAvailable ()
	{
		var result = AdbRunner.MergeDevicesAndEmulators (new List<AdbDeviceInfo> (), new List<string> { "pixel_7_api_35", "pixel_9_api_36" });

		Assert.AreEqual (2, result.Count);
		Assert.AreEqual ("Pixel 7 API 35 (Not Running)", result [0].Description);
		Assert.AreEqual ("Pixel 9 API 36 (Not Running)", result [1].Description);
	}

	// --- AdbPath tests ---
	// Consumer: MAUI DevTools Adb provider (AdbPath, IsAvailable properties)

	[Test]
	public void Constructor_NullPath_ThrowsArgumentException ()
	{
		Assert.Throws<ArgumentException> (() => new AdbRunner (null!));
	}

	[Test]
	public void Constructor_EmptyPath_ThrowsArgumentException ()
	{
		Assert.Throws<ArgumentException> (() => new AdbRunner (""));
	}

	[Test]
	public void Constructor_WhitespacePath_ThrowsArgumentException ()
	{
		Assert.Throws<ArgumentException> (() => new AdbRunner ("   "));
	}

	[Test]
	public void ParseAdbDevicesOutput_DeviceWithProductOnly ()
	{
		var output =
			"List of devices attached\n" +
			"emulator-5554          device product:aosp_x86_64\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (1, devices.Count);
		Assert.AreEqual ("aosp_x86_64", devices [0].Product);
		Assert.AreEqual (AdbDeviceType.Emulator, devices [0].Type);
	}

	[Test]
	public void ParseAdbDevicesOutput_MultipleDevices ()
	{
		var output =
			"List of devices attached\n" +
			"emulator-5554          device product:sdk_gphone64_arm64 model:sdk_gphone64_arm64 device:emu64a transport_id:1\n" +
			"emulator-5556          device product:sdk_gphone64_x86_64 model:sdk_gphone64_x86_64 device:emu64x transport_id:3\n" +
			"0A041FDD400327         device usb:1-1 product:raven model:Pixel_6_Pro device:raven transport_id:2\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (3, devices.Count);
		Assert.AreEqual ("emulator-5554", devices [0].Serial);
		Assert.AreEqual (AdbDeviceType.Emulator, devices [0].Type);
		Assert.AreEqual ("emulator-5556", devices [1].Serial);
		Assert.AreEqual (AdbDeviceType.Emulator, devices [1].Type);
		Assert.AreEqual ("0A041FDD400327", devices [2].Serial);
		Assert.AreEqual (AdbDeviceType.Device, devices [2].Type);
		Assert.AreEqual ("Pixel_6_Pro", devices [2].Model);
	}

	[Test]
	public void MergeDevicesAndEmulators_AllEmulatorsRunning_NoDuplicate ()
	{
		var emulator1 = new AdbDeviceInfo {
			Serial = "emulator-5554", Description = "Pixel 7 API 35",
			Type = AdbDeviceType.Emulator, Status = AdbDeviceStatus.Online,
			AvdName = "pixel_7_api_35",
		};
		var emulator2 = new AdbDeviceInfo {
			Serial = "emulator-5556", Description = "Pixel 9 API 36",
			Type = AdbDeviceType.Emulator, Status = AdbDeviceStatus.Online,
			AvdName = "pixel_9_api_36",
		};

		var result = AdbRunner.MergeDevicesAndEmulators (
			new List<AdbDeviceInfo> { emulator1, emulator2 },
			new List<string> { "pixel_7_api_35", "pixel_9_api_36" });

		Assert.AreEqual (2, result.Count, "Should not add duplicates when all emulators are running");
		Assert.IsTrue (result.All (d => d.Status == AdbDeviceStatus.Online));
	}

	[Test]
	public void MergeDevicesAndEmulators_NonRunningEmulatorHasFormattedDescription ()
	{
		var result = AdbRunner.MergeDevicesAndEmulators (
			new List<AdbDeviceInfo> (),
			new List<string> { "pixel_7_pro_api_35" });

		Assert.AreEqual (1, result.Count);
		Assert.AreEqual ("Pixel 7 Pro API 35 (Not Running)", result [0].Description);
		Assert.AreEqual (AdbDeviceStatus.NotRunning, result [0].Status);
	}

	[Test]
	public void ParseAdbDevicesOutput_RecoveryState ()
	{
		var output = "List of devices attached\n" +
			"0A041FDD400327         recovery product:redfin model:Pixel_5 device:redfin transport_id:2\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (1, devices.Count);
		Assert.AreEqual ("0A041FDD400327", devices [0].Serial);
		Assert.AreEqual (AdbDeviceStatus.Unknown, devices [0].Status);
	}

	[Test]
	public void ParseAdbDevicesOutput_SideloadState ()
	{
		var output = "List of devices attached\n" +
			"0A041FDD400327         sideload\n";

		var devices = AdbRunner.ParseAdbDevicesOutput (output.Split ('\n'));

		Assert.AreEqual (1, devices.Count);
		Assert.AreEqual ("0A041FDD400327", devices [0].Serial);
		Assert.AreEqual (AdbDeviceStatus.Unknown, devices [0].Status);
	}

	[Test]
	public void MapAdbStateToStatus_Recovery_ReturnsUnknown ()
	{
		Assert.AreEqual (AdbDeviceStatus.Unknown, AdbRunner.MapAdbStateToStatus ("recovery"));
	}

	[Test]
	public void MapAdbStateToStatus_Sideload_ReturnsUnknown ()
	{
		Assert.AreEqual (AdbDeviceStatus.Unknown, AdbRunner.MapAdbStateToStatus ("sideload"));
	}

	// --- WaitForDeviceAsync tests ---
	// Consumer: MAUI DevTools Adb provider (WaitForDeviceAsync)

	[Test]
	public void WaitForDeviceAsync_NegativeTimeout_ThrowsArgumentOutOfRange ()
	{
		var runner = new AdbRunner ("/fake/sdk/platform-tools/adb");
		Assert.ThrowsAsync<System.ArgumentOutOfRangeException> (
			async () => await runner.WaitForDeviceAsync (timeout: System.TimeSpan.FromSeconds (-1)));
	}

	[Test]
	public void WaitForDeviceAsync_ZeroTimeout_ThrowsArgumentOutOfRange ()
	{
		var runner = new AdbRunner ("/fake/sdk/platform-tools/adb");
		Assert.ThrowsAsync<System.ArgumentOutOfRangeException> (
			async () => await runner.WaitForDeviceAsync (timeout: System.TimeSpan.Zero));
	}
}
