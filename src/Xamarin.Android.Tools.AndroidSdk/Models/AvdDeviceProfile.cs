// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools;

/// <summary>
/// Represents a hardware device profile (e.g., "pixel_7", "Nexus 5X") from <c>avdmanager list device --compact</c>.
/// </summary>
public record AvdDeviceProfile (string Id);
