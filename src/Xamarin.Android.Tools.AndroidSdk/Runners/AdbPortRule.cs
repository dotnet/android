// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools;

/// <summary>
/// Represents an adb port forwarding rule as reported by 'adb reverse --list' or 'adb forward --list'.
/// </summary>
public record AdbPortRule (AdbPortSpec Remote, AdbPortSpec Local);
