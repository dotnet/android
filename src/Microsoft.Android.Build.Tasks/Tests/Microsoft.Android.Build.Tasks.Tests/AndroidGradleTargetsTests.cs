using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class AndroidGradleTargetsTests : BaseTest
{
	[Test]
	public void CleanThenBuildPassesLiteralPathsAndResolvedSdkEnvironment ()
	{
		if (OperatingSystem.IsWindows ()) {
			Assert.Ignore ("The fake Gradle wrapper is a POSIX shell script.");
			return;
		}

		string directory = Path.Combine (Root, "temp", TestName, "gradle $HOME `echo x` %PATH% apostrophe's");
		string gradleProject = Path.Combine (directory, "lib");
		string sdk = Path.Combine (directory, "sdk");
		string jdk = Path.Combine (directory, "jdk");
		string stamps = Path.Combine (directory, "stamp");
		foreach (string d in new [] { gradleProject, sdk, jdk, stamps }) {
			Directory.CreateDirectory (d);
		}
		string log = Path.Combine (gradleProject, "invocations.log");
		if (File.Exists (log)) {
			File.Delete (log);
		}
		string buildGradle = Path.Combine (gradleProject, "build.gradle.kts");
		File.WriteAllText (buildGradle, "");
		string initScript = Path.Combine (directory, "init.gradle.kts");
		File.WriteAllText (initScript, "");
		string wrapper = Path.Combine (gradleProject, "gradlew");
		File.WriteAllText (wrapper, """
			#!/bin/sh
			{
				printf 'ANDROID_HOME=%s\n' "$ANDROID_HOME"
				printf 'JAVA_HOME=%s\n' "$JAVA_HOME"
				for arg in "$@"; do
					printf 'ARG=%s\n' "$arg"
				done
				printf 'END\n'
			} >> "$(dirname "$0")/invocations.log"

			""");
		File.SetUnixFileMode (wrapper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

		string targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks",
			"MSBuild", "Xamarin", "Android", "Microsoft.Android.Sdk.Bindings.Gradle.targets");
		string intermediate = Path.Combine (directory, "obj") + Path.DirectorySeparatorChar;
		string project = Path.Combine (directory, "gradle.proj");
		// Paths come from $(MSBuildProjectDirectory), as in real projects, so MSBuild keeps special characters escaped.
		new XDocument (
			new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("IntermediateOutputPath", "obj/"),
					new XElement ("_AndroidStampDirectory", "$(MSBuildProjectDirectory)/stamp/"),
					new XElement ("_AGPInitScriptPath", "$(MSBuildProjectDirectory)/init.gradle.kts")),
				new XElement ("ItemGroup",
					new XElement ("AndroidGradleProject", new XAttribute ("Include", "lib/build.gradle.kts"))),
				new XElement ("Import", new XAttribute ("Project", targets)),
				new XElement ("Target", new XAttribute ("Name", "_SetupMSBuildAllProjects")),
				// Mirrors AndroidPrepareForBuild, which resolves the SDK/JDK only during Build.
				new XElement ("Target", new XAttribute ("Name", "AndroidPrepareForBuild"),
					new XElement ("PropertyGroup",
						new XElement ("AndroidSdkDirectory", "$(MSBuildProjectDirectory)/sdk"),
						new XElement ("JavaSdkDirectory", "$(MSBuildProjectDirectory)/jdk"))),
				new XElement ("Target", new XAttribute ("Name", "Clean"), new XAttribute ("DependsOnTargets", "_CleanAndroidGradleProjects")),
				new XElement ("Target", new XAttribute ("Name", "Build"), new XAttribute ("DependsOnTargets", "_BuildAndroidGradleProjects"))))
			.Save (project);

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:Clean;Build", "-v:normal", "-nr:false");
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);

		FileAssert.Exists (log, "The wrapper path must reach the shell literally.\n" + result.Output);
		var invocations = File.ReadAllText (log).Split ("END\n", StringSplitOptions.RemoveEmptyEntries)
			.Select (i => i.Split ('\n', StringSplitOptions.RemoveEmptyEntries))
			.ToArray ();
		Assert.AreEqual (2, invocations.Length, File.ReadAllText (log));

		AssertInvocation (invocations [0], "clean");
		AssertInvocation (invocations [1], "assembleRelease");
		CollectionAssert.Contains (invocations [1], $"ANDROID_HOME={sdk}", "assemble must see the SDK resolved after Clean ran.");
		CollectionAssert.Contains (invocations [1], $"JAVA_HOME={jdk}", "assemble must see the JDK resolved after Clean ran.");

		void AssertInvocation (string [] lines, string task)
		{
			var args = lines.Where (l => l.StartsWith ("ARG=", StringComparison.Ordinal)).Select (l => l.Substring (4)).ToArray ();
			Assert.AreEqual (5, args.Length, string.Join ("\n", lines));
			Assert.AreEqual (task, args [0]);
			StringAssert.StartsWith ($"-PnetAndroidBuildDirOverride={intermediate}gradle/Release-", args [1]);
			StringAssert.EndsWith ("/.", args [1]);
			Assert.AreEqual ("--init-script", args [2]);
			Assert.AreEqual (initScript, args [3]);
			Assert.AreEqual ("--no-daemon", args [4]);
		}
	}
}
