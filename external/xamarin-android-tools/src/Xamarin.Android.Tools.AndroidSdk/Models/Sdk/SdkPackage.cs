// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools;

/// <summary>Information about an SDK package as reported by the <c>sdkmanager</c> CLI.</summary>
public record SdkPackage (string Path, string? Version = null, string? Description = null, bool IsInstalled = false);

