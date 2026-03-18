#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

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
	/// Mock version of BootAndroidEmulator that overrides <see cref="ExecuteBootAsync"/>
	/// to return a configurable <see cref="EmulatorBootResult"/> without launching real processes.
	/// </summary>
	class MockBootAndroidEmulator : BootAndroidEmulator
	{
		public EmulatorBootResult BootResult { get; set; } = new () { Success = true, Serial = "emulator-5554" };
		public string? LastBootedDevice { get; private set; }
		public EmulatorBootOptions? LastBootOptions { get; private set; }

		protected override Task<EmulatorBootResult> ExecuteBootAsync (
			string adbPath,
			string emulatorPath,
			Action<TraceLevel, string> logger,
			string device,
			EmulatorBootOptions options,
			CancellationToken cancellationToken)
		{
			LastBootedDevice = device;
			LastBootOptions = options;
			return Task.FromResult (BootResult);
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

	bool RunTaskSynchronously (MockBootAndroidEmulator task)
	{
		return task.Execute ();
	}

	[Test]
	public void AlreadyOnlineDevice_PassesThrough ()
	{
		var task = CreateTask ("emulator-5554");
		task.BootResult = new EmulatorBootResult {
			Success = true,
			Serial = "emulator-5554",
		};

		Assert.IsTrue (RunTaskSynchronously (task), "Task should succeed");
		Assert.AreEqual ("emulator-5554", task.ResolvedDevice);
		Assert.AreEqual ("-s emulator-5554", task.AdbTarget);
		Assert.AreEqual (0, errors.Count, "Should have no errors");
	}

	[Test]
	public void AlreadyOnlinePhysicalDevice_PassesThrough ()
	{
		var task = CreateTask ("0A041FDD400327");
		task.BootResult = new EmulatorBootResult {
			Success = true,
			Serial = "0A041FDD400327",
		};

		Assert.IsTrue (RunTaskSynchronously (task), "Task should succeed");
		Assert.AreEqual ("0A041FDD400327", task.ResolvedDevice);
		Assert.AreEqual ("-s 0A041FDD400327", task.AdbTarget);
	}

	[Test]
	public void AvdAlreadyRunning_WaitsForFullBoot ()
	{
		var task = CreateTask ("Pixel_6_API_33");
		task.BootResult = new EmulatorBootResult {
			Success = true,
			Serial = "emulator-5554",
		};

		Assert.IsTrue (RunTaskSynchronously (task), "Task should succeed");
		Assert.AreEqual ("emulator-5554", task.ResolvedDevice);
		Assert.AreEqual ("-s emulator-5554", task.AdbTarget);
		Assert.AreEqual ("Pixel_6_API_33", task.LastBootedDevice);
	}

	[Test]
	public void BootEmulator_AppearsAfterPolling ()
	{
		var task = CreateTask ("Pixel_6_API_33");
		task.BootResult = new EmulatorBootResult {
			Success = true,
			Serial = "emulator-5556",
		};

		Assert.IsTrue (RunTaskSynchronously (task), "Task should succeed");
		Assert.AreEqual ("emulator-5556", task.ResolvedDevice);
		Assert.AreEqual ("-s emulator-5556", task.AdbTarget);
		Assert.AreEqual ("Pixel_6_API_33", task.LastBootedDevice);
	}

	[Test]
	public void LaunchFailure_ReturnsError ()
	{
		var task = CreateTask ("Pixel_6_API_33");
		task.BootResult = new EmulatorBootResult {
			Success = false,
			ErrorKind = EmulatorBootErrorKind.LaunchFailed,
			ErrorMessage = "Failed to launch emulator: Simulated launch failure",
		};

		Assert.IsFalse (RunTaskSynchronously (task), "Task should fail");
		Assert.IsTrue (errors.Any (e => e.Code == "XA0143"), "Should have XA0143 error");
		Assert.IsNull (task.ResolvedDevice, "ResolvedDevice should be null");
	}

	[Test]
	public void BootTimeout_ReturnsError ()
	{
		var task = CreateTask ("Pixel_6_API_33");
		task.BootResult = new EmulatorBootResult {
			Success = false,
			ErrorKind = EmulatorBootErrorKind.Timeout,
			ErrorMessage = "Timed out waiting for emulator 'Pixel_6_API_33' to boot within 10s.",
		};

		Assert.IsFalse (RunTaskSynchronously (task), "Task should fail");
		Assert.IsTrue (errors.Any (e => e.Code == "XA0145"), "Should have XA0145 timeout error");
	}

	[Test]
	public void MultipleEmulators_FindsCorrectAvd ()
	{
		var task = CreateTask ("Pixel_9_Pro_XL");
		task.BootResult = new EmulatorBootResult {
			Success = true,
			Serial = "emulator-5556",
		};

		Assert.IsTrue (RunTaskSynchronously (task), "Task should succeed");
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
			BootResult = new EmulatorBootResult {
				Success = true,
				Serial = "emulator-5554",
			},
		};

		Assert.IsTrue (RunTaskSynchronously (task), "Task should succeed");
		Assert.AreEqual ("emulator-5554", task.ResolvedDevice);
	}

	[Test]
	public void ExtraArguments_PassedToOptions ()
	{
		var task = new MockBootAndroidEmulator {
			BuildEngine = engine,
			Device = "Pixel_6_API_33",
			EmulatorToolPath = "/sdk/emulator/",
			EmulatorToolExe = "emulator",
			AdbToolPath = "/sdk/platform-tools/",
			AdbToolExe = "adb",
			BootTimeoutSeconds = 10,
			EmulatorExtraArguments = "-no-snapshot-load -gpu auto",
			BootResult = new EmulatorBootResult {
				Success = true,
				Serial = "emulator-5554",
			},
		};

		Assert.IsTrue (RunTaskSynchronously (task), "Task should succeed");
		Assert.AreEqual ("emulator-5554", task.ResolvedDevice);
		Assert.IsNotNull (task.LastBootOptions, "Boot options should be captured");
		Assert.IsNotNull (task.LastBootOptions!.AdditionalArgs, "AdditionalArgs should not be null");
		CollectionAssert.AreEqual (
			new[] { "-no-snapshot-load", "-gpu", "auto" },
			task.LastBootOptions.AdditionalArgs,
			"Extra arguments should be parsed and passed to options");
	}

	[Test]
	public void UnknownError_MapsToXA0145 ()
	{
		var task = CreateTask ("Pixel_6_API_33");
		task.BootResult = new EmulatorBootResult {
			Success = false,
			ErrorKind = EmulatorBootErrorKind.Unknown,
			ErrorMessage = "Some unexpected error occurred",
		};

		Assert.IsFalse (RunTaskSynchronously (task), "Task should fail");
		Assert.IsTrue (errors.Any (e => e.Code == "XA0145"), "Unknown errors should map to XA0145");
	}
}
