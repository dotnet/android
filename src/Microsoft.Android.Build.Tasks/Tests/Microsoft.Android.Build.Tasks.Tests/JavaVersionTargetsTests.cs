using System;
using System.IO;
using System.Xml.Linq;

using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class JavaVersionTargetsTests : BaseTest
{
	[TestCase ("absolute", "17.0.1", "21.0.8", "XA0031")]
	[TestCase ("relative", "17.0.1", "21.0.8", "XA0031")]
	[TestCase ("absolute", "22.0.1", "21.0.8", "XA0030")]
	[TestCase ("relative", "22.0.1", "21.0.8", "XA0030")]
	[TestCase ("absolute", "21.0.8", "17.0.1", "")]
	[TestCase ("relative", "21.0.8", "17.0.1", "")]
	[TestCase ("absolute", null, "21.0.8", "")]
	[TestCase ("relative", null, "17.0.1", "XA0031")]
	[TestCase ("default", null, "21.0.8", "")]
	[TestCase ("custom-name", null, "21.0.8", "")]
	[TestCase ("absolute", "21", "17.0.1", "")]
	[TestCase ("relative", "21", "17.0.1", "")]
	[TestCase ("default", null, "21", "")]
	[TestCase ("default", null, "17", "XA0031")]
	[TestCase ("default", null, "22", "XA0030")]
	[TestCase ("default", null, "21", "", false)]
	[TestCase ("default", null, "17", "XA0031", false)]
	[TestCase ("default", null, "22", "XA0030", false)]
	[TestCase ("default", null, "21.0.8", "", false)]
	public void ValidatesSelectedJavaToolRelease (string selection, string? toolVersion, string sdkVersion, string errorCode, bool useReleaseFile = true)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		string sdk = Path.Combine (directory, "sdk jdk");
		string toolJdk = Path.Combine (directory, "override jdk");
		Directory.CreateDirectory (Path.Combine (sdk, "bin"));
		Directory.CreateDirectory (Path.Combine (toolJdk, "bin"));
		File.WriteAllText (Path.Combine (sdk, "release"), $"JAVA_VERSION=\"{sdkVersion}\"\n");
		if (toolVersion != null) {
			File.WriteAllText (Path.Combine (toolJdk, "release"), $"JAVA_VERSION=\"{toolVersion}\"\n");
		}
		string executable = OperatingSystem.IsWindows () ? "java.exe" : "java";
		string tool = selection switch {
			"absolute" => Path.Combine (toolJdk, "bin", executable),
			"relative" => Path.Combine ("..", "..", "override jdk", "bin", executable),
			"custom-name" => "custom-" + executable,
			_ => "",
		};
		if (!useReleaseFile) {
			File.Delete (Path.Combine (sdk, "release"));
			tool = OperatingSystem.IsWindows () ? "java.cmd" : "java";
			string java = Path.Combine (sdk, "bin", tool);
			File.WriteAllText (java, OperatingSystem.IsWindows ()
				? $"@echo off\r\necho java version \"{sdkVersion}\" 1>&2\r\n"
				: $"#!/bin/sh\necho 'java version \"{sdkVersion}\"' >&2\n");
			if (!OperatingSystem.IsWindows ()) {
				File.SetUnixFileMode (java, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			}
		}
		string targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src",
			"Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.Tooling.targets");
		string project = Path.Combine (directory, "java.proj");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement ("_XamarinAndroidBuildTasksAssembly", typeof (AndroidError).Assembly.Location),
				new XElement ("_AndroidAllowMissingSdkTooling", "false"),
				new XElement ("_JavaSdkDirectory", Path.GetRelativePath (directory, sdk)),
				new XElement ("JavaToolExe", tool),
				new XElement ("MinimumSupportedJavaVersion", "21.0"),
				new XElement ("LatestSupportedJavaVersion", "21.99")),
			new XElement ("UsingTask", new XAttribute ("TaskName", "AndroidError"),
				new XAttribute ("AssemblyFile", typeof (AndroidError).Assembly.Location)),
			new XElement ("UsingTask", new XAttribute ("TaskName", "AndroidWarning"),
				new XAttribute ("AssemblyFile", typeof (AndroidWarning).Assembly.Location)),
			new XElement ("Import", new XAttribute ("Project", targets)),
			new XElement ("Target", new XAttribute ("Name", "CheckJdkVersion"),
				new XAttribute ("DependsOnTargets", "_ValidateJavaVersion"),
				new XElement ("Message", new XAttribute ("Importance", "high"),
					new XAttribute ("Text", "Parsed Java SDK version $([System.Version]::Parse('$(_JdkVersion)')).")))))
			.Save (project);

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project,
			"-t:CheckJdkVersion", "-v:normal", "-m", "-mt", "-nr:false",
			"-warnaserror:MSB4286,MSB4287,MSB4288");
		string output = result.Output + result.Error;
		Assert.AreEqual (errorCode == "" ? 0 : 1, result.ExitCode, output);
		string expectedVersion = toolVersion ?? sdkVersion;
		if (!expectedVersion.Contains ('.')) {
			expectedVersion += ".0";
		}
		StringAssert.Contains ($"Found Java SDK version {expectedVersion}.", output);
		if (errorCode == "") {
			StringAssert.Contains ($"Parsed Java SDK version {expectedVersion}.", output);
		}
		if (errorCode != "") {
			StringAssert.Contains ("error " + errorCode, output);
		}
		StringAssert.DoesNotContain ("XA0033", output);
	}
}
