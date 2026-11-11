#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class BootAndroidEmulatorTests
{
	List<BuildErrorEventArgs> errors = [];
	List<BuildWarningEventArgs> warnings = [];
	List<BuildMessageEventArgs> messages = [];
	MockBuildEngine? engine;

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
		public string? LastAdbPath { get; private set; }
		public string? LastEmulatorPath { get; private set; }
		public Action<TraceLevel, string>? LastLogger { get; private set; }
		public Func<CancellationToken, Task<EmulatorBootResult>>? BootAsync { get; set; }

		protected override Task<EmulatorBootResult> ExecuteBootAsync (
			string adbPath,
			string emulatorPath,
			Action<TraceLevel, string> logger,
			string device,
			EmulatorBootOptions options,
			CancellationToken cancellationToken)
		{
			LastAdbPath = adbPath;
			LastEmulatorPath = emulatorPath;
			LastBootedDevice = device;
			LastBootOptions = options;
			LastLogger = logger;
			return BootAsync?.Invoke (cancellationToken) ?? Task.FromResult (BootResult);
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
		task.BootResult = new EmulatorBootResult {
			Success = true,
			Serial = "emulator-5554",
		};

		Assert.IsTrue (task.Execute (), "Task should succeed");
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

		Assert.IsTrue (task.Execute (), "Task should succeed");
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

		Assert.IsTrue (task.Execute (), "Task should succeed");
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

		Assert.IsTrue (task.Execute (), "Task should succeed");
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

		Assert.IsFalse (task.Execute (), "Task should fail");
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

		Assert.IsFalse (task.Execute (), "Task should fail");
		var error = errors.FirstOrDefault (e => e.Code == "XA0145");
		Assert.IsNotNull (error, "Should have XA0145 timeout error");
		var message = error?.Message ?? "";
		StringAssert.Contains ("Pixel_6_API_33", message, "AVD name should be substituted into the message");
		Assert.IsFalse (message.Contains ("{0}") || message.Contains ("{1}"), "Message placeholders should be substituted");
	}

	[Test]
	public void MultipleEmulators_FindsCorrectAvd ()
	{
		var task = CreateTask ("Pixel_9_Pro_XL");
		task.BootResult = new EmulatorBootResult {
			Success = true,
			Serial = "emulator-5556",
		};

		Assert.IsTrue (task.Execute (), "Task should succeed");
		Assert.AreEqual ("emulator-5556", task.ResolvedDevice);
		Assert.AreEqual ("-s emulator-5556", task.AdbTarget);
	}

	[Test]
	public void ToolPaths_ResolvedFromAndroidSdkDirectory ()
	{
		var directory = Path.Combine (Path.GetTempPath (), "boot tools");
		var task = new MockBootAndroidEmulator {
			BuildEngine = engine,
			TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ()),
			Device = "emulator-5554",
			AndroidSdkDirectory = "sdk",
			BootTimeoutSeconds = 10,
			BootResult = new EmulatorBootResult {
				Success = true,
				Serial = "emulator-5554",
			},
		};

		Assert.IsTrue (task.Execute (), "Task should succeed");
		Assert.AreEqual ("emulator-5554", task.ResolvedDevice);
		Assert.AreEqual (Path.Combine (directory, "sdk", "platform-tools", OS.IsWindows ? "adb.exe" : "adb"), task.LastAdbPath);
		Assert.AreEqual (Path.Combine (directory, "sdk", "emulator", OS.IsWindows ? "emulator.exe" : "emulator"), task.LastEmulatorPath);
	}

	[Test]
	public async Task BootUsesTaskEnvironment ()
	{
		var root = Path.Combine (Path.GetTempPath (), "boot-task-" + Guid.NewGuid ().ToString ("N"));
		try {
			var tasks = new List<BootAndroidEmulator> ();
			for (int i = 0; i < 2; i++) {
				var directory = Path.Combine (root, $"project {i}");
				var tools = Path.Combine (directory, "tools");
				Directory.CreateDirectory (tools);
				string adb = OperatingSystem.IsWindows () ? "adb.cmd" : "adb";
				string emulator = OperatingSystem.IsWindows () ? "emulator.cmd" : "emulator";
				File.WriteAllText (Path.Combine (tools, adb), OperatingSystem.IsWindows () ? """
					@echo off
					if defined USERNAME exit /b 43
					echo %CD% > adb-directory
					if "%1"=="devices" (
					    echo List of devices attached
					    echo other-device device
					    if exist launched echo %BOOT_SERIAL% device
					    exit /b 0
					)
					if not "%2"=="%BOOT_SERIAL%" exit /b 44
					if "%5"=="ro.boot.qemu.avd_name" (echo %BOOT_AVD%) else if "%5"=="sys.boot_completed" (echo 1) else (echo package:android)
					""" : """
					#!/bin/sh
					if [ -n "$HOME" ]; then exit 43; fi
					printf '%s\n' "$PWD" > adb-directory
					if [ "$1" = devices ]; then
					    echo 'List of devices attached'
					    echo 'other-device device'
					    if [ -f launched ]; then echo "$BOOT_SERIAL device"; fi
					    exit 0
					fi
					if [ "$2" != "$BOOT_SERIAL" ]; then exit 44; fi
					case "$5" in
					    ro.boot.qemu.avd_name) echo "$BOOT_AVD";;
					    sys.boot_completed) echo 1;;
					    *) echo package:android;;
					esac
					""");
				File.WriteAllText (Path.Combine (tools, emulator), OperatingSystem.IsWindows () ? """
					@echo off
					if defined USERNAME exit /b 43
					if not "%2"=="%BOOT_AVD%" exit /b 44
					echo %CD% > emulator-directory
					echo launched > launched
					""" : """
					#!/bin/sh
					if [ -n "$HOME" ] || [ "$2" != "$BOOT_AVD" ]; then exit 43; fi
					printf '%s\n' "$PWD" > emulator-directory
					echo launched > launched
					""");
				if (!OperatingSystem.IsWindows ()) {
					var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
					File.SetUnixFileMode (Path.Combine (tools, adb), mode);
					File.SetUnixFileMode (Path.Combine (tools, emulator), mode);
				}
				var environment = new Dictionary<string, string> {
					["BOOT_SERIAL"] = $"emulator-{5578 + 2 * i}",
					["BOOT_AVD"] = $"TestAvd{i}",
					["PATH"] = tools,
					["TMP"] = directory,
					["TMPDIR"] = directory,
				};
				foreach (string name in new [] { "SystemRoot", "ComSpec" }) {
					if (Environment.GetEnvironmentVariable (name) is string value)
						environment [name] = value;
				}
				tasks.Add (new BootAndroidEmulator {
					BuildEngine = new MockBuildEngine (TextWriter.Null),
					TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, environment),
					Device = environment ["BOOT_AVD"],
					AdbToolExe = adb,
					EmulatorToolPath = "tools",
					EmulatorToolExe = emulator,
					BootTimeoutSeconds = 15,
				});
			}
			var results = await Task.WhenAll (tasks.Select (task => Task.Run (() => task.Execute ())));
			Assert.IsTrue (results.All (result => result), "Both boot tasks should succeed.");
			for (int i = 0; i < tasks.Count; i++) {
				var task = tasks [i];
				var directory = task.TaskEnvironment.ProjectDirectory.Value;
				string serial = $"emulator-{5578 + 2 * i}";
				Assert.AreEqual (serial, task.ResolvedDevice);
				Assert.AreEqual ($"-s {serial}", task.AdbTarget);
				Assert.AreEqual (directory, File.ReadAllText (Path.Combine (directory, "adb-directory")).Trim ());
				Assert.AreEqual (directory, File.ReadAllText (Path.Combine (directory, "emulator-directory")).Trim ());
			}
		} finally {
			if (Directory.Exists (root))
				Directory.Delete (root, recursive: true);
		}
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

		Assert.IsTrue (task.Execute (), "Task should succeed");
		Assert.AreEqual ("emulator-5554", task.ResolvedDevice);
		var bootOptions = task.LastBootOptions;
		Assert.IsNotNull (bootOptions, "Boot options should be captured");
		Assert.IsNotNull (bootOptions?.AdditionalArgs, "AdditionalArgs should not be null");
		CollectionAssert.AreEqual (
			new[] { "-no-snapshot-load", "-gpu", "auto" },
			bootOptions?.AdditionalArgs,
			"Extra arguments should be parsed and passed to options");
	}

	[Test]
	public void ExtraArguments_QuotedValuesPreserved ()
	{
		var task = new MockBootAndroidEmulator {
			BuildEngine = engine,
			Device = "Pixel_6_API_33",
			EmulatorToolPath = "/sdk/emulator/",
			EmulatorToolExe = "emulator",
			AdbToolPath = "/sdk/platform-tools/",
			AdbToolExe = "adb",
			BootTimeoutSeconds = 10,
			EmulatorExtraArguments = "-no-snapshot-load -skin \"Nexus 5X\"",
			BootResult = new EmulatorBootResult {
				Success = true,
				Serial = "emulator-5554",
			},
		};

		Assert.IsTrue (task.Execute (), "Task should succeed");
		var bootOptions = task.LastBootOptions;
		Assert.IsNotNull (bootOptions?.AdditionalArgs, "AdditionalArgs should not be null");
		CollectionAssert.AreEqual (
			new[] { "-no-snapshot-load", "-skin", "Nexus 5X" },
			bootOptions?.AdditionalArgs,
			"Quoted arguments with spaces should be preserved as a single token");
	}

	[Test]
	public void ExtraArguments_EscapedQuotesPreserved ()
	{
		var task = new MockBootAndroidEmulator {
			BuildEngine = engine,
			Device = "Pixel_6_API_33",
			EmulatorToolPath = "/sdk/emulator/",
			EmulatorToolExe = "emulator",
			AdbToolPath = "/sdk/platform-tools/",
			AdbToolExe = "adb",
			BootTimeoutSeconds = 10,
			EmulatorExtraArguments = "-prop \"persist.sys.timezone=\\\"America/New_York\\\"\"",
			BootResult = new EmulatorBootResult {
				Success = true,
				Serial = "emulator-5554",
			},
		};

		Assert.IsTrue (task.Execute (), "Task should succeed");
		var bootOptions = task.LastBootOptions;
		Assert.IsNotNull (bootOptions?.AdditionalArgs, "AdditionalArgs should not be null");
		CollectionAssert.AreEqual (
			new[] { "-prop", "persist.sys.timezone=\"America/New_York\"" },
			bootOptions?.AdditionalArgs,
			"Escaped quotes inside quoted values should be preserved as literal quotes");
	}

	[Test]
	public void UnknownError_MapsToXA0144 ()
	{
		var task = CreateTask ("Pixel_6_API_33");
		task.BootResult = new EmulatorBootResult {
			Success = false,
			ErrorKind = EmulatorBootErrorKind.Unknown,
			ErrorMessage = "Some unexpected error occurred",
		};

		Assert.IsFalse (task.Execute (), "Task should fail");
		var error = errors.FirstOrDefault (e => e.Code == "XA0144");
		Assert.IsNotNull (error, "Unknown errors should map to XA0144");
		StringAssert.Contains ("Unknown", error?.Message, "Error kind should be included in the message");
		StringAssert.Contains ("Some unexpected error occurred", error?.Message, "Error message should be included");
	}

	[Test]
	public void Cancelled_DoesNotFail ()
	{
		var task = CreateTask ("Pixel_6_API_33");
		task.BootResult = new EmulatorBootResult {
			Success = false,
			ErrorKind = EmulatorBootErrorKind.Cancelled,
		};

		Assert.IsTrue (task.Execute (), "Cancelled task should not fail — MSBuild handles cancellation");
		Assert.IsNull (task.ResolvedDevice, "ResolvedDevice should be null on cancellation");
		Assert.IsEmpty (errors, "No errors should be logged for cancellation");
	}

	[Test]
	public async Task CancelStopsPendingBoot ()
	{
		var started = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		var task = CreateTask ();
		task.BootAsync = async token => {
			started.SetResult (true);
			await Task.Delay (Timeout.Infinite, token).ConfigureAwait (false);
			return task.BootResult;
		};
		var execution = Task.Run (() => task.Execute ());
		try {
			await started.Task.WaitAsync (TimeSpan.FromSeconds (10));
			task.Cancel ();
			Assert.IsTrue (await execution.WaitAsync (TimeSpan.FromSeconds (10)));
			Assert.IsNull (task.ResolvedDevice);
			Assert.IsEmpty (errors);
		} finally {
			task.Cancel ();
			await execution.WaitAsync (TimeSpan.FromSeconds (10));
		}
	}

	[Test]
	public void RunnerLoggingStopsWhenTaskReturns ()
	{
		var task = CreateTask ();
		task.BootAsync = _ => Task.Run (() => {
			var logger = task.LastLogger ?? throw new InvalidOperationException ("The runner logger was not supplied.");
			logger (TraceLevel.Info, "boot progress");
			return task.BootResult;
		});
		Assert.IsTrue (task.Execute ());
		Assert.IsTrue (messages.Any (message => message.Message == "boot progress"));
		var callback = task.LastLogger ?? throw new InvalidOperationException ("The runner logger was not supplied.");
		messages.Clear ();
		callback (TraceLevel.Error, "late emulator output");
		Assert.IsEmpty (messages);
		Assert.IsEmpty (errors);
	}

	[Test]
	public void Success_NullSerial_ReturnsError ()
	{
		var task = CreateTask ("Pixel_6_API_33");
		task.BootResult = new EmulatorBootResult {
			Success = true,
			Serial = null,
		};

		Assert.IsFalse (task.Execute (), "Task should fail when serial is null");
		Assert.IsTrue (errors.Any (e => e.Code == "XA0143"), "Null serial should map to XA0143");
		Assert.IsNull (task.ResolvedDevice, "ResolvedDevice should be null");
	}

	[Test]
	public void InvalidTimeout_ReturnsError ()
	{
		var task = CreateTask ("Pixel_6_API_33");
		task.BootTimeoutSeconds = 0;

		Assert.IsFalse (task.Execute (), "Task should fail with zero timeout");
		var error = errors.FirstOrDefault (e => e.Code == "XA0145");
		Assert.IsNotNull (error, "Invalid timeout should produce XA0145 error");
		var message = error?.Message ?? "";
		StringAssert.Contains ("Pixel_6_API_33", message, "AVD name should be substituted into the message");
		Assert.IsFalse (message.Contains ("{0}") || message.Contains ("{1}"), "Message placeholders should be substituted");
	}
}
