using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class DebugKeyTargetsTests : BaseTest
{
	[TestCase (0, "", true, false, false)]
	[TestCase (0, "", true, false, true)]
	[TestCase (1, "keytool error: alias <androiddebugkey> already exists", true, true, false)]
	[TestCase (1, "keytool error: alias <androiddebugkey> already exists", true, true, true)]
	[TestCase (2, "keytool error: invalid algorithm", false, false, false)]
	[TestCase (2, "keytool error: invalid algorithm", false, false, true)]
	[TestCase (0, "Warning: test warning", true, true, false)]
	[TestCase (0, "Warning: test warning", true, true, true)]
	public void ClassifiesKeytoolOutput (int exitCode, string output, bool success, bool warning, bool standardError)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string tool = Path.Combine (directory, OperatingSystem.IsWindows () ? "keytool.cmd" : "keytool");
		File.WriteAllText (tool, OperatingSystem.IsWindows ()
			? $"@echo off\r\necho({output.Replace ("<", "^<").Replace (">", "^>")}{(standardError ? " >&2" : "")}\r\nexit /b {exitCode}\r\n"
			: $"#!/bin/sh\nprintf '%s\\n' '{output}'{(standardError ? " >&2" : "")}\nexit {exitCode}\n");
		if (!OperatingSystem.IsWindows ()) {
			File.SetUnixFileMode (tool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}
		var result = BuildTarget (directory, tool, Path.Combine (directory, "keys", "debug.keystore"));
		Assert.AreEqual (success, result.ExitCode == 0, result.Output + result.Error);
		Assert.AreEqual (warning, result.Output.Contains ("warning ANDKT0000:"), result.Output);
		Assert.AreEqual (!success, result.Output.Contains ("error ANDKT0000:"), result.Output);
		if (output.Length > 0) {
			StringAssert.Contains (warning ? output.Replace ("error:", "warning:") : output, result.Output);
		}
		if (!success) {
			StringAssert.Contains ($"keytool exited with code {exitCode}:", result.Output);
		}
	}

	[Test]
	public void PreservesLiteralPaths ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		// Paired percent signs catch cmd expansion; the apostrophe catches incorrect Unix single quoting.
		string special = Path.Combine (directory, "keys $PATH %PATH% %25 apostrophe's");
		Directory.CreateDirectory (special);
		string tool = Path.Combine (special, OperatingSystem.IsWindows () ? "keytool.cmd" : "keytool");
		File.WriteAllText (tool, OperatingSystem.IsWindows ()
			? "@echo off\r\n:next\r\nif \"%~1\"==\"-keystore\" (type nul > \"%~2\" & exit /b 0)\r\nif \"%~1\"==\"\" exit /b 9\r\nshift\r\ngoto next\r\n"
			: "#!/bin/sh\nwhile [ \"$#\" -gt 0 ]; do\nif [ \"$1\" = '-keystore' ]; then : > \"$2\"; exit $?; fi\nshift\ndone\nexit 9\n");
		if (!OperatingSystem.IsWindows ()) {
			File.SetUnixFileMode (tool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}
		string keystore = Path.Combine (special, "nested", "debug.keystore");
		var result = BuildTarget (directory, tool, keystore);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		FileAssert.Exists (keystore, "Exec must receive the same literal path that MakeDir created.");
	}

	static (int ExitCode, string Output, string Error) BuildTarget (string directory, string tool, string keystore)
	{
		string targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets");
		var source = XDocument.Load (targets);
		var target = new XElement (source.Descendants ().Single (element =>
			element.Name.LocalName == "Target" && (string?) element.Attribute ("Name") == "_CreateAndroidDebugSigningKey"));
		foreach (var element in target.DescendantsAndSelf ()) {
			element.Name = element.Name.LocalName;
		}
		string project = Path.Combine (directory, "debug-key.proj");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement ("KeytoolToolPath", Path.GetDirectoryName (tool)?.Replace ("%", "%25")),
				new XElement ("KeytoolToolExe", Path.GetFileName (tool)),
				new XElement ("_ApkDebugKeyStore", keystore.Replace ("%", "%25")),
				new XElement ("AndroidDebugKeyAlgorithm", "RSA"),
				new XElement ("AndroidDebugKeyValidity", "10950"),
				new XElement ("AndroidDebugStoreType", "pkcs12")),
			target)).Save (project);
		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		return NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:_CreateAndroidDebugSigningKey", "-v:normal", "-nr:false");
	}
}
