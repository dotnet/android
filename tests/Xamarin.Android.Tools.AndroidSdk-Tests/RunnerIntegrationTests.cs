// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Tools.Tests;

/// <summary>
/// Integration tests that verify AdbRunner works against real Android SDK tools.
///
/// These tests only run on CI (TF_BUILD=True or CI=true) where hosted
/// images have JDK (JAVA_HOME) and Android SDK (ANDROID_HOME) pre-installed.
/// Tests are skipped on local developer machines.
/// </summary>
[TestClass]
[TestCategory ("Integration")]
public class RunnerIntegrationTests
{
	static string sdkPath;
	static string jdkPath;
	static string adbPath;

	static void RequireCi ()
	{
		var tfBuild = Environment.GetEnvironmentVariable ("TF_BUILD");
		var ci = Environment.GetEnvironmentVariable ("CI");

		if (!string.Equals (tfBuild, "true", StringComparison.OrdinalIgnoreCase) &&
		    !string.Equals (ci, "true", StringComparison.OrdinalIgnoreCase)) {
			Assert.Inconclusive ("Integration tests only run on CI (TF_BUILD=True or CI=true).");
		}
	}

	/// <summary>
	/// One-time setup: use pre-installed JDK/SDK on CI agents.
	/// Azure Pipelines hosted images have JAVA_HOME and ANDROID_HOME already configured.
	/// </summary>
	[ClassInitialize]
	public static void OneTimeSetUp (TestContext context)
	{
		RequireCi ();

		// Use pre-installed JDK from JAVA_HOME (always available on CI agents)
		jdkPath = Environment.GetEnvironmentVariable (EnvironmentVariableNames.JavaHome);
		if (string.IsNullOrEmpty (jdkPath) || !Directory.Exists (jdkPath)) {
			Assert.Inconclusive ("JAVA_HOME not set or invalid — cannot run integration tests.");
			return;
		}
		Console.WriteLine ($"Using JDK from JAVA_HOME: {jdkPath}");

		// Use pre-installed Android SDK from ANDROID_HOME (must be available on CI agents)
		sdkPath = Environment.GetEnvironmentVariable (EnvironmentVariableNames.AndroidHome);
		if (string.IsNullOrEmpty (sdkPath) || !Directory.Exists (sdkPath)) {
			Assert.Inconclusive ("ANDROID_HOME not set or invalid — cannot run integration tests. Provision Android SDK and set ANDROID_HOME to enable these tests.");
			return;
		}

		Console.WriteLine ($"Using SDK from ANDROID_HOME: {sdkPath}");

		// Resolve the full path to adb for AdbRunner
		var adbExe = OS.IsWindows ? "adb.exe" : "adb";
		adbPath = Path.Combine (sdkPath, "platform-tools", adbExe);
		if (!File.Exists (adbPath))
			Assert.Inconclusive ($"adb not found at {adbPath}");
	}

	[TestMethod]
	public async Task AdbRunner_ListDevicesAsync_ReturnsWithoutError ()
	{
		var runner = new AdbRunner (adbPath);

		// On CI there are no physical devices or emulators, but the command
		// should succeed and return an empty (or non-null) list.
		var devices = await runner.ListDevicesAsync ();

		Assert.IsNotNull (devices);
		Console.WriteLine ($"ListDevicesAsync returned {devices.Count} device(s)");
	}

	[TestMethod]
	public async Task AdbRunner_WaitForDeviceAsync_TimesOut_WhenNoDevice ()
	{
		var runner = new AdbRunner (adbPath);
		var ex = await Assert.ThrowsExactlyAsync<TimeoutException> (async () =>
			await runner.WaitForDeviceAsync (timeout: TimeSpan.FromSeconds (5)));

		Assert.IsNotNull (ex);
		Console.WriteLine ($"WaitForDeviceAsync timed out as expected: {ex?.Message}");
	}

	[TestMethod]
	public async Task AvdManagerRunner_ListAvdsAsync_ReturnsWithoutError ()
	{
		var ext = OS.IsWindows ? ".bat" : "";
		var avdManagerPath = ProcessUtils.FindCmdlineTool (sdkPath, "avdmanager", ext);

		if (avdManagerPath is null) {
			Assert.Inconclusive ("avdmanager not found in SDK — cmdline-tools may not be installed.");
			return;
		}

		var env = new System.Collections.Generic.Dictionary<string, string> {
			{ EnvironmentVariableNames.JavaHome, jdkPath },
			{ EnvironmentVariableNames.AndroidHome, sdkPath },
		};

		var runner = new AvdManagerRunner (avdManagerPath, env);
		var avds = await runner.ListAvdsAsync ();

		Assert.IsNotNull (avds);
		Console.WriteLine ($"ListAvdsAsync returned {avds.Count} AVD(s)");
	}
}
