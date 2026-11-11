#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Platform (Exclude = "Win", Reason = "Uses a shell script as a fake `java`.")]
public class InstallApkSetTargetsTests : BaseTest
{
	[Test]
	public void CommandLine ()
	{
		var (exitCode, output, args) = Run ("exit 0", adbTarget: "-s emulator-5554", jdkVersion: "25.0.1", heapSize: "1G", javaOptions: "-Dfoo=bar");
		Assert.AreEqual (0, exitCode, output);
		CollectionAssert.AreEqual (new [] {
			"-Dfoo=bar",
			"--enable-native-access=ALL-UNNAMED",
			"--sun-misc-unsafe-memory-access=allow",
			"-Xmx1G",
			"-jar", "bundletool.jar",
			"install-apks",
			"--apks", Path.Combine (Root, "temp", TestName, "with spaces", "foo.apks"),
			"--adb", Path.Combine (Root, "temp", TestName, "with spaces", "adb"),
			"--device-id", "emulator-5554",
			"--allow-downgrade",
			"--modules", "_ALL_",
		}, args);
	}

	[Test]
	public void CommandLineWithModulesAndOlderJdk ()
	{
		var (exitCode, output, args) = Run ("exit 0", adbTarget: "", jdkVersion: "21.0.5", heapSize: "", javaOptions: "", modules: ["base", "feature"]);
		Assert.AreEqual (0, exitCode, output);
		CollectionAssert.AreEqual (new [] {
			"-jar", "bundletool.jar",
			"install-apks",
			"--apks", Path.Combine (Root, "temp", TestName, "with spaces", "foo.apks"),
			"--adb", Path.Combine (Root, "temp", TestName, "with spaces", "adb"),
			"--allow-downgrade",
			"--modules", "base,feature",
		}, args);
	}

	[Test]
	public void BundletoolExceptionIsReported ()
	{
		var (exitCode, output, _) = Run ("echo 'com.android.tools.build.bundletool.model.exceptions.CommandExecutionException: Installation of the app failed.'; exit 1");
		Assert.AreNotEqual (0, exitCode, output);
		StringAssert.Contains ($"foo.apks : error BT0000: com.android.tools.build.bundletool.model.exceptions.CommandExecutionException: Installation of the app failed.", output);
		StringAssert.DoesNotContain ("MSB3073", output);
	}

	[Test]
	public void UnknownFailureIsReported ()
	{
		var (exitCode, output, _) = Run ("echo 'something else went wrong'; exit 3");
		Assert.AreNotEqual (0, exitCode, output);
		StringAssert.Contains ("error BT0000: bundletool install-apks failed with exit code 3.", output);
	}

	(int ExitCode, string Output, string [] Args) Run (string body, string adbTarget = "", string jdkVersion = "", string heapSize = "", string javaOptions = "", string []? modules = null)
	{
		string directory = Path.Combine (Root, "temp", TestName, "with spaces");
		Directory.CreateDirectory (directory);
		string argsFile = Path.Combine (directory, "args.txt");
		string java = Path.Combine (directory, "java");
		File.WriteAllText (java, $"#!/bin/sh\nfor a in \"$@\"; do echo \"$a\"; done > \"{argsFile}\"\n{body}\n");
		File.SetUnixFileMode (java, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

		string targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets");
		var target = new XElement (XDocument.Load (targets).Descendants ()
			.Single (e => e.Name.LocalName == "Target" && (string?) e.Attribute ("Name") == "_DeployAppBundle"));
		foreach (var e in target.DescendantsAndSelf ())
			e.Name = e.Name.LocalName;
		target.Attribute ("DependsOnTargets")?.Remove ();

		string project = Path.Combine (directory, "install.proj");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement ("AndroidPackageFormat", "aab"),
				new XElement ("EmbedAssembliesIntoApk", "false"),
				new XElement ("JavaToolPath", directory),
				new XElement ("JavaOptions", javaOptions),
				new XElement ("JavaMaximumHeapSize", heapSize),
				new XElement ("_JdkVersion", jdkVersion),
				new XElement ("AndroidBundleToolJarPath", "bundletool.jar"),
				new XElement ("AdbToolPath", directory),
				new XElement ("AdbToolExe", "adb"),
				new XElement ("AdbTarget", adbTarget),
				new XElement ("_ApkSetIntermediate", Path.Combine (directory, "foo.apks"))),
			new XElement ("ItemGroup", (modules ?? []).Select (m => new XElement ("AndroidInstallModules", new XAttribute ("Include", m)))),
			target))
			.Save (project);

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:_DeployAppBundle", "-v:minimal", "-nr:false");
		string [] args = File.Exists (argsFile) ? File.ReadAllLines (argsFile) : [];
		return (result.ExitCode, result.Output + result.Error, args);
	}
}
