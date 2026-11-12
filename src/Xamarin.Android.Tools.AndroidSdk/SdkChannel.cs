// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools;

/// <summary>
/// Selects SDK packages from channels up to and including the specified channel.
/// </summary>
public enum SdkChannel
{
	Stable = 0,
	Beta = 1,
	Dev = 2,
	Canary = 3,
}
