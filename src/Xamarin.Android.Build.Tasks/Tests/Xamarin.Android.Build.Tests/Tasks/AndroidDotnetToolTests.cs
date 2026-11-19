using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class AndroidDotnetToolTests : BaseTest
	{
		MockBuildEngine engine;
		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		List<BuildMessageEventArgs> messages;

		[SetUp]
		public void Setup ()
		{
			engine = new MockBuildEngine (TestContext.Out,
				errors: errors = new List<BuildErrorEventArgs> (),
				warnings: warnings = new List<BuildWarningEventArgs> (),
				messages: messages = new List<BuildMessageEventArgs> ());
		}

		[Test]
		public void ShouldUseFullToolPath ()
		{
			var dotnetDir = TestEnvironment.DotNetPreviewDirectory;
			var dotnetPath = Path.Combine (dotnetDir, (TestEnvironment.IsWindows ? "dotnet.exe" : "dotnet"));
			var classParseTask = new ClassParseTestTask {
				BuildEngine = engine,
				NetCoreRoot = dotnetDir,
				ToolPath = TestEnvironment.AndroidMSBuildDirectory,
				ToolExe = "class-parse.dll",
			};

			Assert.True (classParseTask.Execute (), "Task should have succeeded.");
			Assert.IsTrue (messages.Any (m => m.Message.StartsWith (dotnetPath)), "Task did not use expected tool path.");
		}

		[Test]
		public void ClassParseWritesReferenceArguments ()
		{
			var outputDirectory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (outputDirectory);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = outputDirectory;
			var output = Path.Combine (outputDirectory, "api.xml");
			var referenceOutput = Path.Combine (outputDirectory, "reference-api.xml");
			var sourceJar = Path.Combine (outputDirectory, "source.jar");
			var referenceJar = Path.Combine (outputDirectory, "reference.jar");
			var task = new ClassParseArgumentsTestTask {
				BuildEngine = engine,
				OutputFile = output,
				ReferenceOutputFile = referenceOutput,
				ReferenceJars = [new TaskItem (referenceJar)],
				SourceJars = [new TaskItem (sourceJar)],
			};

			task.GenerateArguments ();

			var responseFile = File.ReadAllLines (Path.Combine (outputDirectory, "class-parse.rsp"));
			Assert.That (responseFile, Does.Contain ($"--reference-output=\"{referenceOutput}\""));
			Assert.That (responseFile, Does.Contain ($"--reference=\"{referenceJar}\""));
			Assert.That (responseFile, Does.Contain ($"\"{sourceJar}\""));
		}

		[Test]
		public void ConsoleRunnerStopsAdbWhenOutputIsInvalid ()
		{
			if (OperatingSystem.IsWindows ())
				Assert.Ignore ("The fake adb is a Unix shell executable.");

			var outputDirectory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (outputDirectory);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = outputDirectory;
			string adb = Path.Combine (outputDirectory, "adb");
			string pidFile = Path.Combine (outputDirectory, "adb.pid");
			File.Delete (pidFile);
			File.WriteAllText (adb, """
				#!/bin/sh
				echo "$$" > "$ADB_PID_FILE"
				echo 'INSTRUMENTATION_STATUS: stdout-b64=not-valid-base64!'
				exec sleep 60
				""");
			if (!OperatingSystem.IsWindows ())
				File.SetUnixFileMode (adb, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

			var startInfo = new ProcessStartInfo (Path.Combine (TestEnvironment.DotNetPreviewDirectory, "dotnet")) {
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				ArgumentList = {
					Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "Microsoft.Android.Run.dll"),
					"--adb", adb, "--package", "test", "--instrument", "test", "--console",
				},
			};
			startInfo.Environment ["ADB_PID_FILE"] = pidFile;
			var result = Process.RunAndCaptureText (startInfo, TimeSpan.FromSeconds (15));
			bool stillRunning = false;
			if (File.Exists (pidFile)) {
				int pid = int.Parse (File.ReadAllText (pidFile));
				try {
					using var process = Process.GetProcessById (pid);
					stillRunning = !process.HasExited;
					if (stillRunning) {
						process.Kill ();
						process.WaitForExit ();
					}
				} catch (ArgumentException) {
				}
			}
			Assert.That (result.ExitStatus.Canceled, Is.False, "The runner should not hang on invalid output.");
			Assert.That (result.ExitStatus.ExitCode, Is.Not.Zero);
			Assert.That (result.StandardError, Does.Contain ("Base-64"));
			Assert.That (File.Exists (pidFile), Is.True, "The fake adb should have started.");
			Assert.That (stillRunning, Is.False, "The runner must terminate adb when processing its output fails.");
		}

		[Test]
		public void ConsoleRunnerDoesNotInheritUnrelatedHandles ()
		{
			if (OperatingSystem.IsWindows ())
				Assert.Ignore ("The fake adb is a Unix shell executable.");

			var outputDirectory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (outputDirectory);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = outputDirectory;
			string adb = Path.Combine (outputDirectory, "adb");
			string sentinel = Path.Combine (outputDirectory, "sentinel");
			File.WriteAllText (sentinel, "sentinel");
			File.WriteAllText (adb, """
				#!/bin/sh
				if [ "$(cat "/dev/fd/$PROBE_FD" 2>/dev/null)" = sentinel ]; then
					echo 'INSTRUMENTATION_STATUS: stdout-b64=aW5oZXJpdGVkCg=='
				else
					echo 'INSTRUMENTATION_STATUS: stdout-b64=Y2xvc2VkCg=='
				fi
				echo 'INSTRUMENTATION_RESULT: exitCode=0'
				""");
			if (!OperatingSystem.IsWindows ())
				File.SetUnixFileMode (adb, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

			using var stream = File.OpenRead (sentinel);
			var handle = stream.SafeFileHandle;
			ProcessStartInfo CreateStartInfo (string fileName) {
				var startInfo = new ProcessStartInfo (fileName) {
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					InheritedHandles = [handle],
				};
				startInfo.Environment ["PROBE_FD"] = handle.DangerousGetHandle ().ToInt64 ().ToString ();
				return startInfo;
			}

			var control = Process.RunAndCaptureText (CreateStartInfo (adb), TimeSpan.FromSeconds (15));
			Assert.That (control.ExitStatus.ExitCode, Is.Zero);
			Assert.That (control.StandardOutput, Does.Contain ("stdout-b64=aW5oZXJpdGVkCg=="), control.StandardError);
			stream.Seek (0, SeekOrigin.Begin);

			var runner = CreateStartInfo (Path.Combine (TestEnvironment.DotNetPreviewDirectory, "dotnet"));
			runner.ArgumentList.Add (Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "Microsoft.Android.Run.dll"));
			foreach (var arg in new [] { "--adb", adb, "--package", "test", "--instrument", "test", "--console" })
				runner.ArgumentList.Add (arg);
			var result = Process.RunAndCaptureText (runner, TimeSpan.FromSeconds (15));
			Assert.That (result.ExitStatus.Canceled, Is.False);
			Assert.That (result.ExitStatus.ExitCode, Is.Zero, result.StandardError);
			Assert.That (result.StandardOutput, Is.EqualTo ("closed\n"));
		}
	}

	public class ClassParseTestTask : AndroidDotnetToolTask
	{
		public override string TaskPrefix => "TEST";
		protected override string GenerateCommandLineCommands ()
		{
			return GetCommandLineBuilder ().ToString ();
		}
	}

	public class ClassParseArgumentsTestTask : ClassParse
	{
		public string GenerateArguments ()
		{
			return GenerateCommandLineCommands ();
		}
	}
}
