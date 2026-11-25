using System;
using System.IO;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Platform ("MacOsX,Linux")]
public class CoreClrAssemblyStoreTests : BaseTest
{
	[Test]
	public void ConcurrentProbesDuringStartupDecompressOnce ()
	{
		string root = NativeAotBootstrapTestTools.FindRepositoryRoot ();
		string sources = Path.Combine (root, "src", "Microsoft.Android.Build.Tasks", "Tests", "Microsoft.Android.Build.Tasks.Tests", "Resources", "CoreClrAssemblyStore");
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string probe = Path.Combine (directory, "assembly-store-probe");

		var compile = NativeAotBootstrapTestTools.Run (Environment.GetEnvironmentVariable ("CXX") ?? "clang++",
			"-std=c++23", "-O2", "-DRELEASE", "-pthread",
			"-I" + sources,
			"-I" + Path.Combine (root, "src", "native", "clr", "include"),
			"-I" + Path.Combine (root, "src", "native", "common", "include"),
			Path.Combine (root, "src", "native", "clr", "host", "assembly-store.cc"),
			Path.Combine (sources, "assembly-store-probe.cc"),
			"-o", probe);
		Assert.AreEqual (0, compile.ExitCode, compile.Output + compile.Error);

		var result = NativeAotBootstrapTestTools.Run (probe);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
	}
}
