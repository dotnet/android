#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class BootAndroidEmulatorTests : BaseTest
{
	List<BuildErrorEventArgs> errors = [];
	List<BuildWarningEventArgs> warnings = [];
	List<BuildMessageEventArgs> messages = [];
	MockBuildEngine engine = null!;

	[SetUp]
	public void Setup ()
	{
		engine = new MockBuildEngine (TestContext.Out, errors = [], warnings = [], messages = []);
	}

	/// <summary>
	/// Mock version of BootAndroidEmulator that overrides all process-dependent methods
	/// so we can test the task logic without launching real emulators or adb.
	/// </summary>
	class MockBootAndroidEmulator : BootAndroidEmulator
	{
		public HashSet<string> OnlineDevices { get; set; } = [];
		public Dictionary<string, string> RunningEmulatorAvdNames { get; set; } = new ();
		public Dictionary<string, (string Serial, int PollsUntilOnline)> EmulatorBootBehavior { get; set; } = new ();
		public Dictionary<string, string?> BootCompletedValues { get; set; } = new ();
		public Dictionary<string, string?> PmPathResults { get; set; } = new ();
		public bool SimulateLaunchFailure { get; set; }
		public string? LastLaunchAvdName { get; private set; }

		readonly Dictionary<string, int> findCallCounts = new ();

		protected override bool IsOnlineAdbDevice (string adbPath, string deviceId)
			=> OnlineDevices.Contains (deviceId);

		protected override string? FindRunningEmulatorForAvd (string adbPath, string avdName)
		{
			foreach (var kvp in RunningEmulatorAvdNames) {
				if (string.Equals (kvp.Value, avdName, StringComparison.OrdinalIgnoreCase) &&
				    OnlineDevices.Contains (kvp.Key)) {
					return kvp.Key;
				}
			}

			if (EmulatorBootBehavior.TryGetValue (avdName, out var behavior)) {
				findCallCounts.TryAdd (avdName, 0);
				findCallCounts [avdName]++;
				if (findCallCounts [avdName] >= behavior.PollsUntilOnline) {
					OnlineDevices.Add (behavior.Serial);
					RunningEmulatorAvdNames [behavior.Serial] = avdName;
					return behavior.Serial;
				}
			}

			return null;
		}

		protected override string? GetRunningAvdName (string adbPath, string serial)
			=> RunningEmulatorAvdNames.TryGetValue (serial, out var name) ? name : null;

		protected override Process? LaunchEmulatorProcess (string emulatorPath, string avdName)
		{
			LastLaunchAvdName = avdName;

			if (SimulateLaunchFailure) {
				Log.LogError ("XA0143: Failed to launch emulator for AVD '{0}': {1}", avdName, "Simulated launch failure");
				return null;
			}

			return Process.GetCurrentProcess ();
		}

		protected override string? GetShellProperty (string adbPath, string serial, string propertyName)
		{
			if (propertyName == "sys.boot_completed" && BootCompletedValues.TryGetValue (serial, out var value))
				return value;
			return null;
		}

		protected override string? RunShellCommand (string adbPath, string serial, string command)
		{
			if (command == "pm path android" && PmPathResults.TryGetValue (serial, out var result))
				return result;
			return null;
		}
	}

	MockBootAndroidEmulator CreateTask (string device = "Pixel_6_API_33")
	{
		return new MockBootAndroidEmulator {
			BuildEngine = engine,
			Device = device,
			EmulatorToolPath = "/sdk/emulator/",
			EmulatorToolExe = "emulator",
			AdbToolPath = "/sdk/platform-tools/",
			AdbToolExe = "adb",
			BootTimeoutSeconds = 10,
		};
	}

	[Test]
	public void AlreadyOnlineDevice_PassesThrough ()
	{
		var task = CreateTask ("emulator-5554");
		task.OnlineDevices = ["emulator-5554"];

		Assert.IsTrue (task.RunTask (), "RunTask should succeed");
		Assert.AreEqual ("emulator-5554", task.ResolvedDevice);
		Assert.AreEqual ("-s emulator-5554", task.AdbTarget);
		Assert.AreEqual (0, errors.Count, "Should have no errors");
	}

	[Test]
	public void AlreadyOnlinePhysicalDevice_PassesThrough ()
	{
		var task = CreateTask ("0A041FDD400327");
		task.OnlineDevices = ["0A041FDD400327"];

		Assert.IsTrue (task.RunTask (), "RunTask should succeed");
		Assert.AreEqual ("0A041FDD400327", task.ResolvedDevice);
		Assert.AreEqual ("-s 0A041FDD400327", task.AdbTarget);
	}

	[Test]
	public void AvdAlreadyRunning_WaitsForFullBoot ()
	{
		var task = CreateTask ("Pixel_6_API_33");
		task.OnlineDevices = ["emulator-5554"];
		task.RunningEmulatorAvdNames = new () {
			{ "emulator-5554", "Pixel_6_API_33" }
		};
		task.BootCompletedValues = new () { { "emulator-5554", "1" } };
		task.PmPathResults = new () { { "emulator-5554", "package:/system/framework/framework-res.apk" } };

		Assert.IsTrue (task.RunTask (), "RunTask should succeed");
		Assert.AreEqual ("emulator-5554", task.ResolvedDevice);
		Assert.AreEqual ("-s emulator-5554", task.AdbTarget);
	}

	[Test]
	public void BootEmulator_AppearsAfterPolling ()
	{
		var task = CreateTask ("Pixel_6_API_33");
		// Not online initially, will appear after 2 polls
		task.EmulatorBootBehavior = new () {
			{ "Pixel_6_API_33", ("emulator-5556", 2) }
		};
		task.BootCompletedValues = new () { { "emulator-5556", "1" } };
		task.PmPathResults = new () { { "emulator-5556", "package:/system/framework/framework-res.apk" } };

		Assert.IsTrue (task.RunTask (), "RunTask should succeed");
		Assert.AreEqual ("emulator-5556", task.ResolvedDevice);
		Assert.AreEqual ("-s emulator-5556", task.AdbTarget);
		Assert.AreEqual ("Pixel_6_API_33", task.LastLaunchAvdName);
	}

	[Test]
	public void LaunchFailure_ReturnsError ()
	{
		var task = CreateTask ("Pixel_6_API_33");
		task.SimulateLaunchFailure = true;

		Assert.IsFalse (task.RunTask (), "RunTask should fail");
		Assert.IsTrue (errors.Any (e => e.Message != null && e.Message.Contains ("XA0143")), "Should have XA0143 error");
		Assert.IsNull (task.ResolvedDevice, "ResolvedDevice should be null");
	}

	[Test]
	public void BootTimeout_BootCompletedNeverReaches1 ()
	{
		var task = CreateTask ("Pixel_6_API_33");
		task.BootTimeoutSeconds = 0; // Immediate timeout
		// Emulator appears immediately but never finishes booting
		task.OnlineDevices = ["emulator-5554"];
		task.RunningEmulatorAvdNames = new () {
			{ "emulator-5554", "Pixel_6_API_33" }
		};
		task.BootCompletedValues = new () { { "emulator-5554", "0" } };

		Assert.IsFalse (task.RunTask (), "RunTask should fail");
		Assert.IsTrue (errors.Any (e => e.Code == "XA0145"), "Should have XA0145 timeout error");
	}

	[Test]
	public void BootTimeout_PmNeverResponds ()
	{
		var task = CreateTask ("Pixel_6_API_33");
		task.BootTimeoutSeconds = 0; // Immediate timeout
		task.OnlineDevices = ["emulator-5554"];
		task.RunningEmulatorAvdNames = new () {
			{ "emulator-5554", "Pixel_6_API_33" }
		};
		task.BootCompletedValues = new () { { "emulator-5554", "1" } };
		// PmPathResults not set — pm never responds

		Assert.IsFalse (task.RunTask (), "RunTask should fail");
		Assert.IsTrue (errors.Any (e => e.Code == "XA0145"), "Should have XA0145 timeout error");
	}

	[Test]
	public void MultipleEmulators_FindsCorrectAvd ()
	{
		var task = CreateTask ("Pixel_9_Pro_XL");
		task.OnlineDevices = ["emulator-5554", "emulator-5556"];
		task.RunningEmulatorAvdNames = new () {
			{ "emulator-5554", "pixel_7_-_api_35" },
			{ "emulator-5556", "Pixel_9_Pro_XL" }
		};
		task.BootCompletedValues = new () { { "emulator-5556", "1" } };
		task.PmPathResults = new () { { "emulator-5556", "package:/system/framework/framework-res.apk" } };

		Assert.IsTrue (task.RunTask (), "RunTask should succeed");
		Assert.AreEqual ("emulator-5556", task.ResolvedDevice);
		Assert.AreEqual ("-s emulator-5556", task.AdbTarget);
	}

	[Test]
	public void ToolPaths_ResolvedFromAndroidSdkDirectory ()
	{
		var task = new MockBootAndroidEmulator {
			BuildEngine = engine,
			Device = "emulator-5554",
			AndroidSdkDirectory = "/android/sdk",
			BootTimeoutSeconds = 10,
		};
		task.OnlineDevices = ["emulator-5554"];

		// Tool paths are not set explicitly — ResolveAdbPath/ResolveEmulatorPath
		// should compute them from AndroidSdkDirectory
		Assert.IsTrue (task.RunTask (), "RunTask should succeed");
		Assert.AreEqual ("emulator-5554", task.ResolvedDevice);
	}
}
