using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class InstallApkSetTargetsTests : BaseTest
{
	const string Exception = "com.android.tools.build.bundletool.model.exceptions.CommandExecutionException: Device installation failed.";
	const string Control = "An unrelated bundletool output line.";

	[TestCase ("21", "", "", "", false)]
	[TestCase ("25", "-Dexample=value", "1G", "-s emulator-5554", true)]
	[TestCase ("25", "--enable-native-access=ALL-UNNAMED --sun-misc-unsafe-memory-access=allow", "1G", "emulator-5554", true)]
	public void PreservesLiteralArguments (string jdkVersion, string javaOptions, string heap, string adbTarget, bool modules)
	{
		string directory = CreateDirectory ();
		string tool = CreateTool (directory, 0, Control, false);
		var result = BuildTarget (directory, jdkVersion, javaOptions, heap, adbTarget, modules);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		var expected = new List<string> ();
		if (javaOptions.StartsWith ("-D", StringComparison.Ordinal)) {
			expected.Add (javaOptions);
		}
		if (jdkVersion == "25") {
			expected.Add ("--enable-native-access=ALL-UNNAMED");
			expected.Add ("--sun-misc-unsafe-memory-access=allow");
		}
		if (heap.Length > 0) {
			expected.Add ("-Xmx" + heap);
		}
		expected.AddRange ([
			"-jar", Path.Combine (directory, "bundletool.jar"),
			"install-apks", "--apks", Path.Combine (directory, "application.apks"),
			"--adb", Path.Combine (directory, "adb"),
		]);
		if (adbTarget.Length > 0) {
			expected.AddRange (["--device-id", "emulator-5554"]);
		}
		expected.AddRange (["--allow-downgrade", "--modules", modules ? "base,feature $PATH %PATH% %25 apostrophe's `literal`" : "_ALL_"]);
		CollectionAssert.AreEqual (expected, File.ReadAllLines (tool + ".argv"), result.Output);
		StringAssert.DoesNotContain ("BT0000", result.Output);
	}

	[TestCase (true, false)]
	[TestCase (true, true)]
	[TestCase (false, false)]
	public void ReportsFailureDiagnostics (bool matching, bool standardError)
	{
		string directory = CreateDirectory ();
		CreateTool (directory, 7, Control + (matching ? "\n" + Exception : ""), standardError);
		var result = BuildTarget (directory, "21", "", "", "", false);
		Assert.AreNotEqual (0, result.ExitCode, result.Output + result.Error);
		string [] diagnostics = result.Output.Split ('\n').Where (line => line.Contains ("error BT0000:")).ToArray ();
		Assert.IsNotEmpty (diagnostics, result.Output + result.Error);
		foreach (string diagnostic in diagnostics) {
			if (matching) {
				StringAssert.Contains (Path.Combine (directory, "application.apks"), diagnostic);
				StringAssert.Contains (Exception, diagnostic);
				StringAssert.DoesNotContain (Control, diagnostic);
			} else {
				StringAssert.Contains ("bundletool install-apks failed with exit code 7.", diagnostic);
				StringAssert.DoesNotContain (Exception, diagnostic);
			}
		}
	}

	string CreateDirectory ()
	{
		if (OperatingSystem.IsWindows ()) {
			Assert.Ignore ("The fake java executable is a POSIX shell script.");
		}
		string directory = Path.Combine (Root, "temp", TestName, "paths $PATH %PATH% %25 apostrophe's `literal`");
		Directory.CreateDirectory (directory);
		return directory;
	}

	static string CreateTool (string directory, int exitCode, string output, bool standardError)
	{
		string tool = Path.Combine (directory, "java");
		File.WriteAllText (tool + ".output", output);
		File.WriteAllText (tool, $"""
			#!/bin/sh
			printf '%s\n' "$@" > "$0.argv"
			cat "$0.output"{(standardError ? " >&2" : "")}
			exit {exitCode}

			""");
		if (!OperatingSystem.IsWindows ()) {
			File.SetUnixFileMode (tool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}
		return tool;
	}

	static (int ExitCode, string Output, string Error) BuildTarget (string directory, string jdkVersion, string javaOptions, string heap, string adbTarget, bool modules)
	{
		string targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets");
		var source = XDocument.Load (targets);
		var target = new XElement (source.Descendants ().Single (element =>
			element.Name.LocalName == "Target" && (string?) element.Attribute ("Name") == "_DeployAppBundle"));
		foreach (var element in target.DescendantsAndSelf ()) {
			element.Name = element.Name.LocalName;
		}
		static XElement Property (string name, string value) => new (name, value.Replace ("%", "%25"));
		string project = Path.Combine (Path.GetDirectoryName (directory) ?? throw new InvalidOperationException ("Missing test directory"), "install.proj");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				Property ("AndroidPackageFormat", "aab"),
				Property ("JavaToolPath", directory),
				Property ("_JdkVersion", jdkVersion),
				Property ("JavaOptions", javaOptions),
				Property ("JavaMaximumHeapSize", heap),
				Property ("AndroidBundleToolJarPath", Path.Combine (directory, "bundletool.jar")),
				Property ("_ApkSetIntermediate", Path.Combine (directory, "application.apks")),
				Property ("AdbToolPath", directory),
				Property ("AdbToolExe", "adb"),
				Property ("AdbTarget", adbTarget)),
			new XElement ("ItemGroup",
				modules ? new [] {
					new XElement ("AndroidInstallModules", new XAttribute ("Include", "base")),
					new XElement ("AndroidInstallModules", new XAttribute ("Include", "feature $PATH %25PATH%25 %2525 apostrophe's `literal`")),
				} : []),
			new XElement ("Target", new XAttribute ("Name", "_BuildApkSet")),
			new XElement ("Target", new XAttribute ("Name", "_AndroidAdbToolPath")),
			target)).Save (project);
		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		return NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:_DeployAppBundle", "-v:normal", "-nr:false");
	}
}
