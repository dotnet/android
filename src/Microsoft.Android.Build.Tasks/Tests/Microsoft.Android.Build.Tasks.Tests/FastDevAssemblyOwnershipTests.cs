using System;
using System.IO;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Platform ("MacOsX,Linux")]
public class FastDevAssemblyOwnershipTests : BaseTest
{
	string nativeProbe = "";
	string probeDirectory = "";

	[OneTimeSetUp]
	public void CompileProductionProbe ()
	{
		string root = NativeAotBootstrapTestTools.FindRepositoryRoot ();
		string resources = Path.Combine (TestContext.CurrentContext.TestDirectory, "Resources", "FastDevOwnership");
		probeDirectory = Path.Combine (Root, "temp", nameof (FastDevAssemblyOwnershipTests));
		Directory.CreateDirectory (Path.Combine (probeDirectory, "runtime-base"));
		foreach (string header in new [] { "constants.hh", "runtime-base/util.hh", "runtime-base/android-system.hh" }) {
			File.WriteAllText (Path.Combine (probeDirectory, header), "#include <probe-platform.hh>\n");
		}
		nativeProbe = Path.Combine (probeDirectory, "fastdev-probe");
		var compile = NativeAotBootstrapTestTools.Run (Environment.GetEnvironmentVariable ("CXX") ?? "clang++",
			"-std=c++23", "-O2", "-pthread", "-DDEBUG", "-fsanitize=address,undefined", "-fno-sanitize-recover=all",
			"-I" + probeDirectory, "-I" + resources,
			"-I" + Path.Combine (root, "src", "native", "clr", "include"),
			Path.Combine (root, "src", "native", "clr", "host", "fastdev-assemblies.cc"),
			Path.Combine (resources, "fastdev-probe.cc"), "-o", nativeProbe);
		Assert.AreEqual (0, compile.ExitCode, compile.Output + compile.Error);
	}

	[OneTimeTearDown]
	public void DeleteProbe ()
	{
		if (probeDirectory.Length > 0 && Directory.Exists (probeDirectory)) {
			Directory.Delete (probeDirectory, recursive: true);
		}
	}

	[TestCase ("repeat")]
	[TestCase ("concurrent")]
	[TestCase ("snapshot")]
	[TestCase ("failure-retry")]
	[TestCase ("names")]
	[TestCase ("tpa")]
	public void RetainsSuccessfulSnapshots (string scenario)
	{
		var result = NativeAotBootstrapTestTools.Run (nativeProbe, scenario, probeDirectory);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
	}
}
