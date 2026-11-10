// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
public class AvdManagerRunnerTests
{
	[TestCase ("hw.gpu.enabled=no\nhw.gpu.mode=auto\n")]
	[TestCase (" hw.gpu.enabled = no \nhw.gpu.mode = off\n")]
	[TestCase ("hw.gpu.enabled=no\nhw.gpu.enabled=yes\nhw.gpu.mode=host\n")]
	[TestCase ("hw.gpu.enabled=yes\nhw.gpu.mode=auto\n")]
	[TestCase ("")]
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
		Assert.That (avd.CreationCommand, Is.EqualTo ("create avd -n test-avd -k system-image"));
	}

	[Test]
	public async Task GetOrCreateAvdAsync_ExistingAvd_PreservesConfiguration ()
	{
		const string config = "hw.gpu.enabled=no\r\nhw.gpu.mode=host\r\nhw.ramSize=4096\r\n";
		using var avd = new FakeAvdManager (config, existing: true);
		var existing = await avd.Runner.GetOrCreateAvdAsync ("TEST-AVD", "system-image");

		Assert.AreEqual (avd.AvdPath, existing.Path);
		Assert.AreEqual (config, File.ReadAllText (avd.ConfigPath));
		Assert.IsEmpty (avd.CreationCommand);
	}

	[Test]
	public async Task GetOrCreateAvdAsync_Force_EnablesGpu ()
	{
		using var avd = new FakeAvdManager ("hw.gpu.enabled=no\nhw.gpu.mode=off\n", existing: true);
		await avd.Runner.GetOrCreateAvdAsync ("test-avd", "system-image", force: true);

		CollectionAssert.AreEqual (new [] { "hw.gpu.enabled=yes", "hw.gpu.mode=auto" }, File.ReadAllLines (avd.ConfigPath));
		Assert.That (avd.CreationCommand, Does.Contain ("--force"));
	}

	[Test]
	public void GetOrCreateAvdAsync_MissingConfig_Throws ()
	{
		using var avd = new FakeAvdManager ("");
		File.Delete (avd.ConfigPath);

		Assert.ThrowsAsync<FileNotFoundException> (() => avd.Runner.GetOrCreateAvdAsync ("test-avd", "system-image"));
		Assert.IsFalse (File.Exists (avd.ConfigPath));
	}

	[Test]
	public void GetOrCreateAvdAsync_MissingPath_Throws ()
	{
		using var avd = new FakeAvdManager ("", reportPath: false);

		var exception = Assert.ThrowsAsync<InvalidOperationException> (() => avd.Runner.GetOrCreateAvdAsync ("test-avd", "system-image"));
		Assert.That (exception.Message, Does.Contain ("did not report a path"));
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
				Assert.Ignore ("Fake avdmanager tests use shell scripts and are not supported on Windows.");

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

	[Test]
	[Platform ("Linux,MacOsX")]
	public async Task GetOrCreateAvdAsync_FeedsNoClosesInputAndPreservesTheAvdArgument ()
	{
		var directory = Path.Combine (Path.GetTempPath (), $"avd-native-{Guid.NewGuid ():N}");
		Directory.CreateDirectory (directory);
		var executable = Path.Combine (directory, "avdmanager");
		try {
			File.WriteAllText (Path.Combine (directory, "config.ini"), "hw.ramSize=2048\n");
			File.WriteAllText (executable, """
				#!/bin/sh
				if [ "$1" = "create" ]; then
				    printf '%s\n' "$@" > "$0.arguments"
				    IFS= read -r answer
				    [ "$answer" = "no" ] || exit 7
				    [ -z "$(cat)" ] || exit 8
				    exit 0
				fi
				printf 'Name: AVD with spaces\nPath: %s\n' "$(dirname "$0")"
				""");
			if (!OperatingSystem.IsWindows ())
				File.SetUnixFileMode (executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			using var deadline = new CancellationTokenSource (TimeSpan.FromSeconds (5));
			var runner = new AvdManagerRunner (executable);
			var avd = await runner.GetOrCreateAvdAsync ("AVD with spaces", "system-images;android-37;google_apis;arm64-v8a",
				force: true, cancellationToken: deadline.Token);
			Assert.AreEqual ("AVD with spaces", avd.Name);
			CollectionAssert.AreEqual (new [] { "hw.ramSize=2048", "hw.gpu.enabled=yes", "hw.gpu.mode=auto" },
				File.ReadAllLines (Path.Combine (directory, "config.ini")));
			CollectionAssert.AreEqual (
				new [] { "create", "avd", "-n", "AVD with spaces", "-k", "system-images;android-37;google_apis;arm64-v8a", "--force" },
				File.ReadAllLines (executable + ".arguments"));
		} finally {
			File.Delete (executable + ".arguments");
			File.Delete (executable);
			File.Delete (Path.Combine (directory, "config.ini"));
			Directory.Delete (directory);
		}
	}

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

			var path = CommandLineToolsResolver.Find (tempDir, "avdmanager", OS.IsWindows ? ".bat" : "")?.Path;
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
			var path = CommandLineToolsResolver.Find (tempDir, "avdmanager", OS.IsWindows ? ".bat" : "")?.Path;
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
			var path = CommandLineToolsResolver.Find (tempDir, "avdmanager", OS.IsWindows ? ".bat" : "")?.Path;
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

			var path = CommandLineToolsResolver.Find (tempDir, "avdmanager", OS.IsWindows ? ".bat" : "")?.Path;
			Assert.That (path, Does.Contain ("latest"));
		} finally {
			Directory.Delete (tempDir, true);
		}
	}

	[Test]
	public void FindCmdlineTool_MissingSdk_ReturnsNull ()
	{
		var path = CommandLineToolsResolver.Find ("/nonexistent/path", "avdmanager", OS.IsWindows ? ".bat" : "")?.Path;
		Assert.IsNull (path);
	}

	[TestCase (null)]
	[TestCase ("")]
	[TestCase ("   ")]
	public void Constructor_InvalidPath_ThrowsArgumentException (string path)
	{
		Assert.Throws<ArgumentException> (() => new AvdManagerRunner (path));
	}

	[TestCase (null)]
	[TestCase ("")]
	[TestCase ("   ")]
	public void GetOrCreateAvdAsync_InvalidName_ThrowsArgumentException (string name)
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		Assert.ThrowsAsync<ArgumentException> (() => runner.GetOrCreateAvdAsync (name, "system-image"));
	}

	[TestCase (null)]
	[TestCase ("")]
	[TestCase (" \t ")]
	public void GetOrCreateAvdAsync_InvalidSystemImage_ThrowsArgumentException (string systemImage)
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		Assert.ThrowsAsync<ArgumentException> (() => runner.GetOrCreateAvdAsync ("test-avd", systemImage));
	}

	[TestCase (null)]
	[TestCase ("")]
	[TestCase (" \t ")]
	public void DeleteAvdAsync_InvalidName_ThrowsArgumentException (string name)
	{
		var runner = new AvdManagerRunner ("/fake/avdmanager");
		Assert.ThrowsAsync<ArgumentException> (() => runner.DeleteAvdAsync (name));
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
			var path = CommandLineToolsResolver.Find (tempDir, "avdmanager", OS.IsWindows ? ".bat" : "")?.Path;
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
