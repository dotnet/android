using System;
using System.IO;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Platform ("MacOsX,Linux")]
public class CpuArchitectureTests : BaseTest
{
	string probeDirectory = "";
	string nativeProbe = "";

	[OneTimeSetUp]
	public void CompileProductionCpuDetector ()
	{
		string root = NativeAotBootstrapTestTools.FindRepositoryRoot ();
		string resources = Path.Combine (TestContext.CurrentContext.TestDirectory, "Resources", "CpuArchitecture");
		probeDirectory = Path.Combine (Root, "temp", nameof (CpuArchitectureTests));
		Directory.CreateDirectory (probeDirectory);
		nativeProbe = Path.Combine (probeDirectory, "cpu-architecture-probe");
		var compile = NativeAotBootstrapTestTools.Run (Environment.GetEnvironmentVariable ("CXX") ?? "clang++",
			"-std=c++23", "-O2", "-Wall", "-Wextra", "-Werror",
			"-fno-exceptions", "-fno-rtti", "-nostdlib++",
			"-fsanitize=undefined", "-fno-sanitize-recover=undefined",
			"-I" + resources,
			"-I" + Path.Combine (root, "src", "native", "common", "include"),
			"-I" + Path.Combine (root, "src", "native", "clr", "include"),
			"-I" + Path.Combine (root, "src", "native", "clr"),
			Path.Combine (resources, "cpu-architecture-probe.cc"),
			"-o", nativeProbe);
		Assert.AreEqual (0, compile.ExitCode, compile.Output + compile.Error);
	}

	[OneTimeTearDown]
	public void DeleteCpuDetectorProbe ()
	{
		if (probeDirectory.Length > 0 && Directory.Exists (probeDirectory)) {
			Directory.Delete (probeDirectory, recursive: true);
		}
	}

	[TestCase ("absent", 1)]
	[TestCase ("houdini", 4)]
	public void DetectsArchitectureAndClosesMaps (string scenario, int cpu)
	{
		var result = NativeAotBootstrapTestTools.Run (nativeProbe, scenario);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		StringAssert.Contains ($"running={cpu} opens=1 closes=1 warnings=0", result.Output);
		Assert.AreEqual ("", result.Error);
	}

	[Test]
	public void OpenFailureAbortsBeforeSelectingAnApkAbi ()
	{
		var result = NativeAotBootstrapTestTools.Run (nativeProbe, "open-error");
		Assert.AreEqual (1, result.ExitCode, result.Output + result.Error);
		StringAssert.Contains ("opens=1 closes=0 warnings=1", result.Output);
		StringAssert.Contains ("Failed to open /proc/self/maps for CPU detection", result.Error);
		StringAssert.Contains ("Unable to determine the running CPU architecture", result.Error);
	}
}
