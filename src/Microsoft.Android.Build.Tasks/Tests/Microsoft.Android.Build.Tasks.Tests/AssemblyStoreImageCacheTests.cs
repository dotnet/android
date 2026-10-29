using System;
using System.IO;
using System.Runtime.InteropServices;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture (false)]
[TestFixture (true)]
[Platform ("MacOsX,Linux")]
public class AssemblyStoreImageCacheTests (bool release) : BaseTest
{
	string nativeProbe = "";
	string probeDirectory = "";

	[OneTimeSetUp]
	public void CompileProductionLoader ()
	{
		string root = NativeAotBootstrapTestTools.FindRepositoryRoot ();
		probeDirectory = Path.Combine (Root, "temp", nameof (AssemblyStoreImageCacheTests), release ? "Release" : "Debug");
		Directory.CreateDirectory (probeDirectory);
		nativeProbe = Path.Combine (probeDirectory, "assembly-store-probe");
		string compressionLibrary = Path.Combine (RuntimeEnvironment.GetRuntimeDirectory (),
			OperatingSystem.IsMacOS () ? "libSystem.IO.Compression.Native.dylib" : "libSystem.IO.Compression.Native.so");
		FileAssert.Exists (compressionLibrary);
		var compile = NativeAotBootstrapTestTools.Run (Environment.GetEnvironmentVariable ("CXX") ?? "clang++",
			"-std=c++23", "-O2", "-pthread", "-fno-strict-aliasing",
			"-fsanitize=address,undefined", "-fno-sanitize-recover=all",
			release ? "-DRELEASE" : "-DDEBUG",
			"-I" + Path.Combine (root, "src", "native", "clr", "include"),
			"-I" + Path.Combine (root, "src", "native", "common", "include"),
			Path.Combine (root, "src", "native", "clr", "host", "assembly-store.cc"),
			Path.Combine (TestContext.CurrentContext.TestDirectory, "Resources", "AssemblyStore", "image-cache-probe.cc"),
			compressionLibrary, "-Wl,-rpath," + RuntimeEnvironment.GetRuntimeDirectory (), "-o", nativeProbe);
		Assert.AreEqual (0, compile.ExitCode, compile.Output + compile.Error);
	}

	[OneTimeTearDown]
	public void DeleteProductionLoaderProbe ()
	{
		if (probeDirectory.Length > 0 && Directory.Exists (probeDirectory)) {
			Directory.Delete (probeDirectory, recursive: true);
		}
	}

	[TestCase ("startup")]
	[TestCase ("concurrent")]
	public void ReusesWritableImagesAndRetainsThemAcrossReconfiguration (string mode)
	{
		var result = NativeAotBootstrapTestTools.Run (nativeProbe, mode);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		StringAssert.Contains ("Image cache checks passed", result.Output);
	}
}
