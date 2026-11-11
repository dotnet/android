// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Tools.Tests;

[TestClass]
public class EmulatorRunnerTests
{
	[TestMethod]
	public void ParseListAvdsOutput_MultipleAvds ()
	{
		var output = "Pixel_7_API_35\nMAUI_Emulator\nNexus_5X\n";

		var avds = EmulatorRunner.ParseListAvdsOutput (output);

		Assert.AreEqual (3, avds.Count);
		Assert.AreEqual ("Pixel_7_API_35", avds [0]);
		Assert.AreEqual ("MAUI_Emulator", avds [1]);
		Assert.AreEqual ("Nexus_5X", avds [2]);
	}

	[TestMethod]
	public void ParseListAvdsOutput_EmptyOutput ()
	{
		var avds = EmulatorRunner.ParseListAvdsOutput ("");
		Assert.AreEqual (0, avds.Count);
	}

	[TestMethod]
	public void ParseListAvdsOutput_WindowsNewlines ()
	{
		var output = "Pixel_7_API_35\r\nMAUI_Emulator\r\n";

		var avds = EmulatorRunner.ParseListAvdsOutput (output);

		Assert.AreEqual (2, avds.Count);
		Assert.AreEqual ("Pixel_7_API_35", avds [0]);
		Assert.AreEqual ("MAUI_Emulator", avds [1]);
	}

	[TestMethod]
	public void ParseListAvdsOutput_BlankLines ()
	{
		var output = "\nPixel_7_API_35\n\n\nMAUI_Emulator\n\n";

		var avds = EmulatorRunner.ParseListAvdsOutput (output);

		Assert.AreEqual (2, avds.Count);
	}

	[TestMethod]
	[DataRow (null)]
	[DataRow ("")]
	[DataRow ("   ")]
	public void Constructor_ThrowsOnInvalidPath (string path)
	{
		Assert.ThrowsExactly<ArgumentException> (() => new EmulatorRunner (path));
	}

	[TestMethod]
	[DataRow (null)]
	[DataRow ("")]
	[DataRow ("   ")]
	public void LaunchEmulator_ThrowsOnInvalidAvdName (string avdName)
	{
		var runner = new EmulatorRunner ("/fake/emulator");
		Assert.ThrowsExactly<ArgumentException> (() => runner.LaunchEmulator (avdName));
	}

	[TestMethod]
	public async Task CommandsCopySuppliedProcessEnvironment ()
	{
		var (tempDir, emuPath) = CreateFakeEmulatorSdk ();
		var directory = Directory.CreateDirectory (Path.Combine (tempDir, "working directory")).FullName;
		var marker = Path.Combine (directory, "environment.txt");
		WriteEnvironmentProbe (emuPath, "environment.txt");
		try {
			var startInfo = new ProcessStartInfo { WorkingDirectory = directory };
			startInfo.EnvironmentVariables.Clear ();
			if (Environment.GetEnvironmentVariable ("SystemRoot") is string systemRoot)
				startInfo.EnvironmentVariables ["SystemRoot"] = systemRoot;
			startInfo.EnvironmentVariables ["EMULATOR_TEST_VALUE"] = "original";
			var runner = EmulatorRunner.CreateWithProcessEnvironment (emuPath, startInfo);
			startInfo.WorkingDirectory = Path.GetTempPath ();
			startInfo.EnvironmentVariables ["EMULATOR_TEST_VALUE"] = "changed";

			CollectionAssert.AreEqual (new [] { "TestAVD" }, await runner.ListAvdNamesAsync ());
			CollectionAssert.AreEqual (new [] { "original", "" }, File.ReadAllLines (marker));
			File.Delete (marker);

			using var process = runner.LaunchEmulator ("TestAVD");
			Assert.IsTrue (process.WaitForExit (5000), "The fake emulator should exit promptly.");
			Assert.AreEqual (0, process.ExitCode);
			CollectionAssert.AreEqual (new [] { "original", "" }, File.ReadAllLines (marker));
		} finally {
			DeleteDirectoryWithRetry (tempDir);
		}
	}

	[TestMethod]
	public async Task ConstructorPreservesInheritedEnvironmentAndWorkingDirectory ()
	{
		var (tempDir, emuPath) = CreateFakeEmulatorSdk ();
		var markerName = $"emulator-environment-{Guid.NewGuid ():N}.txt";
		var marker = Path.Combine (Directory.GetCurrentDirectory (), markerName);
		WriteEnvironmentProbe (emuPath, markerName);
		try {
			var runner = new EmulatorRunner (emuPath, new Dictionary<string, string> {
				["EMULATOR_TEST_VALUE"] = "override",
			});
			var inheritedValue = Environment.GetEnvironmentVariable (OS.IsWindows ? "USERNAME" : "HOME") ?? "";
			var expected = new [] { "override", inheritedValue };

			CollectionAssert.AreEqual (new [] { "TestAVD" }, await runner.ListAvdNamesAsync ());
			CollectionAssert.AreEqual (expected, File.ReadAllLines (marker));
			File.Delete (marker);

			using var process = runner.LaunchEmulator ("TestAVD");
			Assert.IsTrue (process.WaitForExit (5000), "The fake emulator should exit promptly.");
			Assert.AreEqual (0, process.ExitCode);
			CollectionAssert.AreEqual (expected, File.ReadAllLines (marker));
		} finally {
			File.Delete (marker);
			DeleteDirectoryWithRetry (tempDir);
		}
	}

	[TestMethod]
	public async Task AlreadyOnlineDevice_PassesThrough ()
	{
		var devices = new List<AdbDeviceInfo> {
			new AdbDeviceInfo {
				Serial = "emulator-5554",
				Type = AdbDeviceType.Emulator,
				Status = AdbDeviceStatus.Online,
				AvdName = "Pixel_7_API_35",
			},
		};

		var mockAdb = new MockAdbRunner (devices);
		var runner = new EmulatorRunner ("/fake/emulator");

		var result = await runner.BootEmulatorAsync ("emulator-5554", mockAdb);

		Assert.IsTrue (result.Success);
		Assert.AreEqual ("emulator-5554", result.Serial);
		Assert.IsNull (result.ErrorMessage);
	}

	[TestMethod]
	public async Task AvdAlreadyRunning_WaitsForFullBoot ()
	{
		var devices = new List<AdbDeviceInfo> {
			new AdbDeviceInfo {
				Serial = "emulator-5554",
				Type = AdbDeviceType.Emulator,
				Status = AdbDeviceStatus.Online,
				AvdName = "Pixel_7_API_35",
			},
		};

		var mockAdb = new MockAdbRunner (devices);
		mockAdb.ShellProperties ["sys.boot_completed"] = "1";
		mockAdb.ShellCommands ["pm path android"] = "package:/system/framework/framework-res.apk";

		var runner = new EmulatorRunner ("/fake/emulator");
		var options = new EmulatorBootOptions { BootTimeout = TimeSpan.FromSeconds (5), PollInterval = TimeSpan.FromMilliseconds (50) };

		var result = await runner.BootEmulatorAsync ("Pixel_7_API_35", mockAdb, options);

		Assert.IsTrue (result.Success);
		Assert.AreEqual ("emulator-5554", result.Serial);
	}

	[TestMethod]
	public async Task BootEmulator_AppearsAfterPolling ()
	{
		var devices = new List<AdbDeviceInfo> ();
		var mockAdb = new MockAdbRunner (devices);
		mockAdb.ShellProperties ["sys.boot_completed"] = "1";
		mockAdb.ShellCommands ["pm path android"] = "package:/system/framework/framework-res.apk";

		var (tempDir, emuPath) = CreateFakeEmulatorSdk ();
		var launchedPath = Path.Combine (tempDir, "launched");
		if (OS.IsWindows) {
			File.WriteAllText (emuPath, $"@echo off\r\necho %* > \"{launchedPath}\"\r\nexit /b 0\r\n");
		} else {
			File.WriteAllText (emuPath, $"#!/bin/sh\necho \"$@\" > \"{launchedPath}\"\n");
		}

		int pollCount = 0;
		mockAdb.OnListDevices = () => {
			pollCount++;
			if (pollCount >= 2 && File.Exists (launchedPath) && IsFileUnlocked (emuPath)) {
				devices.Add (new AdbDeviceInfo {
					Serial = "emulator-5554",
					Type = AdbDeviceType.Emulator,
					Status = AdbDeviceStatus.Online,
					AvdName = "Pixel_7_API_35",
				});
			}
		};

		try {
			var runner = new EmulatorRunner (emuPath);
			var options = new EmulatorBootOptions {
				BootTimeout = TimeSpan.FromSeconds (10),
				PollInterval = TimeSpan.FromMilliseconds (50),
				ColdBoot = true,
				AdditionalArgs = new List<string> { "-gpu", "auto", "-no-audio" },
			};

			var result = await runner.BootEmulatorAsync ("Pixel_7_API_35", mockAdb, options);

			Assert.IsTrue (result.Success);
			Assert.AreEqual ("emulator-5554", result.Serial);
			Assert.IsTrue (pollCount >= 2);
			Assert.IsTrue (await WaitForFileAsync (launchedPath, TimeSpan.FromSeconds (5)), "The fake emulator should log its arguments.");
			var logged = File.ReadAllText (launchedPath);
			Assert.Contains ("-avd Pixel_7_API_35", logged);
			Assert.Contains ("-no-snapshot-load", logged);
			Assert.Contains ("-gpu auto -no-audio", logged);
		} finally {
			DeleteDirectoryWithRetry (tempDir);
		}
	}

	[TestMethod]
	public async Task LaunchFailure_ReturnsError ()
	{
		var devices = new List<AdbDeviceInfo> ();
		var mockAdb = new MockAdbRunner (devices);

		// Nonexistent path → LaunchAvd throws → error result
		var runner = new EmulatorRunner ("/nonexistent/emulator");
		var options = new EmulatorBootOptions { BootTimeout = TimeSpan.FromSeconds (2) };

		var result = await runner.BootEmulatorAsync ("Pixel_7_API_35", mockAdb, options);

		Assert.IsFalse (result.Success);
		Assert.AreEqual (EmulatorBootErrorKind.LaunchFailed, result.ErrorKind);
	}

	[TestMethod]
	public async Task BootTimeout_BootCompletedNeverReaches1 ()
	{
		var devices = new List<AdbDeviceInfo> {
			new AdbDeviceInfo {
				Serial = "emulator-5554",
				Type = AdbDeviceType.Emulator,
				Status = AdbDeviceStatus.Online,
				AvdName = "Pixel_7_API_35",
			},
		};

		var mockAdb = new MockAdbRunner (devices);
		// boot_completed never returns "1"
		mockAdb.ShellProperties ["sys.boot_completed"] = "0";

		var runner = new EmulatorRunner ("/fake/emulator");
		var options = new EmulatorBootOptions {
			BootTimeout = TimeSpan.FromMilliseconds (200),
			PollInterval = TimeSpan.FromMilliseconds (50),
		};

		var result = await runner.BootEmulatorAsync ("Pixel_7_API_35", mockAdb, options);

		Assert.IsFalse (result.Success);
		Assert.IsNotNull (result.ErrorMessage);
		Assert.Contains ("Timed out", result.ErrorMessage);
	}

	[TestMethod]
	public async Task BootEmulatorAsync_InvalidBootTimeout_Throws ()
	{
		var runner = new EmulatorRunner ("/fake/emulator");
		var mockAdb = new MockAdbRunner (new List<AdbDeviceInfo> ());
		var options = new EmulatorBootOptions { BootTimeout = TimeSpan.Zero };

		await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException> (() =>
			runner.BootEmulatorAsync ("test", mockAdb, options));
	}

	[TestMethod]
	public async Task BootEmulatorAsync_InvalidPollInterval_Throws ()
	{
		var runner = new EmulatorRunner ("/fake/emulator");
		var mockAdb = new MockAdbRunner (new List<AdbDeviceInfo> ());
		var options = new EmulatorBootOptions { PollInterval = TimeSpan.FromMilliseconds (-1) };

		await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException> (() =>
			runner.BootEmulatorAsync ("test", mockAdb, options));
	}

	[TestMethod]
	public async Task MultipleEmulators_FindsCorrectAvd ()
	{
		var devices = new List<AdbDeviceInfo> {
			new AdbDeviceInfo {
				Serial = "emulator-5554",
				Type = AdbDeviceType.Emulator,
				Status = AdbDeviceStatus.Online,
				AvdName = "Pixel_5_API_30",
			},
			new AdbDeviceInfo {
				Serial = "emulator-5556",
				Type = AdbDeviceType.Emulator,
				Status = AdbDeviceStatus.Online,
				AvdName = "Pixel_7_API_35",
			},
			new AdbDeviceInfo {
				Serial = "emulator-5558",
				Type = AdbDeviceType.Emulator,
				Status = AdbDeviceStatus.Online,
				AvdName = "Nexus_5X_API_28",
			},
		};

		var mockAdb = new MockAdbRunner (devices);
		mockAdb.ShellProperties ["sys.boot_completed"] = "1";
		mockAdb.ShellCommands ["pm path android"] = "package:/system/framework/framework-res.apk";

		var runner = new EmulatorRunner ("/fake/emulator");
		var options = new EmulatorBootOptions { BootTimeout = TimeSpan.FromSeconds (5), PollInterval = TimeSpan.FromMilliseconds (50) };

		var result = await runner.BootEmulatorAsync ("Pixel_7_API_35", mockAdb, options);

		Assert.IsTrue (result.Success);
		Assert.AreEqual ("emulator-5556", result.Serial, "Should find the correct AVD among multiple emulators");
	}

	[TestMethod]
	public async Task AlreadyOnlinePhysicalDevice_PassesThrough ()
	{
		// Physical devices have non-emulator serials (e.g., USB serial numbers).
		// BootEmulatorAsync should recognise them as already-online devices and
		// return immediately without attempting to launch an emulator.
		var devices = new List<AdbDeviceInfo> {
			new AdbDeviceInfo {
				Serial = "0A041FDD400327",
				Type = AdbDeviceType.Device,
				Status = AdbDeviceStatus.Online,
			},
		};

		var mockAdb = new MockAdbRunner (devices);
		var runner = new EmulatorRunner ("/fake/emulator");

		var result = await runner.BootEmulatorAsync ("0A041FDD400327", mockAdb);

		Assert.IsTrue (result.Success);
		Assert.AreEqual ("0A041FDD400327", result.Serial);
		Assert.IsNull (result.ErrorMessage);
	}

	[TestMethod]
	public async Task CancellationToken_AbortsBoot ()
	{
		// Verify that cancelling the token during the polling phase causes
		// BootEmulatorAsync to return promptly rather than blocking for the
		// full BootTimeout duration.
		var (tempDir, emuPath) = CreateFakeEmulatorSdk ();

		try {
			var devices = new List<AdbDeviceInfo> ();
			var mockAdb = new MockAdbRunner (devices);

			var runner = new EmulatorRunner (emuPath);
			var options = new EmulatorBootOptions {
				BootTimeout = TimeSpan.FromSeconds (30),
				PollInterval = TimeSpan.FromMilliseconds (50),
			};

			using var cts = new CancellationTokenSource ();
			cts.CancelAfter (TimeSpan.FromMilliseconds (300));

			var sw = Stopwatch.StartNew ();
			try {
				await runner.BootEmulatorAsync ("Nonexistent_AVD", mockAdb, options, cts.Token);
				Assert.Fail ("Should have thrown OperationCanceledException");
			} catch (OperationCanceledException) {
				sw.Stop ();
				// Should abort well before the 30s BootTimeout
				Assert.IsLessThan (5, sw.Elapsed.TotalSeconds,
					"Cancellation should abort within a few seconds, not wait for full timeout");
			}
		} finally {
			DeleteDirectoryWithRetry (tempDir);
		}
	}

	[TestMethod]
	public async Task BootEmulatorAsync_NullAdbRunner_Throws ()
	{
		var runner = new EmulatorRunner ("/fake/emulator");

		await Assert.ThrowsExactlyAsync<ArgumentNullException> (() =>
			runner.BootEmulatorAsync ("test", null!));
	}

	[TestMethod]
	public async Task BootEmulatorAsync_EmptyDeviceName_Throws ()
	{
		var runner = new EmulatorRunner ("/fake/emulator");
		var mockAdb = new MockAdbRunner (new List<AdbDeviceInfo> ());

		await Assert.ThrowsExactlyAsync<ArgumentException> (() =>
			runner.BootEmulatorAsync ("", mockAdb));
	}

	[TestMethod]
	[OSCondition (OperatingSystems.Linux | OperatingSystems.OSX)]
	public async Task LaunchEmulator_SurvivesSigint ()
	{
		var (tempDir, emuPath) = CreateFakeEmulatorSdk (keepRunning: true);
		Process? process = null;
		try {
			var runner = new EmulatorRunner (emuPath);
			process = runner.LaunchEmulator ("TestAVD");

			// Process.Start can return before the launcher shell installs its SIGINT trap.
			Assert.IsTrue (await WaitForFileAsync (emuPath + ".ready", TimeSpan.FromSeconds (5)),
				"The fake emulator should reach its entry point before SIGINT is sent");
			Assert.IsFalse (process.HasExited, "Process should be running after launch");

			// Send SIGINT to the emulator process
			var killPsi = ProcessUtils.CreateProcessStartInfo ("kill", "-INT", process.Id.ToString ());
			using var kill = new Process { StartInfo = killPsi };
			kill.Start ();
			Assert.IsTrue (kill.WaitForExit (5000), "kill command should exit promptly");
			Assert.AreEqual (0, kill.ExitCode, "kill -INT should succeed");

			Assert.IsFalse (process.WaitForExit (500), "Emulator process should survive SIGINT");
		} finally {
			try { process?.Kill (); process?.WaitForExit (5000); } catch { }
			process?.Dispose ();
			Directory.Delete (tempDir, true);
		}
	}

	[TestMethod]
	public async Task InvalidEmulatorBinary_ReturnsLaunchFailed ()
	{
		var (tempDir, emuPath) = CreateFakeEmulatorSdk ();

		// Overwrite with a script that exits immediately with error code 1
		if (OS.IsWindows) {
			File.WriteAllText (emuPath, "@echo off\r\nexit /b 1\r\n");
		} else {
			File.WriteAllText (emuPath, "#!/bin/sh\nexit 1\n");
		}

		try {
			var devices = new List<AdbDeviceInfo> ();
			var mockAdb = new MockAdbRunner (devices);

			var runner = new EmulatorRunner (emuPath);
			var options = new EmulatorBootOptions {
				BootTimeout = TimeSpan.FromSeconds (5),
				PollInterval = TimeSpan.FromMilliseconds (50),
			};

			var result = await runner.BootEmulatorAsync ("Test_AVD", mockAdb, options);

			Assert.IsFalse (result.Success);
			Assert.AreEqual (EmulatorBootErrorKind.LaunchFailed, result.ErrorKind);
			Assert.IsNotNull (result.ErrorMessage);
			Assert.Contains ("exited with code", result.ErrorMessage);
		} finally {
			Directory.Delete (tempDir, true);
		}
	}

	[TestMethod]
	[OSCondition (OperatingSystems.Linux | OperatingSystems.OSX)]
	public void ShellQuote_EscapesSingleQuotes ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), $"emu-quote-test-{Path.GetRandomFileName ()}");
		var emulatorDir = Path.Combine (tempDir, "emu'dir");
		Directory.CreateDirectory (emulatorDir);

		var emuPath = Path.Combine (emulatorDir, "emulator");
		File.WriteAllText (emuPath, "#!/bin/sh\nsleep 60\n");
		var psi = ProcessUtils.CreateProcessStartInfo ("chmod", "+x", emuPath);
		using (var chmod = new Process { StartInfo = psi }) {
			chmod.Start ();
			Assert.IsTrue (chmod.WaitForExit (5000), "chmod should exit promptly");
		}

		Process? process = null;
		try {
			var runner = new EmulatorRunner (emuPath);
			process = runner.LaunchEmulator ("TestAVD");

			Assert.IsFalse (process.HasExited, "Process should start even with single-quote in path");
		} finally {
			try { process?.Kill (); process?.WaitForExit (5000); } catch { }
			process?.Dispose ();
			Directory.Delete (tempDir, true);
		}
	}

	// --- Helpers ---

	static void WriteEnvironmentProbe (string emulatorPath, string marker)
	{
		File.WriteAllText (emulatorPath, OS.IsWindows ? $"""
			@echo off
			(echo %EMULATOR_TEST_VALUE%&echo.%USERNAME%) > "{marker}"
			echo TestAVD
			""" : $"""
			#!/bin/sh
			printf '%s\n' "$EMULATOR_TEST_VALUE" "$HOME" > "{marker}"
			echo TestAVD
			""");
	}

	static (string tempDir, string emulatorPath) CreateFakeEmulatorSdk (bool keepRunning = false)
	{
		var tempDir = Path.Combine (Path.GetTempPath (), $"emu-boot-test-{Path.GetRandomFileName ()}");
		var emulatorDir = Path.Combine (tempDir, "emulator");
		Directory.CreateDirectory (emulatorDir);

		var emuName = OS.IsWindows ? "emulator.bat" : "emulator";
		var emuPath = Path.Combine (emulatorDir, emuName);
		if (OS.IsWindows) {
			var command = keepRunning ? "ping -n 60 127.0.0.1 >nul" : "exit /b 0";
			File.WriteAllText (emuPath, $"@echo off\r\n{command}\r\n");
		} else {
			var command = keepRunning ? "echo ready > \"$0.ready\"\nexec sleep 60" : "exit 0";
			File.WriteAllText (emuPath, $"#!/bin/sh\n{command}\n");
			var psi = ProcessUtils.CreateProcessStartInfo ("chmod", "+x", emuPath);
			using var chmod = new Process { StartInfo = psi };
			chmod.Start ();
			chmod.WaitForExit ();
		}

		return (tempDir, emuPath);
	}

	static async Task<bool> WaitForFileAsync (string path, TimeSpan timeout)
	{
		var stopwatch = Stopwatch.StartNew ();
		while (stopwatch.Elapsed < timeout) {
			if (File.Exists (path) && new FileInfo (path).Length > 0 && IsFileUnlocked (path))
				return true;
			await Task.Delay (20);
		}
		return File.Exists (path) && new FileInfo (path).Length > 0 && IsFileUnlocked (path);
	}

	static void DeleteDirectoryWithRetry (string path)
	{
		var timeout = Stopwatch.StartNew ();
		while (Directory.Exists (path)) {
			try {
				Directory.Delete (path, recursive: true);
				return;
			} catch (IOException) when (OS.IsWindows && timeout.Elapsed < TimeSpan.FromSeconds (5)) {
				Thread.Sleep (20);
			} catch (UnauthorizedAccessException) when (OS.IsWindows && timeout.Elapsed < TimeSpan.FromSeconds (5)) {
				Thread.Sleep (20);
			}
		}
	}

	static bool IsFileUnlocked (string path)
	{
		try {
			using var stream = File.Open (path, FileMode.Open, FileAccess.Read, FileShare.None);
			return true;
		} catch (IOException) {
			return false;
		}
	}

	/// <summary>
	/// Mock AdbRunner for testing BootEmulatorAsync without real adb commands.
	/// </summary>
	class MockAdbRunner : AdbRunner
	{
		readonly List<AdbDeviceInfo> devices;

		public Dictionary<string, string> ShellProperties { get; } = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, string> ShellCommands { get; } = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
		public Action? OnListDevices { get; set; }

		public MockAdbRunner (List<AdbDeviceInfo> devices)
			: base ("/fake/adb")
		{
			this.devices = devices;
		}

		public override Task<IReadOnlyList<AdbDeviceInfo>> ListDevicesAsync (CancellationToken cancellationToken = default)
		{
			OnListDevices?.Invoke ();
			return Task.FromResult<IReadOnlyList<AdbDeviceInfo>> (devices);
		}

		public override Task<string?> GetShellPropertyAsync (string serial, string propertyName, CancellationToken cancellationToken = default)
		{
			ShellProperties.TryGetValue (propertyName, out var value);
			return Task.FromResult (value);
		}

		public override Task<string?> RunShellCommandAsync (string serial, string command, CancellationToken cancellationToken)
		{
			ShellCommands.TryGetValue (command, out var value);
			return Task.FromResult (value);
		}
	}
}
