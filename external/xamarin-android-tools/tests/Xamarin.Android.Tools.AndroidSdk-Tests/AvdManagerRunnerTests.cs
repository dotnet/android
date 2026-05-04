// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
public class AvdManagerRunnerTests
{
	[Test]
	public void ParseAvdListOutput_MultipleAvds ()
	{
		var output =
			"Available Android Virtual Devices:\n" +
			"    Name: Pixel_7_API_35\n" +
			"    Device: pixel_7 (Google)\n" +
			"  Path: /Users/test/.android/avd/Pixel_7_API_35.avd\n" +
			"  Target: Google APIs (Google Inc.)\n" +
			"          Based on: Android 15 Tag/ABI: google_apis/arm64-v8a\n" +
			"---------\n" +
			"    Name: MAUI_Emulator\n" +
			"    Device: pixel_6 (Google)\n" +
			"  Path: /Users/test/.android/avd/MAUI_Emulator.avd\n" +
			"  Target: Google APIs (Google Inc.)\n" +
			"          Based on: Android 14 Tag/ABI: google_apis/x86_64\n";

		var avds = AvdManagerRunner.ParseAvdListOutput (output);

		Assert.AreEqual (2, avds.Count);

		Assert.AreEqual ("Pixel_7_API_35", avds [0].Name);
		Assert.AreEqual ("pixel_7 (Google)", avds [0].DeviceProfile);
		Assert.AreEqual ("/Users/test/.android/avd/Pixel_7_API_35.avd", avds [0].Path);

		Assert.AreEqual ("MAUI_Emulator", avds [1].Name);
		Assert.AreEqual ("pixel_6 (Google)", avds [1].DeviceProfile);
		Assert.AreEqual ("/Users/test/.android/avd/MAUI_Emulator.avd", avds [1].Path);
	}

	[Test]
	public void ParseAvdListOutput_WindowsNewlines ()
	{
		var output =
			"Available Android Virtual Devices:\r\n" +
			"    Name: Test_AVD\r\n" +
			"    Device: Nexus 5X (Google)\r\n" +
			"  Path: C:\\Users\\test\\.android\\avd\\Test_AVD.avd\r\n" +
			"  Target: Google APIs (Google Inc.)\r\n";

		var avds = AvdManagerRunner.ParseAvdListOutput (output);

		Assert.AreEqual (1, avds.Count);
		Assert.AreEqual ("Test_AVD", avds [0].Name);
		Assert.AreEqual ("Nexus 5X (Google)", avds [0].DeviceProfile);
		Assert.AreEqual ("C:\\Users\\test\\.android\\avd\\Test_AVD.avd", avds [0].Path);
	}

	[Test]
	public void ParseAvdListOutput_EmptyOutput ()
	{
		var avds = AvdManagerRunner.ParseAvdListOutput ("");
		Assert.AreEqual (0, avds.Count);
	}

	[Test]
	public void ParseAvdListOutput_NoAvds ()
	{
		var output = "Available Android Virtual Devices:\n";
		var avds = AvdManagerRunner.ParseAvdListOutput (output);
		Assert.AreEqual (0, avds.Count);
	}

	[Test]
	public void ParseAvdListOutput_SingleAvdNoDevice ()
	{
		var output =
			"    Name: Minimal_AVD\n" +
			"  Path: /home/user/.android/avd/Minimal_AVD.avd\n";

		var avds = AvdManagerRunner.ParseAvdListOutput (output);

		Assert.AreEqual (1, avds.Count);
		Assert.AreEqual ("Minimal_AVD", avds [0].Name);
		Assert.IsNull (avds [0].DeviceProfile);
		Assert.AreEqual ("/home/user/.android/avd/Minimal_AVD.avd", avds [0].Path);
	}

	[Test]
	public void ParseAvdListOutput_ReturnsIReadOnlyList ()
	{
		var avds = AvdManagerRunner.ParseAvdListOutput ("");
		Assert.IsInstanceOf<IReadOnlyList<AvdInfo>> (avds);
	}

	[Test]
	public void FindCmdlineTool_FindsVersionedDir ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), $"avd-test-{Path.GetRandomFileName ()}");
		var binDir = Path.Combine (tempDir, "cmdline-tools", "12.0", "bin");
		Directory.CreateDirectory (binDir);

		try {
			var avdMgrName = OS.IsWindows ? "avdmanager.bat" : "avdmanager";
			File.WriteAllText (Path.Combine (binDir, avdMgrName), "");

			var path = ProcessUtils.FindCmdlineTool (tempDir, "avdmanager", OS.IsWindows ? ".bat" : "");
			Assert.That (path, Does.Contain ("12.0"));
		} finally {
			Directory.Delete (tempDir, true);
		}
	}

	[Test]
	public void FindCmdlineTool_PrefersHigherVersion ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), $"avd-test-{Path.GetRandomFileName ()}");
		var avdMgrName = OS.IsWindows ? "avdmanager.bat" : "avdmanager";

		var binDir10 = Path.Combine (tempDir, "cmdline-tools", "10.0", "bin");
		var binDir12 = Path.Combine (tempDir, "cmdline-tools", "12.0", "bin");
		Directory.CreateDirectory (binDir10);
		Directory.CreateDirectory (binDir12);
		File.WriteAllText (Path.Combine (binDir10, avdMgrName), "");
		File.WriteAllText (Path.Combine (binDir12, avdMgrName), "");

		try {
			var path = ProcessUtils.FindCmdlineTool (tempDir, "avdmanager", OS.IsWindows ? ".bat" : "");
			Assert.That (path, Does.Contain ("12.0"));
		} finally {
			Directory.Delete (tempDir, true);
		}
	}

	[Test]
	public void FindCmdlineTool_HandlesPreReleaseVersionDir ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), $"avd-test-{Path.GetRandomFileName ()}");
		var avdMgrName = OS.IsWindows ? "avdmanager.bat" : "avdmanager";

		// Pre-release "13.0-rc1" should be preferred over "12.0"
		var binDir12 = Path.Combine (tempDir, "cmdline-tools", "12.0", "bin");
		var binDirRc = Path.Combine (tempDir, "cmdline-tools", "13.0-rc1", "bin");
		Directory.CreateDirectory (binDir12);
		Directory.CreateDirectory (binDirRc);
		File.WriteAllText (Path.Combine (binDir12, avdMgrName), "");
		File.WriteAllText (Path.Combine (binDirRc, avdMgrName), "");

		try {
			var path = ProcessUtils.FindCmdlineTool (tempDir, "avdmanager", OS.IsWindows ? ".bat" : "");
			Assert.That (path, Does.Contain ("13.0-rc1"));
		} finally {
			Directory.Delete (tempDir, true);
		}
	}

	[Test]
	public void FindCmdlineTool_PrefersLatest ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), $"avd-test-{Path.GetRandomFileName ()}");
		var binDir = Path.Combine (tempDir, "cmdline-tools", "latest", "bin");
		Directory.CreateDirectory (binDir);

		try {
			var avdMgrName = OS.IsWindows ? "avdmanager.bat" : "avdmanager";
			File.WriteAllText (Path.Combine (binDir, avdMgrName), "");

			var path = ProcessUtils.FindCmdlineTool (tempDir, "avdmanager", OS.IsWindows ? ".bat" : "");
			Assert.That (path, Does.Contain ("latest"));
		} finally {
			Directory.Delete (tempDir, true);
		}
	}

	[Test]
	public void FindCmdlineTool_MissingSdk_ReturnsNull ()
	{
		var path = ProcessUtils.FindCmdlineTool ("/nonexistent/path", "avdmanager", OS.IsWindows ? ".bat" : "");
		Assert.IsNull (path);
	}

	[Test]
	public void Constructor_NullPath_ThrowsArgumentException ()
	{
		Assert.Throws<ArgumentException> (() => new AvdManagerRunner (null!));
	}

	[Test]
	public void Constructor_EmptyPath_ThrowsArgumentException ()
	{
		Assert.Throws<ArgumentException> (() => new AvdManagerRunner (""));
	}

	[Test]
	public void Constructor_WhitespacePath_ThrowsArgumentException ()
	{
		Assert.Throws<ArgumentException> (() => new AvdManagerRunner ("   "));
	}

	[Test]
	public void Constructor_AcceptsEnvironmentVariables ()
	{
		var env = new Dictionary<string, string> { { "ANDROID_HOME", "/test/sdk" } };
		var runner = new AvdManagerRunner ("/fake/avdmanager", env);
		Assert.IsNotNull (runner);
	}

	[Test]
	public void GetOrCreateAvdAsync_NullName_ThrowsArgumentException ()
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		Assert.ThrowsAsync<ArgumentException> (() => runner.GetOrCreateAvdAsync (null!, "system-image"));
	}

	[Test]
	public void GetOrCreateAvdAsync_EmptyName_ThrowsArgumentException ()
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		Assert.ThrowsAsync<ArgumentException> (() => runner.GetOrCreateAvdAsync ("", "system-image"));
	}

	[Test]
	public void GetOrCreateAvdAsync_WhitespaceName_ThrowsArgumentException ()
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		Assert.ThrowsAsync<ArgumentException> (() => runner.GetOrCreateAvdAsync ("   ", "system-image"));
	}

	[Test]
	public void GetOrCreateAvdAsync_NullSystemImage_ThrowsArgumentException ()
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		Assert.ThrowsAsync<ArgumentException> (() => runner.GetOrCreateAvdAsync ("test-avd", null!));
	}

	[Test]
	public void GetOrCreateAvdAsync_EmptySystemImage_ThrowsArgumentException ()
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		Assert.ThrowsAsync<ArgumentException> (() => runner.GetOrCreateAvdAsync ("test-avd", ""));
	}

	[Test]
	public void GetOrCreateAvdAsync_WhitespaceSystemImage_ThrowsArgumentException ()
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		Assert.ThrowsAsync<ArgumentException> (() => runner.GetOrCreateAvdAsync ("test-avd", " \t "));
	}

	[Test]
	public void DeleteAvdAsync_NullName_ThrowsArgumentException ()
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		Assert.ThrowsAsync<ArgumentException> (() => runner.DeleteAvdAsync (null!));
	}

	[Test]
	public void DeleteAvdAsync_EmptyName_ThrowsArgumentException ()
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		Assert.ThrowsAsync<ArgumentException> (() => runner.DeleteAvdAsync (""));
	}

	[Test]
	public void DeleteAvdAsync_WhitespaceName_ThrowsArgumentException ()
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		Assert.ThrowsAsync<ArgumentException> (() => runner.DeleteAvdAsync (" \t "));
	}

	[Test]
	public void FindCmdlineTool_PrefersStableOverPreRelease ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), $"avd-test-{Path.GetRandomFileName ()}");
		var avdMgrName = OS.IsWindows ? "avdmanager.bat" : "avdmanager";

		// Both "13.0" (stable) and "13.0-rc1" (prerelease) exist — stable should win
		var binDirStable = Path.Combine (tempDir, "cmdline-tools", "13.0", "bin");
		var binDirRc = Path.Combine (tempDir, "cmdline-tools", "13.0-rc1", "bin");
		Directory.CreateDirectory (binDirStable);
		Directory.CreateDirectory (binDirRc);
		File.WriteAllText (Path.Combine (binDirStable, avdMgrName), "");
		File.WriteAllText (Path.Combine (binDirRc, avdMgrName), "");

		try {
			var path = ProcessUtils.FindCmdlineTool (tempDir, "avdmanager", OS.IsWindows ? ".bat" : "");
			Assert.That (path, Does.Contain (Path.Combine ("13.0", "bin")));
		} finally {
			Directory.Delete (tempDir, true);
		}
	}

	[Test]
	public void ParseCompactDeviceListOutput_MultipleProfiles ()
	{
		var output =
			"automotive_1024p_landscape\n" +
			"pixel_7\n" +
			"Nexus 5X\n" +
			"Galaxy Nexus\n";

		var profiles = AvdManagerRunner.ParseCompactDeviceListOutput (output);

		Assert.AreEqual (4, profiles.Count);
		Assert.AreEqual ("automotive_1024p_landscape", profiles [0].Id);
		Assert.AreEqual ("pixel_7", profiles [1].Id);
		Assert.AreEqual ("Nexus 5X", profiles [2].Id);
		Assert.AreEqual ("Galaxy Nexus", profiles [3].Id);
	}

	[Test]
	public void ParseCompactDeviceListOutput_EmptyOutput ()
	{
		var profiles = AvdManagerRunner.ParseCompactDeviceListOutput ("");
		Assert.AreEqual (0, profiles.Count);
	}

	[Test]
	public void ParseCompactDeviceListOutput_WindowsNewlines ()
	{
		var output =
			"pixel_fold\r\n" +
			"pixel_9_pro\r\n";

		var profiles = AvdManagerRunner.ParseCompactDeviceListOutput (output);

		Assert.AreEqual (2, profiles.Count);
		Assert.AreEqual ("pixel_fold", profiles [0].Id);
		Assert.AreEqual ("pixel_9_pro", profiles [1].Id);
	}

	[Test]
	public void ParseCompactDeviceListOutput_SkipsBlankLines ()
	{
		var output = "\n\npixel_7\n\n";

		var profiles = AvdManagerRunner.ParseCompactDeviceListOutput (output);

		Assert.AreEqual (1, profiles.Count);
		Assert.AreEqual ("pixel_7", profiles [0].Id);
	}

	[Test]
	public void ParseCompactDeviceListOutput_ReturnsIReadOnlyList ()
	{
		var profiles = AvdManagerRunner.ParseCompactDeviceListOutput ("");
		Assert.IsInstanceOf<IReadOnlyList<AvdDeviceProfile>> (profiles);
	}
}
