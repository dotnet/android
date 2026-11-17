// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Tools.Tests;

[TestClass]
public class AvdManagerRunnerTests
{
	[TestMethod]
	[DataRow ("hw.gpu.enabled=no\nhw.gpu.mode=auto\n")]
	[DataRow (" hw.gpu.enabled = no \nhw.gpu.mode = off\n")]
	[DataRow ("hw.gpu.enabled=no\nhw.gpu.enabled=yes\nhw.gpu.mode=host\n")]
	[DataRow ("hw.gpu.enabled=yes\nhw.gpu.mode=auto\n")]
	[DataRow ("")]
	public async Task GetOrCreateAvdAsync_NewAvd_EnablesGpu (string gpuConfig)
	{
		using var avd = new FakeAvdManager ("# Keep this comment\nhw.ramSize=2048\nhw.gpu.enabled.extra=no\n" + gpuConfig);
		var created = await avd.Runner.GetOrCreateAvdAsync ("test-avd", "system-image");

		Assert.AreEqual (avd.AvdPath, created.Path);
		CollectionAssert.AreEqual (new [] {
			"# Keep this comment",
			"hw.ramSize=2048",
			"hw.gpu.enabled.extra=no",
			"hw.gpu.enabled=yes",
			"hw.gpu.mode=auto",
		}, File.ReadAllLines (avd.ConfigPath));
		Assert.AreEqual ("create avd -n test-avd -k system-image", avd.CreationCommand);
	}

	[TestMethod]
	public async Task GetOrCreateAvdAsync_ExistingAvd_PreservesConfiguration ()
	{
		const string config = "hw.gpu.enabled=no\r\nhw.gpu.mode=host\r\nhw.ramSize=4096\r\n";
		using var avd = new FakeAvdManager (config, existing: true);
		var existing = await avd.Runner.GetOrCreateAvdAsync ("TEST-AVD", "system-image");

		Assert.AreEqual (avd.AvdPath, existing.Path);
		Assert.AreEqual (config, File.ReadAllText (avd.ConfigPath));
		Assert.IsEmpty (avd.CreationCommand);
	}

	[TestMethod]
	public async Task GetOrCreateAvdAsync_Force_EnablesGpu ()
	{
		using var avd = new FakeAvdManager ("hw.gpu.enabled=no\nhw.gpu.mode=off\n", existing: true);
		await avd.Runner.GetOrCreateAvdAsync ("test-avd", "system-image", force: true);

		CollectionAssert.AreEqual (new [] { "hw.gpu.enabled=yes", "hw.gpu.mode=auto" }, File.ReadAllLines (avd.ConfigPath));
		Assert.Contains ("--force", avd.CreationCommand);
	}

	[TestMethod]
	public async Task GetOrCreateAvdAsync_MissingConfig_Throws ()
	{
		using var avd = new FakeAvdManager ("");
		File.Delete (avd.ConfigPath);

		await Assert.ThrowsExactlyAsync<FileNotFoundException> (() => avd.Runner.GetOrCreateAvdAsync ("test-avd", "system-image"));
		Assert.IsFalse (File.Exists (avd.ConfigPath));
	}

	[TestMethod]
	public async Task GetOrCreateAvdAsync_MissingPath_Throws ()
	{
		using var avd = new FakeAvdManager ("", reportPath: false);

		var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException> (() => avd.Runner.GetOrCreateAvdAsync ("test-avd", "system-image"));
		Assert.Contains ("did not report a path", exception.Message);
	}

	sealed class FakeAvdManager : IDisposable
	{
		readonly string directory = Path.Combine (Path.GetTempPath (), $"avd-test-{Path.GetRandomFileName ()}");

		public string AvdPath { get; }
		public string ConfigPath => Path.Combine (AvdPath, "config.ini");
		string MarkerPath => Path.Combine (directory, "created");
		public string CreationCommand => File.ReadAllText (MarkerPath).Trim ();
		public AvdManagerRunner Runner { get; }

		public FakeAvdManager (string config, bool existing = false, bool reportPath = true)
		{
			if (OS.IsWindows)
				Assert.Inconclusive ("Fake avdmanager tests use shell scripts and are not supported on Windows.");

			AvdPath = Path.Combine (directory, "custom location", "actual.avd");
			Directory.CreateDirectory (AvdPath);
			File.WriteAllText (ConfigPath, config);
			if (existing)
				File.WriteAllText (MarkerPath, "");

			var scriptPath = Path.Combine (directory, "avdmanager");
			File.WriteAllText (scriptPath, """
				#!/bin/sh
				if [ "$1" = create ]; then
				  read answer
				  echo "$*" > "$FAKE_MARKER"
				  exit 0
				fi
				if [ -f "$FAKE_MARKER" ]; then
				  echo '    Name: test-avd'
				  if [ -n "$FAKE_AVD_PATH" ]; then
				    echo "  Path: $FAKE_AVD_PATH"
				  fi
				fi
				""");
			if (!OperatingSystem.IsWindows ())
				File.SetUnixFileMode (scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

			Runner = new AvdManagerRunner (scriptPath, new Dictionary<string, string> {
				["FAKE_MARKER"] = MarkerPath,
				["FAKE_AVD_PATH"] = reportPath ? AvdPath : "",
			});
		}

		public void Dispose () => Directory.Delete (directory, true);
	}

	[TestMethod]
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

	[TestMethod]
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

	[TestMethod]
	public void ParseAvdListOutput_EmptyOutput ()
	{
		var avds = AvdManagerRunner.ParseAvdListOutput ("");
		Assert.AreEqual (0, avds.Count);
	}

	[TestMethod]
	public void ParseAvdListOutput_NoAvds ()
	{
		var output = "Available Android Virtual Devices:\n";
		var avds = AvdManagerRunner.ParseAvdListOutput (output);
		Assert.AreEqual (0, avds.Count);
	}

	[TestMethod]
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

	[TestMethod]
	public void ParseAvdListOutput_ReturnsIReadOnlyList ()
	{
		var avds = AvdManagerRunner.ParseAvdListOutput ("");
		Assert.IsInstanceOfType<IReadOnlyList<AvdInfo>> (avds);
	}

	[TestMethod]
	public void FindCmdlineTool_FindsVersionedDir ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), $"avd-test-{Path.GetRandomFileName ()}");
		var binDir = Path.Combine (tempDir, "cmdline-tools", "12.0", "bin");
		Directory.CreateDirectory (binDir);

		try {
			var avdMgrName = OS.IsWindows ? "avdmanager.bat" : "avdmanager";
			File.WriteAllText (Path.Combine (binDir, avdMgrName), "");

			var path = ProcessUtils.FindCmdlineTool (tempDir, "avdmanager", OS.IsWindows ? ".bat" : "");
			Assert.Contains ("12.0", path);
		} finally {
			Directory.Delete (tempDir, true);
		}
	}

	[TestMethod]
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
			Assert.Contains ("12.0", path);
		} finally {
			Directory.Delete (tempDir, true);
		}
	}

	[TestMethod]
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
			Assert.Contains ("13.0-rc1", path);
		} finally {
			Directory.Delete (tempDir, true);
		}
	}

	[TestMethod]
	public void FindCmdlineTool_PrefersLatest ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), $"avd-test-{Path.GetRandomFileName ()}");
		var binDir = Path.Combine (tempDir, "cmdline-tools", "latest", "bin");
		Directory.CreateDirectory (binDir);

		try {
			var avdMgrName = OS.IsWindows ? "avdmanager.bat" : "avdmanager";
			File.WriteAllText (Path.Combine (binDir, avdMgrName), "");

			var path = ProcessUtils.FindCmdlineTool (tempDir, "avdmanager", OS.IsWindows ? ".bat" : "");
			Assert.Contains ("latest", path);
		} finally {
			Directory.Delete (tempDir, true);
		}
	}

	[TestMethod]
	public void FindCmdlineTool_MissingSdk_ReturnsNull ()
	{
		var path = ProcessUtils.FindCmdlineTool ("/nonexistent/path", "avdmanager", OS.IsWindows ? ".bat" : "");
		Assert.IsNull (path);
	}

	[TestMethod]
	[DataRow (null)]
	[DataRow ("")]
	[DataRow ("   ")]
	public void Constructor_InvalidPath_ThrowsArgumentException (string path)
	{
		Assert.ThrowsExactly<ArgumentException> (() => new AvdManagerRunner (path));
	}

	[TestMethod]
	[DataRow (null)]
	[DataRow ("")]
	[DataRow ("   ")]
	public async Task GetOrCreateAvdAsync_InvalidName_ThrowsArgumentException (string name)
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		await Assert.ThrowsExactlyAsync<ArgumentException> (() => runner.GetOrCreateAvdAsync (name, "system-image"));
	}

	[TestMethod]
	[DataRow (null)]
	[DataRow ("")]
	[DataRow (" \t ")]
	public async Task GetOrCreateAvdAsync_InvalidSystemImage_ThrowsArgumentException (string systemImage)
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		await Assert.ThrowsExactlyAsync<ArgumentException> (() => runner.GetOrCreateAvdAsync ("test-avd", systemImage));
	}

	[TestMethod]
	[DataRow (null)]
	[DataRow ("")]
	[DataRow (" \t ")]
	public async Task DeleteAvdAsync_InvalidName_ThrowsArgumentException (string name)
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		await Assert.ThrowsExactlyAsync<ArgumentException> (() => runner.DeleteAvdAsync (name));
	}

	[TestMethod]
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
			Assert.Contains (Path.Combine ("13.0", "bin"), path);
		} finally {
			Directory.Delete (tempDir, true);
		}
	}

	[TestMethod]
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

	[TestMethod]
	public void ParseCompactDeviceListOutput_EmptyOutput ()
	{
		var profiles = AvdManagerRunner.ParseCompactDeviceListOutput ("");
		Assert.AreEqual (0, profiles.Count);
	}

	[TestMethod]
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

	[TestMethod]
	public void ParseCompactDeviceListOutput_SkipsBlankLines ()
	{
		var output = "\n\npixel_7\n\n";

		var profiles = AvdManagerRunner.ParseCompactDeviceListOutput (output);

		Assert.AreEqual (1, profiles.Count);
		Assert.AreEqual ("pixel_7", profiles [0].Id);
	}

	[TestMethod]
	public void ParseCompactDeviceListOutput_ReturnsIReadOnlyList ()
	{
		var profiles = AvdManagerRunner.ParseCompactDeviceListOutput ("");
		Assert.IsInstanceOfType<IReadOnlyList<AvdDeviceProfile>> (profiles);
	}
}
