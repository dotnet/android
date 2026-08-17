// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tools;

/// <summary>
/// Options for booting an Android emulator.
/// </summary>
public record EmulatorBootOptions
{
	public TimeSpan BootTimeout { get; init; } = TimeSpan.FromSeconds (300);
	public List<string>? AdditionalArgs { get; init; }
	public bool ColdBoot { get; init; }
	public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds (500);
}
