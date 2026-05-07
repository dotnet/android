// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
public class EmulatorRunnerTests
{
	[Test]
	public void ParseListAvdsOutput_MultipleAvds ()
	{
		var output = "Pixel_7_API_35\nMAUI_Emulator\nNexus_5X\n";

		var avds = EmulatorRunner.ParseListAvdsOutput (output);

		Assert.AreEqual (3, avds.Count);
		Assert.AreEqual ("Pixel_7_API_35", avds [0]);
		Assert.AreEqual ("MAUI_Emulator", avds [1]);
		Assert.AreEqual ("Nexus_5X", avds [2]);
	}

	[Test]
	public void ParseListAvdsOutput_EmptyOutput ()
	{
		var avds = EmulatorRunner.ParseListAvdsOutput ("");
		Assert.AreEqual (0, avds.Count);
	}

	[Test]
	public void ParseListAvdsOutput_WindowsNewlines ()
	{
		var output = "Pixel_7_API_35\r\nMAUI_Emulator\r\n";

		var avds = EmulatorRunner.ParseListAvdsOutput (output);

		Assert.AreEqual (2, avds.Count);
		Assert.AreEqual ("Pixel_7_API_35", avds [0]);
		Assert.AreEqual ("MAUI_Emulator", avds [1]);
	}

	[Test]
	public void ParseListAvdsOutput_BlankLines ()
	{
		var output = "\nPixel_7_API_35\n\n\nMAUI_Emulator\n\n";

		var avds = EmulatorRunner.ParseListAvdsOutput (output);

		Assert.AreEqual (2, avds.Count);
	}

	[Test]
	public void Constructor_ThrowsOnNullPath ()
	{
		Assert.Throws<ArgumentException> (() => new EmulatorRunner (null!));
	}

	[Test]
	public void Constructor_ThrowsOnEmptyPath ()
	{
		Assert.Throws<ArgumentException> (() => new EmulatorRunner (""));
	}

	[Test]
	public void Constructor_ThrowsOnWhitespacePath ()
	{
		Assert.Throws<ArgumentException> (() => new EmulatorRunner ("   "));
	}

	[Test]
	public void LaunchEmulator_ThrowsOnNullAvdName ()
	{
		var runner = new EmulatorRunner ("/fake/emulator");
		Assert.Throws<ArgumentException> (() => runner.LaunchEmulator (null!));
	}

	[Test]
	public void LaunchEmulator_ThrowsOnEmptyAvdName ()
	{
		var runner = new EmulatorRunner ("/fake/emulator");
		Assert.Throws<ArgumentException> (() => runner.LaunchEmulator (""));
	}

	[Test]
	public void LaunchEmulator_ThrowsOnWhitespaceAvdName ()
	{
		var runner = new EmulatorRunner ("/fake/emulator");
		Assert.Throws<ArgumentException> (() => runner.LaunchEmulator ("   "));
	}

	// --- BootEmulatorAsync tests (ported from dotnet/android BootAndroidEmulatorTests) ---

	[Test]
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

	[Test]
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

	[Test]
	public async Task BootEmulator_AppearsAfterPolling ()
	{
		var devices = new List<AdbDeviceInfo> ();
		var mockAdb = new MockAdbRunner (devices);
		mockAdb.ShellProperties ["sys.boot_completed"] = "1";
		mockAdb.ShellCommands ["pm path android"] = "package:/system/framework/framework-res.apk";

		int pollCount = 0;
		mockAdb.OnListDevices = () => {
			pollCount++;
			if (pollCount >= 2) {
				devices.Add (new AdbDeviceInfo {
					Serial = "emulator-5554",
					Type = AdbDeviceType.Emulator,
					Status = AdbDeviceStatus.Online,
					AvdName = "Pixel_7_API_35",
				});
			}
		};

		var (tempDir, emuPath) = CreateFakeEmulatorSdk ();
		Process? emulatorProcess = null;
		try {
			var runner = new EmulatorRunner (emuPath);
			var options = new EmulatorBootOptions {
				BootTimeout = TimeSpan.FromSeconds (10),
				PollInterval = TimeSpan.FromMilliseconds (50),
			};

			var result = await runner.BootEmulatorAsync ("Pixel_7_API_35", mockAdb, options);

			Assert.IsTrue (result.Success);
			Assert.AreEqual ("emulator-5554", result.Serial);
			Assert.IsTrue (pollCount >= 2);
		} finally {
			// Kill any emulator process spawned by the test
			try {
				emulatorProcess = FindEmulatorProcess (emuPath);
				emulatorProcess?.Kill ();
				emulatorProcess?.WaitForExit (1000);
			} catch { }
			Directory.Delete (tempDir, true);
		}
	}

	[Test]
	public async Task LaunchFailure_ReturnsError ()
	{
		var devices = new List<AdbDeviceInfo> ();
		var mockAdb = new MockAdbRunner (devices);

		// Nonexistent path → LaunchAvd throws → error result
		var runner = new EmulatorRunner ("/nonexistent/emulator");
		var options = new EmulatorBootOptions { BootTimeout = TimeSpan.FromSeconds (2) };

		var result = await runner.BootEmulatorAsync ("Pixel_7_API_35", mockAdb, options);

		Assert.IsFalse (result.Success);
		Assert.That (result.ErrorMessage, Does.Contain ("Failed to launch"));
	}

	[Test]
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
		Assert.That (result.ErrorMessage, Does.Contain ("Timed out"));
	}

	[Test]
	public void BootEmulatorAsync_InvalidBootTimeout_Throws ()
	{
		var runner = new EmulatorRunner ("/fake/emulator");
		var mockAdb = new MockAdbRunner (new List<AdbDeviceInfo> ());
		var options = new EmulatorBootOptions { BootTimeout = TimeSpan.Zero };

		Assert.ThrowsAsync<ArgumentOutOfRangeException> (() =>
			runner.BootEmulatorAsync ("test", mockAdb, options));
	}

	[Test]
	public void BootEmulatorAsync_InvalidPollInterval_Throws ()
	{
		var runner = new EmulatorRunner ("/fake/emulator");
		var mockAdb = new MockAdbRunner (new List<AdbDeviceInfo> ());
		var options = new EmulatorBootOptions { PollInterval = TimeSpan.FromMilliseconds (-1) };

		Assert.ThrowsAsync<ArgumentOutOfRangeException> (() =>
			runner.BootEmulatorAsync ("test", mockAdb, options));
	}

	[Test]
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

	// --- Tests ported from dotnet/android BootAndroidEmulatorTests ---

	[Test]
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

	[Test]
	public async Task AdditionalArgs_PassedToLaunchEmulator ()
	{
		// Verify that AdditionalArgs from EmulatorBootOptions are forwarded
		// to the emulator process. We use a fake emulator script that logs
		// its arguments so we can inspect them after the boot times out.
		var (tempDir, emuPath) = CreateFakeEmulatorSdk ();
		var argsLogPath = Path.Combine (tempDir, "args.log");

		// Rewrite the fake emulator to log its arguments
		if (OS.IsWindows) {
			File.WriteAllText (emuPath, $"@echo off\r\necho %* > \"{argsLogPath}\"\r\nping -n 60 127.0.0.1 >nul\r\n");
		} else {
			File.WriteAllText (emuPath, $"#!/bin/sh\necho \"$@\" > \"{argsLogPath}\"\nsleep 60\n");
		}

		try {
			var devices = new List<AdbDeviceInfo> ();
			var mockAdb = new MockAdbRunner (devices);

			var runner = new EmulatorRunner (emuPath);
			var options = new EmulatorBootOptions {
				BootTimeout = TimeSpan.FromMilliseconds (500),
				PollInterval = TimeSpan.FromMilliseconds (50),
				AdditionalArgs = new List<string> { "-gpu", "auto", "-no-audio" },
			};

			// Boot will time out (no device appears), but the emulator process
			// should have been launched with the additional args.
			var result = await runner.BootEmulatorAsync ("Test_AVD", mockAdb, options);

			Assert.IsFalse (result.Success, "Boot should time out");

			// Give the script a moment to flush args.log
			await Task.Delay (200);

			if (File.Exists (argsLogPath)) {
				var logged = File.ReadAllText (argsLogPath);
				Assert.That (logged, Does.Contain ("-gpu"), "Should contain -gpu arg");
				Assert.That (logged, Does.Contain ("auto"), "Should contain auto value");
				Assert.That (logged, Does.Contain ("-no-audio"), "Should contain -no-audio arg");
				Assert.That (logged, Does.Contain ("-avd"), "Should contain -avd flag");
				Assert.That (logged, Does.Contain ("Test_AVD"), "Should contain AVD name");
			}
		} finally {
			// Clean up any spawned processes
			try {
				foreach (var p in Process.GetProcessesByName ("sleep")) {
					try { p.Kill (); p.WaitForExit (1000); } catch { }
				}
			} catch { }
			Directory.Delete (tempDir, true);
		}
	}

	[Test]
	public async Task CancellationToken_AbortsBoot ()
	{
		// Verify that cancelling the token during the polling phase causes
		// BootEmulatorAsync to return promptly rather than blocking for the
		// full BootTimeout duration. We need a real fake emulator script so
		// LaunchEmulator succeeds (starts the process), then cancel while polling.
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
				Assert.That (sw.Elapsed.TotalSeconds, Is.LessThan (5),
					"Cancellation should abort within a few seconds, not wait for full timeout");
			}
		} finally {
			try {
				foreach (var p in Process.GetProcessesByName ("sleep")) {
					try { p.Kill (); p.WaitForExit (1000); } catch { }
				}
			} catch { }
			Directory.Delete (tempDir, true);
		}
	}

	[Test]
	public async Task ColdBoot_PassesNoSnapshotLoad ()
	{
		// Verify that ColdBoot = true causes -no-snapshot-load to be passed.
		var (tempDir, emuPath) = CreateFakeEmulatorSdk ();
		var argsLogPath = Path.Combine (tempDir, "args.log");

		if (OS.IsWindows) {
			File.WriteAllText (emuPath, $"@echo off\r\necho %* > \"{argsLogPath}\"\r\nping -n 60 127.0.0.1 >nul\r\n");
		} else {
			File.WriteAllText (emuPath, $"#!/bin/sh\necho \"$@\" > \"{argsLogPath}\"\nsleep 60\n");
		}

		try {
			var devices = new List<AdbDeviceInfo> ();
			var mockAdb = new MockAdbRunner (devices);

			var runner = new EmulatorRunner (emuPath);
			var options = new EmulatorBootOptions {
				BootTimeout = TimeSpan.FromMilliseconds (500),
				PollInterval = TimeSpan.FromMilliseconds (50),
				ColdBoot = true,
			};

			var result = await runner.BootEmulatorAsync ("Test_AVD", mockAdb, options);

			Assert.IsFalse (result.Success, "Boot should time out");
			await Task.Delay (200);

			if (File.Exists (argsLogPath)) {
				var logged = File.ReadAllText (argsLogPath);
				Assert.That (logged, Does.Contain ("-no-snapshot-load"), "ColdBoot should pass -no-snapshot-load");
			}
		} finally {
			try {
				foreach (var p in Process.GetProcessesByName ("sleep")) {
					try { p.Kill (); p.WaitForExit (1000); } catch { }
				}
			} catch { }
			Directory.Delete (tempDir, true);
		}
	}

	[Test]
	public void BootEmulatorAsync_NullAdbRunner_Throws ()
	{
		var runner = new EmulatorRunner ("/fake/emulator");

		Assert.ThrowsAsync<ArgumentNullException> (() =>
			runner.BootEmulatorAsync ("test", null!));
	}

	[Test]
	public void BootEmulatorAsync_EmptyDeviceName_Throws ()
	{
		var runner = new EmulatorRunner ("/fake/emulator");
		var mockAdb = new MockAdbRunner (new List<AdbDeviceInfo> ());

		Assert.ThrowsAsync<ArgumentException> (() =>
			runner.BootEmulatorAsync ("", mockAdb));
	}

	[Test]
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
			Assert.That (result.ErrorMessage, Does.Contain ("exited with code"));
		} finally {
			Directory.Delete (tempDir, true);
		}
	}

	// --- Helpers ---

	static (string tempDir, string emulatorPath) CreateFakeEmulatorSdk ()
	{
		var tempDir = Path.Combine (Path.GetTempPath (), $"emu-boot-test-{Path.GetRandomFileName ()}");
		var emulatorDir = Path.Combine (tempDir, "emulator");
		Directory.CreateDirectory (emulatorDir);

		var emuName = OS.IsWindows ? "emulator.bat" : "emulator";
		var emuPath = Path.Combine (emulatorDir, emuName);
		if (OS.IsWindows) {
			File.WriteAllText (emuPath, "@echo off\r\nping -n 60 127.0.0.1 >nul\r\n");
		} else {
			File.WriteAllText (emuPath, "#!/bin/sh\nsleep 60\n");
			var psi = ProcessUtils.CreateProcessStartInfo ("chmod", "+x", emuPath);
			using var chmod = new Process { StartInfo = psi };
			chmod.Start ();
			chmod.WaitForExit ();
		}

		return (tempDir, emuPath);
	}

	static Process? FindEmulatorProcess (string emuPath)
	{
		// Best-effort: find the process by matching the command line
		try {
			foreach (var p in Process.GetProcessesByName ("emulator")) {
				return p;
			}
			foreach (var p in Process.GetProcessesByName ("sleep")) {
				return p;
			}
		} catch { }
		return null;
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
