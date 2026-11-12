#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Microsoft.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestFixture]
	public class LintTests : BaseTest
	{
		const string TaskName = "Microsoft.Android.Tasks.Lint";

		class TestableLint : Lint
		{
			public void Feed (string line) => LogEventsFromTextOutput (line, MessageImportance.Normal);
		}

		[Test]
		public void ParsesLintOutput ()
		{
			var errors = new List<BuildErrorEventArgs> ();
			var warnings = new List<BuildWarningEventArgs> ();
			var targetDirectory = Path.Combine ("obj", "android");
			var task = new TestableLint {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors, warnings),
				TargetDirectory = targetDirectory,
			};
			foreach (var line in new [] {
				"AndroidManifest.xml:19: Warning: <uses-permission> tag appears after <application> tag [ManifestOrder]",
				"<uses-permission android:name=\"android.permission.INTERNET\" />",
				"\t~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~",
				"android/src/sdldroidtest/SDLSurface.java:43: Error: Call requires API level 21 (current min is 10): new android.view.SurfaceView [NewApi]",
				"super (p0, p1, p2, p3);",
				"~~~~~",
				"Warning: The resource R.layout.main appears to be unused [UnusedResources]",
				"Warning: The resource R.string.app_name appears to be unused [UnusedResources]",
				"\t^",
			}) {
				task.Feed (line);
			}

			Assert.AreEqual (3, warnings.Count);
			Assert.AreEqual ("XA0102", warnings [0].Code);
			Assert.AreEqual (Path.Combine (targetDirectory, "AndroidManifest.xml"), warnings [0].File);
			Assert.AreEqual (19, warnings [0].LineNumber);
			Assert.AreEqual (1, warnings [0].ColumnNumber);
			Assert.AreEqual (" <uses-permission> tag appears after <application> tag [ManifestOrder]", warnings [0].Message);
			Assert.AreEqual ("XA0102", warnings [1].Code);
			Assert.AreEqual ("The resource R.layout.main appears to be unused [UnusedResources]", warnings [1].Message);
			Assert.AreEqual ("XA0102", warnings [2].Code);
			Assert.AreEqual ("The resource R.string.app_name appears to be unused [UnusedResources]", warnings [2].Message);

			Assert.AreEqual (1, errors.Count);
			Assert.AreEqual ("XA0103", errors [0].Code);
			Assert.AreEqual (Path.Combine (targetDirectory, "android/src/sdldroidtest/SDLSurface.java"), errors [0].File);
			Assert.AreEqual (43, errors [0].LineNumber);
			Assert.AreEqual (0, errors [0].ColumnNumber);
			Assert.AreEqual (" Call requires API level 21 (current min is 10): new android.view.SurfaceView [NewApi]", errors [0].Message);
		}

		[Test]
		public void TaskRegistrationUsesFullAndCorePair ()
		{
			var entries = LoadRegistrations ();
			Assert.AreEqual (2, entries.Length);
			Assert.That (entries.Select (e => (string?) e.Attribute ("AssemblyFile")), Is.All.EqualTo ("$(_MicrosoftAndroidBuildTasksAssembly)"));
			Assert.That (entries.Select (e => (string?) e.Attribute ("Runtime")), Is.All.EqualTo ("NET"));
			var full = entries.Single (e => (string?) e.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Full' ");
			var core = entries.Single (e => (string?) e.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Core' ");
			Assert.AreEqual ("TaskHostFactory", (string?) full.Attribute ("TaskFactory"));
			Assert.IsNull (core.Attribute ("TaskFactory"));
		}

		[TestCase (TaskName)]
		[TestCase ("Lint")]
		public void RegisteredTaskNamesExecute (string taskName)
		{
			string directory = Path.Combine (Root, "temp", TestName);
			string toolPath = Path.Combine (directory, "tools");
			string targetDirectory = Path.Combine (directory, "android project");
			Directory.CreateDirectory (toolPath);
			Directory.CreateDirectory (targetDirectory);
			CreateFakeLint (toolPath);

			string project = Path.Combine (directory, "lint.proj");
			new XDocument (new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (Lint).Assembly.Location)),
				LoadRegistrations (),
				new XElement ("Target", new XAttribute ("Name", "Build"),
					new XElement (taskName,
						new XAttribute ("ToolPath", toolPath),
						new XAttribute ("TargetDirectory", targetDirectory),
						new XAttribute ("IntermediateOutputPath", directory),
						new XAttribute ("JavaSdkPath", directory)))))
				.Save (project);

			string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
			var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:Build", "-v:minimal", "-nr:false");
			string output = result.Output + result.Error;
			Assert.AreEqual (0, result.ExitCode, output);
			// `lint --version` reported 26.0.2, so the issues introduced up to 26.0.2 are disabled.
			StringAssert.Contains ($"warning XA0102: args=--quiet --disable Instantiatable,StaticFieldLeak,UnusedResources,HardcodedDebugMode {targetDirectory}", output);
			StringAssert.IsMatch (@"strings\.xml\(3,\d+\): warning XA0102:  Unused resource \[UnusedResources\]", output);
		}

		static void CreateFakeLint (string toolPath)
		{
			if (OperatingSystem.IsWindows ()) {
				File.WriteAllText (Path.Combine (toolPath, "lint.bat"), """
					@echo off
					if "%~1"=="--version" (
						echo lint: version 26.0.2
						exit /b 0
					)
					rem Match the shell fixture's $* expansion by removing argument quoting.
					echo Warning: args=%~1 %~2 %~3 %~4
					echo ^^
					echo res\values\strings.xml:3: Warning: Unused resource [UnusedResources]
					echo     ~~~~
					""".Replace ("\n", "\r\n"));
				return;
			}
			string lint = Path.Combine (toolPath, "lint");
			File.WriteAllText (lint, """
				#!/bin/sh
				if [ "$1" = "--version" ]; then
					echo "lint: version 26.0.2"
					exit 0
				fi
				echo "Warning: args=$*"
				echo "^"
				echo "res/values/strings.xml:3: Warning: Unused resource [UnusedResources]"
				echo "    ~~~~"
				""");
			File.SetUnixFileMode (lint, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}

		static XElement [] LoadRegistrations ()
		{
			string commonTargets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
				"src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets");
			return XDocument.Load (commonTargets).Descendants ()
				.Where (element => element.Name.LocalName == "UsingTask" && (string?) element.Attribute ("TaskName") == TaskName)
				.Select (element => {
					var registration = new XElement (element);
					registration.Name = "UsingTask";
					return registration;
				})
				.ToArray ();
		}
	}
}
