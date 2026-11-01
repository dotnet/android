using System;
using System.IO;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Platform ("MacOsX,Linux")]
public class CpuArchitectureTests : BaseTest
{
	[TestCase ("absent", 0)]
	[TestCase ("houdini", 0)]
	[TestCase ("open-error", 1)]
	public void DetectsCpuWithoutLeakingMapsOrMistakingOpenFailureForHoudini (string scenario, int exitCode)
	{
		string root = NativeAotBootstrapTestTools.FindRepositoryRoot ();
		string resources = Path.Combine (TestContext.CurrentContext.TestDirectory, "Resources", "CpuArchitecture");
		string probeDirectory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (probeDirectory);
		string nativeProbe = Path.Combine (probeDirectory, "cpu-architecture-probe");
		var compile = NativeAotBootstrapTestTools.Run (Environment.GetEnvironmentVariable ("CXX") ?? "clang++",
			"-std=c++23", "-O2", "-Wall", "-Wextra", "-Werror",
			"-fno-exceptions", "-fno-rtti", "-nostdlib++",
			"-I" + resources,
			"-I" + Path.Combine (root, "src", "native", "common", "include"),
			"-I" + Path.Combine (root, "src", "native", "clr", "include"),
			"-I" + Path.Combine (root, "src", "native", "clr"),
			Path.Combine (resources, "cpu-architecture-probe.cc"),
			"-o", nativeProbe);
		Assert.AreEqual (0, compile.ExitCode, compile.Output + compile.Error);
		var result = NativeAotBootstrapTestTools.Run (nativeProbe, scenario);
		Assert.AreEqual (exitCode, result.ExitCode, result.Output + result.Error);
		if (exitCode == 1) {
			StringAssert.Contains ("Failed to open /proc/self/maps for CPU detection", result.Error);
			StringAssert.Contains ("Unable to determine the running CPU architecture", result.Error);
		} else {
			Assert.AreEqual ("", result.Error);
		}
	}
}
