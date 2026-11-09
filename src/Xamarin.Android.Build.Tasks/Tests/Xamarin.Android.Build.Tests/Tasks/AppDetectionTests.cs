#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class AppDetectionTests
	{
		[TestCase (false)]
		[TestCase (true)]
		public async Task WaitsForPendingQueryInMSBuild (bool multithreaded)
		{
			using var detection = new Detection ("missing");
			var assembly = Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "Xamarin.Android.Build.Debugging.Tasks.dll");
			var project = new XElement ("Project",
				new XElement ("UsingTask", new XAttribute ("TaskName", "Xamarin.Android.Tasks.DetectIfAppWasUninstalled"),
					new XAttribute ("AssemblyFile", assembly)),
				new XElement ("UsingTask", new XAttribute ("TaskName", "Xamarin.Android.Tasks.WaitForAppDetection"),
					new XAttribute ("AssemblyFile", assembly)),
				new XElement ("Target", new XAttribute ("Name", "Probe"),
					new XElement ("DetectIfAppWasUninstalled", new XAttribute ("AdbToolPath", "tools"),
						new XAttribute ("AdbToolExe", detection.Executable), new XAttribute ("PackageName", "com.example.app"),
						new XAttribute ("UploadFlagFile", "upload.flag"), new XAttribute ("UserID", "10")),
					new XElement ("Touch", new XAttribute ("Files", "producer-returned"), new XAttribute ("AlwaysCreate", "true")),
					new XElement ("WaitForAppDetection"),
					new XElement ("Error", new XAttribute ("Condition", "Exists('upload.flag')"),
						new XAttribute ("Text", "The waiter returned before the package query deleted the flag."))));
			var log = await RunMSBuild (detection, project, multithreaded, "-warnaserror:MSB4286,MSB4287,MSB4288");
			StringAssert.DoesNotContain ("TaskHostFactory", log);
			StringAssert.Contains ("--user 10", File.ReadAllText (Path.Combine (detection.Directory, "query-arguments")));
		}

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (true, true)]
		public async Task DeviceSelectionSurvivesDeviceOrderChanges (bool multithreaded, bool embedAssemblies)
		{
			using var detection = new Detection ("switching");
			var project = new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("AndroidApplication", "true"),
					new XElement ("EmbedAssembliesIntoApk", embedAssemblies),
					new XElement ("_AndroidPackage", "com.example.app"),
					new XElement ("RuntimeIdentifiers", "android-arm64"),
					new XElement ("AndroidLaunchActivity", "MainActivity"),
					new XElement ("_AndroidComputeRunArgumentsDependsOn", "_AndroidAdbToolPath"),
					new XElement ("_XamarinAndroidBuildTasksAssembly", Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "Xamarin.Android.Build.Tasks.dll")),
					new XElement ("AdbToolPath", Path.Combine (detection.Directory, "tools")),
					new XElement ("AdbToolExe", detection.Executable)),
				new XElement ("Import", new XAttribute ("Project",
					Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "Xamarin.Android.Common.Debugging.targets"))),
				new XElement ("Import", new XAttribute ("Project",
					Path.Combine (TestEnvironment.DotNetPreviewAndroidSdkDirectory, "targets", "Microsoft.Android.Sdk.Application.targets"))),
				new XElement ("Target", new XAttribute ("Name", "_SetLatestTargetFrameworkVersion")),
				new XElement ("Target", new XAttribute ("Name", "_ValidateAndroidPackageProperties")),
				new XElement ("Target", new XAttribute ("Name", "DetectionStarted"),
					new XAttribute ("AfterTargets", "_DetectIfAppWasUninstalled"),
					new XElement ("Touch", new XAttribute ("Files", "producer-returned"), new XAttribute ("AlwaysCreate", "true"))),
				new XElement ("Target", new XAttribute ("Name", "Probe"),
					new XAttribute ("DependsOnTargets", "_GetPrimaryCpuAbi;_DetectIfAppWasUninstalled;_GetUploadInputs;_AndroidComputeRunArguments"),
					new XElement ("Error", new XAttribute ("Condition", "Exists('upload.flag')"),
						new XAttribute ("Text", "Detection checked the installed app on the wrong device.")),
					new XElement ("Error", new XAttribute ("Condition", "'$(AdbTarget)' != '-s emulator-5554'"),
						new XAttribute ("Text", "Deployment did not receive the selected serial.")),
					new XElement ("Error", new XAttribute ("Condition", "!$(RunArguments.Contains('-s emulator-5554'))"),
						new XAttribute ("Text", "Run arguments lost the selected serial."))));
			// GetPrimaryCpuAbi is still hosted; only the detector and waiter are MT tasks.
			await RunMSBuild (detection, project, multithreaded, "-p:AdbTarget=-e");
			StringAssert.StartsWith ("-s emulator-5554 ", File.ReadAllText (Path.Combine (detection.Directory, "query-arguments")));
		}

		static async Task<string> RunMSBuild (Detection detection, XElement project, bool multithreaded, params string [] arguments)
		{
			project.Save (Path.Combine (detection.Directory, "detection.proj"));
			var startInfo = new ProcessStartInfo (Path.Combine (TestEnvironment.DotNetPreviewDirectory, "dotnet")) {
				WorkingDirectory = detection.Directory,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			foreach (var pair in detection.Environment)
				startInfo.Environment [pair.Key] = pair.Value;
			startInfo.Environment.Remove ("WAIT_TEST_ISOLATED");
			string binlog = Path.Combine (TestContext.CurrentContext.WorkDirectory,
				$"{TestContext.CurrentContext.Test.Name}.binlog");
			foreach (var argument in new [] { "msbuild", "detection.proj", "-t:Probe", "-m", "-nr:false", "-v:diag",
				$"-bl:{binlog}" }.Concat (arguments))
				startInfo.ArgumentList.Add (argument);
			if (multithreaded)
				startInfo.ArgumentList.Add ("-mt");
			using var process = new Process { StartInfo = startInfo };
			Assert.IsTrue (process.Start ());
			var output = process.StandardOutput.ReadToEndAsync ();
			var error = process.StandardError.ReadToEndAsync ();
			try {
				Assert.IsTrue (SpinWait.SpinUntil (() => process.HasExited ||
					(File.Exists (Path.Combine (detection.Directory, "query-started")) &&
					 File.Exists (Path.Combine (detection.Directory, "producer-returned"))), TimeSpan.FromSeconds (20)),
					"The producer did not start its background query.");
				if (process.HasExited)
					Assert.Fail (await output + await error);
				Assert.IsFalse (process.WaitForExit (100), "MSBuild must wait while the query is pending.");
				detection.Release ();
				Assert.IsTrue (process.WaitForExit (20000), "MSBuild did not finish after releasing the query.");
				var log = await output + await error;
				TestContext.Out.WriteLine ($"Binlog: {binlog}");
				Assert.AreEqual (0, process.ExitCode, log);
				StringAssert.Contains ("DetectIfAppWasUninstalled Completed.", log);
				Assert.IsFalse (File.Exists (detection.Flag));
				return log;
			} finally {
				detection.Release ();
				if (!process.HasExited) {
					process.Kill (entireProcessTree: true);
					process.WaitForExit ();
				}
			}
		}

		[Test]
		public void QueryErrorPreservesFlag ()
		{
			using var detection = new Detection ("fail");
			var messages = new List<BuildMessageEventArgs> ();
			var engine = new MockBuildEngine (TestContext.Out, messages: messages);
			var producer = detection.CreateProducer (engine);
			producer.AdbToolPath = null;
			Assert.IsTrue (producer.Execute ());
			detection.Release ();
			var waiter = new WaitForAppDetection { BuildEngine = engine, TaskEnvironment = producer.TaskEnvironment };
			Assert.IsTrue (waiter.Execute ());
			Assert.IsTrue (File.Exists (detection.Flag));
			var failure = messages.Single (m => m.Message?.StartsWith ("DetectIfAppWasUninstalled failed with", StringComparison.Ordinal) == true);
			StringAssert.Contains ("Package manager unavailable", failure.Message);
			StringAssert.Contains (Path.Combine (detection.Directory, "temp"),
				File.ReadAllText (Path.Combine (detection.Directory, "query-directory")).Trim ());
		}

		[Test]
		public void QueryAndDeviceCacheAreProjectSpecific ()
		{
			using var first = new Detection ("installed");
			using var second = new Detection ("missing");
			var engine = new MockBuildEngine (TestContext.Out);
			var firstProducer = first.CreateProducer (engine);
			var secondProducer = second.CreateProducer (engine);
			secondProducer.AdbToolPath = Path.Combine (first.Directory, "tools");
			Assert.IsTrue (firstProducer.Execute ());
			Assert.IsTrue (secondProducer.Execute ());
			var firstQuery = GetQuery (engine, firstProducer.TaskEnvironment);
			Assert.AreNotSame (firstQuery, GetQuery (engine, secondProducer.TaskEnvironment));
			Assert.IsFalse (firstQuery.IsCompleted);
			second.Release ();
			Assert.IsTrue (new WaitForAppDetection { BuildEngine = engine, TaskEnvironment = secondProducer.TaskEnvironment }.Execute ());
			Assert.IsFalse (firstQuery.IsCompleted, "Waiting in one project must not wait for another project's query.");
			Assert.IsFalse (File.Exists (second.Flag));
			StringAssert.Contains ("-s missing", File.ReadAllText (Path.Combine (second.Directory, "query-arguments")));
			Assert.IsTrue (File.Exists (first.Flag));
			first.Release ();
			Assert.IsTrue (new WaitForAppDetection { BuildEngine = engine, TaskEnvironment = firstProducer.TaskEnvironment }.Execute ());
			Assert.IsTrue (File.Exists (first.Flag));
		}

		[Test]
		public void CancellingProducerPreservesFlag ()
		{
			using var detection = new Detection ("missing");
			var engine = new MockBuildEngine (TestContext.Out);
			var producer = detection.CreateProducer (engine);
			Assert.IsTrue (producer.Execute ());
			producer.Cancel ();
			Assert.IsTrue (new WaitForAppDetection { BuildEngine = engine, TaskEnvironment = producer.TaskEnvironment }.Execute ());
			Assert.IsTrue (GetQuery (engine, producer.TaskEnvironment).IsCanceled);
			Assert.IsTrue (File.Exists (detection.Flag));
		}

		[Test]
		public void WaiterPreservesFaultAndCancellationBehavior ()
		{
			var errors = new List<BuildErrorEventArgs> ();
			var engine = new MockBuildEngine (TextWriter.Null, errors: errors);
			var environment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (
				Path.GetTempPath (), new Dictionary<string, string> ());
			var key = (DetectIfAppWasUninstalled.GetPackagesAsyncKey, environment.ProjectDirectory);
			Assert.IsTrue (new WaitForAppDetection { BuildEngine = engine, TaskEnvironment = environment }.Execute ());
			engine.RegisterTaskObjectAssemblyLocal (key, Task.FromException<Exception?> (new InvalidOperationException ("query fault")),
				RegisteredTaskObjectLifetime.Build);
			Assert.IsFalse (new WaitForAppDetection { BuildEngine = engine, TaskEnvironment = environment }.Execute ());
			Assert.AreEqual ("XAWFAD7009", errors.Single ().Code);
			engine.UnregisterTaskObjectAssemblyLocal (key, RegisteredTaskObjectLifetime.Build);
			var pending = new TaskCompletionSource<Exception?> ();
			engine.RegisterTaskObjectAssemblyLocal (key, pending.Task, RegisteredTaskObjectLifetime.Build);
			var waiter = new WaitForAppDetection { BuildEngine = engine, TaskEnvironment = environment };
			var wait = Task.Run (() => waiter.Execute ());
			try {
				Assert.IsFalse (wait.Wait (100));
				waiter.Cancel ();
				Assert.IsTrue (wait.Wait (TimeSpan.FromSeconds (5)), "Cancellation must release the waiter.");
				Assert.IsTrue (wait.Result);
				Assert.IsFalse (pending.Task.IsCompleted, "Cancelling the waiter must not cancel the producer.");
			} finally {
				pending.TrySetResult (null);
			}
		}

		static Task<Exception?> GetQuery (MockBuildEngine engine, TaskEnvironment environment) =>
			engine.GetRegisteredTaskObjectAssemblyLocal<Task<Exception?>> (
				(DetectIfAppWasUninstalled.GetPackagesAsyncKey, environment.ProjectDirectory), RegisteredTaskObjectLifetime.Build) ??
				throw new InvalidOperationException ("The producer did not register its query in this project.");

		sealed class Detection : IDisposable
		{
			public string Directory { get; } = Path.Combine (Path.GetTempPath (), "app-detection-" + Guid.NewGuid ().ToString ("N"));
			public string Executable => OperatingSystem.IsWindows () ? "adb.cmd" : "adb";
			public string Flag => Path.Combine (Directory, "upload.flag");
			public Dictionary<string, string> Environment { get; }
			DetectIfAppWasUninstalled? producer;

			public Detection (string result)
			{
				System.IO.Directory.CreateDirectory (Path.Combine (Directory, "tools"));
				System.IO.Directory.CreateDirectory (Path.Combine (Directory, "temp"));
				Environment = new Dictionary<string, string> {
					["WAIT_TEST_ROOT"] = Directory,
					["WAIT_TEST_RESULT"] = result,
					["WAIT_TEST_ISOLATED"] = "true",
					["TMP"] = Path.Combine (Directory, "temp"),
					["TMPDIR"] = Path.Combine (Directory, "temp"),
					["PATH"] = Path.Combine (Directory, "tools") + Path.PathSeparator + System.Environment.GetEnvironmentVariable ("PATH"),
				};
				foreach (var name in new [] { "SystemRoot", "ComSpec" }) {
					if (System.Environment.GetEnvironmentVariable (name) is string value)
						Environment [name] = value;
				}
				File.WriteAllText (Flag, "");
				var adb = Path.Combine (Directory, "tools", Executable);
				File.WriteAllText (adb, OperatingSystem.IsWindows () ? """
					@echo off
					if "%WAIT_TEST_ISOLATED%"=="true" if defined USERNAME exit /b 43
					if "%1"=="devices" (
					    echo List of devices attached
					    if "%WAIT_TEST_RESULT%"=="switching" (
					        if exist "%WAIT_TEST_ROOT%\devices-listed" (
					            echo emulator-5556 device model:Test
					            echo emulator-5554 device model:Test
					        ) else (
					            echo emulator-5554 device model:Test
					            echo emulator-5556 device model:Test
					        )
					        echo listed > "%WAIT_TEST_ROOT%\devices-listed"
					        exit /b 0
					    )
					    echo %WAIT_TEST_RESULT% device model:Test
					    exit /b 0
					)
					if "%~4"=="getprop" (
					    if "%~5"=="ro.build.version.sdk" (echo 34) else (echo arm64-v8a)
					    exit /b 0
					)
					if "%~4"=="getprop ro.product.cpu.abilist64" (
					    echo arm64-v8a
					    exit /b 0
					)
					echo %* > "%WAIT_TEST_ROOT%\query-arguments"
					echo %CD% > "%WAIT_TEST_ROOT%\query-directory"
					echo started > "%WAIT_TEST_ROOT%\query-started"
					:wait
					if not exist "%WAIT_TEST_ROOT%\release" goto wait
					if "%WAIT_TEST_RESULT%"=="fail" (
					    echo Package manager unavailable 1>&2
					    exit /b 42
					)
					if "%WAIT_TEST_RESULT%"=="installed" echo package:com.example.app
					if "%WAIT_TEST_RESULT%"=="switching" if "%2"=="emulator-5556" echo package:com.example.app
					exit /b 0
					""" : """
					#!/bin/sh
					if [ "$WAIT_TEST_ISOLATED" = true ] && [ -n "$HOME" ]; then exit 43; fi
					if [ "$1" = devices ]; then
					    echo "List of devices attached"
					    if [ "$WAIT_TEST_RESULT" = switching ]; then
					        if [ -f "$WAIT_TEST_ROOT/devices-listed" ]; then
					            echo "emulator-5556 device model:Test"
					            echo "emulator-5554 device model:Test"
					        else
					            echo "emulator-5554 device model:Test"
					            echo "emulator-5556 device model:Test"
					        fi
					        echo listed > "$WAIT_TEST_ROOT/devices-listed"
					        exit 0
					    fi
					    echo "$WAIT_TEST_RESULT device model:Test"
					    exit 0
					fi
					if [ "$4" = getprop ]; then
					    if [ "$5" = ro.build.version.sdk ]; then echo 34; else echo arm64-v8a; fi
					    exit 0
					fi
					if [ "$4" = "getprop ro.product.cpu.abilist64" ]; then echo arm64-v8a; exit 0; fi
					echo "$*" > "$WAIT_TEST_ROOT/query-arguments"
					echo "$PWD" > "$WAIT_TEST_ROOT/query-directory"
					echo started > "$WAIT_TEST_ROOT/query-started"
					while [ ! -f "$WAIT_TEST_ROOT/release" ]; do sleep 0.05; done
					if [ "$WAIT_TEST_RESULT" = fail ]; then
					    echo "Package manager unavailable" >&2
					    exit 42
					fi
					if [ "$WAIT_TEST_RESULT" = installed ]; then echo "package:com.example.app"; fi
					if [ "$WAIT_TEST_RESULT" = switching ] && [ "$2" = emulator-5556 ]; then echo "package:com.example.app"; fi
					exit 0
					""");
				if (!OperatingSystem.IsWindows ())
					File.SetUnixFileMode (adb, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			}

			public DetectIfAppWasUninstalled CreateProducer (MockBuildEngine engine) => producer = new DetectIfAppWasUninstalled {
				BuildEngine = engine,
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (Directory, Environment),
				AdbToolPath = "tools",
				AdbToolExe = Executable,
				PackageName = "com.example.app",
				UploadFlagFile = "upload.flag",
			};

			public void Release () => File.WriteAllText (Path.Combine (Directory, "release"), "");

			public void Dispose ()
			{
				Release ();
				if (producer?.BuildEngine is IBuildEngine4 engine) {
					producer.Cancel ();
					var query = engine.GetRegisteredTaskObjectAssemblyLocal<Task<Exception?>> (
						(DetectIfAppWasUninstalled.GetPackagesAsyncKey, producer.TaskEnvironment.ProjectDirectory),
						RegisteredTaskObjectLifetime.Build);
					if (query != null) {
						try {
							Assert.IsTrue (SpinWait.SpinUntil (() => query.IsCompleted, TimeSpan.FromSeconds (5)),
								"The fake adb query did not stop.");
							query.GetAwaiter ().GetResult ();
						} catch (OperationCanceledException) {
							// Expected when cleaning up a failed test.
						}
					}
				}
				System.IO.Directory.Delete (Directory, recursive: true);
			}
		}
	}
}
