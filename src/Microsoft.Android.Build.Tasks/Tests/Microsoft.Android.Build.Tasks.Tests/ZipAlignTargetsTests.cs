using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class ZipAlignTargetsTests : BaseTest
{
	[TestCase (true, false)]
	[TestCase (true, true)]
	[TestCase (false, false)]
	[TestCase (false, true)]
	public void AlignsEachApkOnceAndPreservesLiteralPaths (bool apkSigner, bool perAbi)
	{
		string directory = CreateDirectory ();
		string tool = CreateTool (directory);
		var result = BuildTarget (directory, tool, apkSigner, perAbi);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		string [] inputs = File.ReadAllLines (tool + ".calls");
		Assert.AreEqual (perAbi ? 3 : 1, inputs.Length, result.Output);
		Assert.AreEqual (inputs.Length, inputs.Distinct ().Count (), "The explicit base APK also matches the ABI wildcard.");
		foreach (string input in inputs) {
			string name = Path.GetFileNameWithoutExtension (input).Replace ("-Signed-Unaligned", "");
			string output = Path.Combine (directory, "output", name + "-Signed.apk");
			Assert.AreEqual (input, File.ReadAllText (output), "Both input and destination must reach the tool literally.");
		}

		result = BuildTarget (directory, tool, apkSigner, perAbi);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		Assert.AreEqual (inputs.Length, File.ReadAllLines (tool + ".calls").Length, "An unchanged build must skip zipalign.");
	}

	[TestCase (true, "arm64", false)]
	[TestCase (true, "arm64", true)]
	[TestCase (true, "x86", true)]
	[TestCase (false, "arm64", false)]
	[TestCase (false, "arm64", true)]
	[TestCase (false, "x86", true)]
	public void ReportsFailingBatchWithoutHidingOtherDiagnostics (bool apkSigner, string failingAbi, bool silent)
	{
		string directory = CreateDirectory ();
		string tool = CreateTool (directory, failingAbi, silent);
		var result = BuildTarget (directory, tool, apkSigner, perAbi: true);
		Assert.AreNotEqual (0, result.ExitCode, result.Output + result.Error);
		string [] calls = File.ReadAllLines (tool + ".calls");
		Assert.AreEqual (3, calls.Length, result.Output);
		string failedInput = calls.Single (input => Path.GetFileName (input).Contains (failingAbi));
		string [] errors = result.Output.Split ('\n').Where (line => line.Contains ("error ANDZA0000:")).ToArray ();
		Assert.IsNotEmpty (errors, result.Output);
		if (silent) {
			Assert.That (errors, Is.All.Contains ($"zipalign exited with code 7 for {failedInput}"));
		} else {
			Assert.That (errors.Any (line => line.Contains ("first diagnostic")), Is.True, result.Output);
			Assert.That (errors.Any (line => line.Contains ("second diagnostic")), Is.True, result.Output);
		}
		Assert.That (errors.Any (line => line.Contains ("successful alignment")), Is.False, "Successful batches must not supply a failing batch's output.");
		FileAssert.Exists (Path.Combine (directory, "output", "app-Signed.apk"));
		if (failingAbi == "arm64") {
			FileAssert.Exists (Path.Combine (directory, "output", "app-x86-Signed.apk"), "A later successful ABI must not hide an earlier failure.");
		}
	}

	string CreateDirectory ()
	{
		string directory = Path.Combine (Root, "temp", TestName, "paths $PATH %PATH% %25 apostrophe's");
		if (!OperatingSystem.IsWindows ()) {
			directory = Path.Combine (directory, "backtick`literal`");
		}
		Directory.CreateDirectory (directory);
		return directory;
	}

	static string CreateTool (string directory, string failingAbi = "", bool silent = false)
	{
		string tool = Path.Combine (directory, OperatingSystem.IsWindows () ? "zipalign.cmd" : "zipalign");
		string diagnostics = silent ? "" : OperatingSystem.IsWindows ()
			? "echo first diagnostic\r\necho second diagnostic >&2\r\n"
			: "printf '%s\\n' 'first diagnostic'\nprintf '%s\\n' 'second diagnostic' >&2\n";
		File.WriteAllText (tool, OperatingSystem.IsWindows ()
			? "@echo off\r\n"
				+ "if not \"%~1\"==\"-p\" exit /b 8\r\nif not \"%~2\"==\"16\" exit /b 8\r\n"
				+ "if not \"%~5\"==\"\" exit /b 8\r\nif not exist \"%~3\" exit /b 9\r\n"
				+ "echo %~3>>\"%~f0.calls\"\r\n"
				+ (failingAbi.Length == 0 ? "" : $"echo %~n3|findstr {failingAbi} >nul\r\nif not errorlevel 1 (\r\n{diagnostics}exit /b 7\r\n)\r\n")
				+ "echo successful alignment\r\n<nul set /p \"=%~3\">\"%~4\"\r\nexit /b 0\r\n"
			: "#!/bin/sh\n"
				+ "[ \"$#\" -eq 4 ] && [ \"$1\" = '-p' ] && [ \"$2\" = '16' ] || exit 8\n"
				+ "[ -f \"$3\" ] || exit 9\nprintf '%s\\n' \"$3\" >> \"$0.calls\"\n"
				+ (failingAbi.Length == 0 ? "" : $"case \"${{3##*/}}\" in *{failingAbi}*)\n{diagnostics}exit 7;;\nesac\n")
				+ "echo 'successful alignment'\nprintf '%s' \"$3\" > \"$4\"\n");
		if (!OperatingSystem.IsWindows ()) {
			File.SetUnixFileMode (tool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}
		return tool;
	}

	static (int ExitCode, string Output, string Error) BuildTarget (string directory, string tool, bool apkSigner, bool perAbi)
	{
		string intermediate = Path.Combine (directory, "intermediate");
		string output = Path.Combine (directory, "output");
		string inputDirectory = apkSigner ? Path.Combine (intermediate, "android", "bin") : output;
		Directory.CreateDirectory (inputDirectory);
		Directory.CreateDirectory (output);
		string suffix = apkSigner ? "" : "-Signed-Unaligned";
		foreach (string name in perAbi ? new [] { "app", "app-arm64", "app-x86" } : new [] { "app" }) {
			string input = Path.Combine (inputDirectory, name + suffix + ".apk");
			if (!File.Exists (input)) {
				File.WriteAllText (input, name);
			}
		}
		string targetsPath = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
			"src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets");
		var source = XDocument.Load (targetsPath);
		string [] names = ["_PrepareForSign", "_AndroidZipAlignInputs", "_AndroidZipAlign"];
		var targets = source.Descendants ().Where (element =>
			element.Name.LocalName == "Target" && names.Contains ((string?) element.Attribute ("Name")))
			.Select (element => new XElement (element)).ToArray ();
		foreach (var target in targets) {
			foreach (var element in target.DescendantsAndSelf ()) {
				element.Name = element.Name.LocalName;
			}
		}
		static XElement Property (string name, string value) => new (name, value.Replace ("%", "%25"));
		string project = Path.Combine (directory, "zipalign.proj");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				Property ("ZipAlignToolPath", directory),
				Property ("ZipalignToolExe", Path.GetFileName (tool)),
				Property ("IntermediateOutputPath", intermediate + Path.DirectorySeparatorChar),
				Property ("OutDir", output + Path.DirectorySeparatorChar),
				Property ("ApkFileIntermediate", Path.Combine (inputDirectory, "app.apk")),
				Property ("_AndroidPackage", "app"),
				Property ("AndroidUseApkSigner", apkSigner.ToString ()),
				Property ("AndroidPackageFormat", "apk"),
				Property ("AndroidCreatePackagePerAbi", perAbi.ToString ()),
				Property ("AndroidZipAlignment", "16")),
			targets,
			new XElement ("Target", new XAttribute ("Name", "Sign"),
				new XAttribute ("DependsOnTargets", "_PrepareForSign;_AndroidZipAlign")))).Save (project);
		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		return NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:Sign", "-v:normal", "-nr:false",
			"-m", "-mt", "-warnaserror:MSB4286,MSB4287,MSB4288");
	}
}
