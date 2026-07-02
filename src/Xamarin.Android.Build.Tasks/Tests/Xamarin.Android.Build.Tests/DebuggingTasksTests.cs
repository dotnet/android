using Microsoft.Build.Framework;
using NUnit.Framework;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using Xamarin.Android.Build;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Category ("Node-2")]
	[TestFixture, NonParallelizable]
	public class DebuggingTasksTests : BaseTest
	{
		[OneTimeSetUp]
		public void SetUp ()
		{
		}

		// https://github.com/xamarin/monodroid/blob/63bbeb076d809c74811a8001d38bf2e9e8672627/tests/msbuild/nunit/Xamarin.Android.Build.Tests/Xamarin.Android.Build.Tests/ResolveXamarinAndroidToolsTests.cs
		[Test]
		[Repeat (10)]
		public void TestResolveToolsExists ()
		{
			List<BuildErrorEventArgs> errors = new List<BuildErrorEventArgs>();
			List<BuildMessageEventArgs> messages = new List<BuildMessageEventArgs>();

			var path = Path.Combine ("temp", TestName);
			if (Directory.Exists (Path.Combine (Root, path)))
				Directory.Delete (Path.Combine (Root, path), recursive: true);

			var engine = new MockBuildEngine (TestContext.Out, errors: errors, messages: messages);
			var frameworksRoot = Path.Combine (TestEnvironment.DotNetPreviewDirectory, "packs", "Microsoft.NETCore.App.Ref");
			var mscorlibDll = Directory.GetFiles (frameworksRoot, "mscorlib.dll", SearchOption.AllDirectories).LastOrDefault ();
			var frameworksPath = Path.GetDirectoryName (mscorlibDll);
			var androidSdk = CreateFauxAndroidSdkDirectory (Path.Combine (path, "Sdk"), "24.0.1", new[]
			{
				new ApiInfo { Id = "23", Level = 23, Name = "Marshmallow", FrameworkVersion = "v6.0", Stable = true },
				new ApiInfo { Id = "26", Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
				new ApiInfo { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
				new ApiInfo { Id = "28", Level = 28, Name = "Pie", FrameworkVersion = "v9.0", Stable = true },
			});
			//var androidNdk = CreateFauxAndroidNdkDirectory (Path.Combine (path, "Ndk"));
			var javaSdk = CreateFauxJavaSdkDirectory (Path.Combine(path, "Java"), "1.8.0", out string javaExe, out string javacExe);
			var task = new ResolveXamarinAndroidTools () {
				BuildEngine = engine,
				AndroidNdkPath = null,
				AndroidSdkPath = androidSdk,
				JavaSdkPath = javaSdk,
				MonoAndroidToolsPath = TestEnvironment.AndroidMSBuildDirectory,
				ReferenceAssemblyPaths = new string[] {
					frameworksPath,
					TestEnvironment.MonoAndroidFrameworkDirectory,
				},
			};
			Assert.True (task.Execute (), "Task should have completed successfully.");
			Assert.AreEqual (0, errors.Count, "No Errors should have been raised");
			var expected = $"  Found FrameworkPath at {Path.GetFullPath (frameworksPath)}";
			var firstTaskExecMessages = messages.Select (x => x.Message)?.ToList ();
			Assert.IsNotNull (firstTaskExecMessages, "First execution did not contain any messages!");
			CollectionAssert.Contains (firstTaskExecMessages, expected);
			CollectionAssert.DoesNotContain (firstTaskExecMessages, "  Using cached AndroidSdk values");
			CollectionAssert.DoesNotContain (firstTaskExecMessages, "  Using cached MonoDroidSdk values");

			Assert.True (task.Execute (), "Task should have completed successfully.");
			Assert.AreEqual (0, errors.Count, "No Errors should have been raised");
			var secondTaskExecMessages = messages.Select (x => x.Message)?.ToList ();
			Assert.IsNotNull (secondTaskExecMessages, "Second execution did not contain any messages!");
			CollectionAssert.Contains (secondTaskExecMessages, expected);
			CollectionAssert.Contains (secondTaskExecMessages, "  Using cached AndroidSdk values");
			CollectionAssert.Contains (secondTaskExecMessages, "  Using cached MonoDroidSdk values");
		}

	}

	/// <summary>
	/// Unit tests for <see cref="FastDeploy"/> helper methods that do not require a device.
	/// </summary>
	[TestFixture]
	public class FastDeployTests
	{
		// Canonical transient race output produced by the Android run-as tool when
		// the per-user data directory has not yet materialized after pm install.
		static readonly string [] TransientRaceOutputs = {
			"run-as: couldn't stat /data/user/0/com.example.app: No such file or directory",
			"run-as: couldn't stat /data/user/10/com.example.app: No such file or directory",
			// Verify case-insensitivity of the detection.
			"run-as: Couldn't Stat /data/user/0/com.example.app: No Such File Or Directory",
			// Extra surrounding whitespace / newlines as they may appear in raw adb output.
			"  run-as: couldn't stat /data/user/0/com.example.app: No such file or directory\n",
		};

		// Genuine run-as failures that must NOT be swallowed by the retry loop.
		static readonly string [] NonTransientOutputs = {
			// Null / empty — first guard in the implementation.
			null,
			"",
			// Successful pwd output — the data directory already exists.
			"/data/user/0/com.example.app",
			// Package not debuggable.
			"run-as: package 'com.example.app' is not debuggable",
			// Package not installed.
			"run-as: package 'com.example.app' is unknown",
			// Permission denied (SELinux / policy).
			"run-as: couldn't stat /data/user/0/com.example.app: Permission denied",
			// Only one of the two required substrings — must not match.
			"run-as: couldn't stat /data/user/0/com.example.app",
			"No such file or directory",
		};

		[TestCaseSource (nameof (TransientRaceOutputs))]
		public void IsTransientRunAsStatRace_ReturnsTrueForRaceSignature (string output)
		{
			Assert.IsTrue (FastDeploy.IsTransientRunAsStatRace (output),
				$"Expected transient-race detection for: {output}");
		}

		[TestCaseSource (nameof (NonTransientOutputs))]
		public void IsTransientRunAsStatRace_ReturnsFalseForNonTransientOutput (string output)
		{
			Assert.IsFalse (FastDeploy.IsTransientRunAsStatRace (output),
				$"Expected no transient-race detection for: {output}");
		}
	}
}
