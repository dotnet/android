// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Xamarin.Android.Tools;

/// <summary>
/// Status response from the ADB daemon after sending a command.
/// </summary>
internal enum AdbResponseStatus
{
	Okay,
	Fail,
}
