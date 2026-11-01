using System;
using System.IO;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Platform ("MacOsX,Linux")]
public class FastDevAssemblyReadTests : BaseTest
{
	string nativeProbe = "";
	string probeDirectory = "";

	[OneTimeSetUp]
	public void CompileProductionReader ()
	{
		string root = NativeAotBootstrapTestTools.FindRepositoryRoot ();
		string resources = Path.Combine (TestContext.CurrentContext.TestDirectory, "Resources", "FastDev");
		probeDirectory = Path.Combine (Root, "temp", nameof (FastDevAssemblyReadTests));
		Directory.CreateDirectory (Path.Combine (probeDirectory, "runtime-base"));
		foreach (string header in new [] { "constants.hh", "runtime-base/android-system.hh", "runtime-base/util.hh" }) {
			File.WriteAllText (Path.Combine (probeDirectory, header), "#include <fastdev-test.hh>\n");
		}
		nativeProbe = Path.Combine (probeDirectory, "fastdev-probe");
		var compile = NativeAotBootstrapTestTools.Run (Environment.GetEnvironmentVariable ("CXX") ?? "clang++",
			"-std=c++23", "-O2", "-Wall", "-Wextra", "-Werror",
			"-fsanitize=address,undefined", "-fno-sanitize-recover=all", "-DDEBUG",
			"-I" + probeDirectory, "-I" + resources,
			"-I" + Path.Combine (root, "src", "native", "clr", "include"),
			"-I" + Path.Combine (root, "src", "native", "clr", "host"),
			Path.Combine (resources, "fastdev-probe.cc"), "-o", nativeProbe);
		Assert.AreEqual (0, compile.ExitCode, compile.Output + compile.Error);
	}

	[OneTimeTearDown]
	public void DeleteProductionReaderProbe ()
	{
		if (probeDirectory.Length > 0 && Directory.Exists (probeDirectory)) {
			Directory.Delete (probeDirectory, recursive: true);
		}
	}

	[TestCase ("complete")]
	[TestCase ("short")]
	[TestCase ("interrupted")]
	[TestCase ("eof")]
	[TestCase ("partial-eof")]
	[TestCase ("error")]
	[TestCase ("partial-error")]
	[TestCase ("empty")]
	[TestCase ("oversize")]
	[TestCase ("open-error")]
	[TestCase ("allocation-error")]
	[TestCase ("close-error")]
	public void ReadsUntilCompleteOrReportsActualFailure (string scenario)
	{
		var result = NativeAotBootstrapTestTools.Run (nativeProbe, scenario, probeDirectory);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
	}
}
