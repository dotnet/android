// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools;

/// <summary>
/// Represents an Android device or emulator from 'adb devices -l' output.
/// Mirrors the metadata produced by dotnet/android's GetAvailableAndroidDevices task.
/// </summary>
public class AdbDeviceInfo
{
	/// <summary>
	/// Serial number of the device (e.g., "emulator-5554", "0A041FDD400327").
	/// For non-running emulators, this is the AVD name.
	/// </summary>
	public string Serial { get; set; } = string.Empty;

	/// <summary>
	/// Human-friendly description of the device (e.g., "Pixel 7 API 35", "Pixel 6 Pro").
	/// </summary>
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Device type: Device or Emulator.
	/// </summary>
	public AdbDeviceType Type { get; set; }

	/// <summary>
	/// Device status: Online, Offline, Unauthorized, NoPermissions, NotRunning, Unknown.
	/// </summary>
	public AdbDeviceStatus Status { get; set; }

	/// <summary>
	/// AVD name for emulators (e.g., "pixel_7_api_35"). Null for physical devices.
	/// </summary>
	public string? AvdName { get; set; }

	/// <summary>
	/// Device model from adb properties (e.g., "Pixel_6_Pro").
	/// </summary>
	public string? Model { get; set; }

	/// <summary>
	/// Product name from adb properties (e.g., "raven").
	/// </summary>
	public string? Product { get; set; }

	/// <summary>
	/// Device code name from adb properties (e.g., "raven").
	/// </summary>
	public string? Device { get; set; }

	/// <summary>
	/// Transport ID from adb properties.
	/// </summary>
	public string? TransportId { get; set; }

	/// <summary>
	/// Whether this device is an emulator.
	/// </summary>
	public bool IsEmulator => Type == AdbDeviceType.Emulator;
}
