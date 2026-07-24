// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Xamarin.Android.Tools;

/// <summary>
/// Shared defaults for Android SDK tool runners.
/// </summary>
static class RunnerDefaults
{
	/// <summary>
	/// A no-op logger that discards all messages. Used as the default
	/// when callers don't provide a logger callback.
	/// </summary>
	internal static readonly Action<TraceLevel, string> NullLogger = static (_, _) => { };
}
