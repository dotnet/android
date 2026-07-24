// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools;

/// <summary>
/// Represents the status of an Android device.
/// </summary>
public enum AdbDeviceStatus
{
	Online,
	Offline,
	Unauthorized,
	NoPermissions,
	NotRunning,
	Unknown
}
