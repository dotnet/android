// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tools;

/// <summary>
/// Helper for building environment variables for Android SDK tools.
/// Returns a dictionary that can be passed to <see cref="ProcessUtils.StartProcess"/>.
/// </summary>
internal static class AndroidEnvironmentHelper
{
	/// <summary>
	/// Builds environment variables needed to run Android SDK tools.
	/// Pass the result to <see cref="ProcessUtils.StartProcess"/> via the environmentVariables parameter.
	/// </summary>
	internal static Dictionary<string, string> GetEnvironmentVariables (string? sdkPath, string? jdkPath)
	{
		var env = new Dictionary<string, string> ();

		if (sdkPath is { Length: > 0 })
			env [EnvironmentVariableNames.AndroidHome] = sdkPath;

		if (jdkPath is { Length: > 0 }) {
			env [EnvironmentVariableNames.JavaHome] = jdkPath;
			var jdkBin = Path.Combine (jdkPath, "bin");
			var currentPath = Environment.GetEnvironmentVariable (EnvironmentVariableNames.Path) ?? "";
			env [EnvironmentVariableNames.Path] = string.IsNullOrEmpty (currentPath) ? jdkBin : jdkBin + Path.PathSeparator + currentPath;
		}

		if (string.IsNullOrEmpty (Environment.GetEnvironmentVariable (EnvironmentVariableNames.AndroidUserHome)))
			env [EnvironmentVariableNames.AndroidUserHome] = Path.Combine (
				Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".android");

		return env;
	}
}
