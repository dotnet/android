using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Platform ("MacOsX,Linux")]
public class NativeRuntimeArithmeticTests : BaseTest
{
	string nativeProbe = "";
	string probeDirectory = "";

	[OneTimeSetUp]
	public void CompileProductionRuntime ()
	{
		string root = NativeAotBootstrapTestTools.FindRepositoryRoot ();
		string ndkBin = Path.GetDirectoryName (NativeToolTestHelper.GetToolPath ("clang"))
			?? throw new InvalidOperationException ("The NDK compiler directory is missing.");
		string ndkHeaders = Path.GetFullPath (Path.Combine (ndkBin, "..", "sysroot", "usr", "include"));
		DirectoryAssert.Exists (ndkHeaders);
		probeDirectory = Path.Combine (Root, "temp", nameof (NativeRuntimeArithmeticTests));
		Directory.CreateDirectory (probeDirectory);
		nativeProbe = Path.Combine (probeDirectory, "runtime-arithmetic-probe");
		var compile = NativeAotBootstrapTestTools.Run (Environment.GetEnvironmentVariable ("CXX") ?? "clang++",
			"-std=c++23", "-O2", "-fsanitize=undefined", "-fno-sanitize-recover=undefined",
			"-ffunction-sections", "-fdata-sections", "-D__BIONIC_AVAILABILITY_GUARD(x)=0",
			OperatingSystem.IsMacOS () ? "-Wl,-dead_strip" : "-Wl,--gc-sections",
			"-I" + Path.Combine (root, "src", "native", "clr", "include"),
			"-I" + Path.Combine (root, "src", "native", "common", "include"),
			"-idirafter", ndkHeaders,
			Path.Combine (root, "src", "native", "clr", "host", "coreclr-bootstrap.cc"),
			Path.Combine (root, "src", "native", "clr", "runtime-base", "android-system-shared.cc"),
			Path.Combine (TestContext.CurrentContext.TestDirectory, "Resources", "NativeAotBootstrap", "runtime-arithmetic-probe.cc"),
			"-o", nativeProbe);
		Assert.AreEqual (0, compile.ExitCode, compile.Output + compile.Error);
	}

	[OneTimeTearDown]
	public void DeleteProductionRuntimeProbe ()
	{
		if (probeDirectory.Length > 0 && Directory.Exists (probeDirectory)) {
			Directory.Delete (probeDirectory, recursive: true);
		}
	}

	[TestCase ("", false, 51200, 46080)]
	[TestCase ("", true, 2000, 1800)]
	[TestCase ("0", false, 0, 0)]
	[TestCase ("100k", false, 100000, 90000)]
	[TestCase ("2m", false, 2000000, 1800000)]
	[TestCase ("2147483k", false, 2147483000, 1932734700)]
	[TestCase ("2147483646", false, 2147483646, 1932735281)]
	[TestCase ("2147483647", false, int.MaxValue, int.MaxValue)]
	[TestCase ("2147483648", false, int.MaxValue, int.MaxValue)]
	[TestCase ("3000000k", false, int.MaxValue, int.MaxValue)]
	[TestCase ("2148m", false, int.MaxValue, int.MaxValue)]
	[TestCase ("999999999999999999999999m", false, int.MaxValue, int.MaxValue)]
	[TestCase ("-1", false, int.MaxValue, int.MaxValue)]
	[TestCase ("-2k", false, int.MaxValue, int.MaxValue)]
	[TestCase ("-999999999999999999999999", false, int.MaxValue, int.MaxValue)]
	public void GlobalReferenceLimitsStayWithinManagedRange (string value, bool emulator, int maximum, int threshold)
	{
		var result = NativeAotBootstrapTestTools.Run (nativeProbe, "gref", value, emulator ? "emulator" : "device");
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		Assert.AreEqual ($"{maximum} {threshold}", result.Output);
	}

	[Test]
	public void UnsupportedGlobalReferenceSuffixStillWarns ()
	{
		var result = NativeAotBootstrapTestTools.Run (nativeProbe, "gref", "100kextra", "device");
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		Assert.AreEqual ("100000 90000", result.Output);
		StringAssert.Contains ("Unsupported 'debug.dotnet.max_grefc' value '100kextra'", result.Error);
	}

	[TestCase (false, "2 0 1")]
	[TestCase (true, "2 2 2 0 1")]
	public void ReadsValidCoreClrBootstrap (bool preloads, string expected)
	{
		var result = RunBootstrap (CreateBootstrap (preloads));
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		Assert.AreEqual (expected, result.Output);
	}

	[TestCase (0u, 0u)]
	[TestCase (2u, 0u)]
	[TestCase (2u, 3u)]
	public void RejectsInvalidPreloadStride (uint count, uint stride)
	{
		byte [] body = CreateBootstrap (true);
		BitConverter.GetBytes (count).CopyTo (body, 32);
		BitConverter.GetBytes (stride).CopyTo (body, 36);
		var result = RunBootstrap (body);
		Assert.AreNotEqual (0, result.ExitCode);
		StringAssert.Contains ("invalid JNI preload stride", result.Error);
	}

	[Test]
	public void RejectsPreloadIndexOutsideDsoCache ()
	{
		byte [] body = CreateBootstrap (true);
		BitConverter.GetBytes (2u).CopyTo (body, 152);
		var result = RunBootstrap (body);
		Assert.AreNotEqual (0, result.ExitCode);
		StringAssert.Contains ("preload refers to a missing DSO", result.Error);
	}

	[TestCase (24, 1024u, "DSO table exceeds")]
	[TestCase (24, 0x40000000u, "DSO table exceeds")]
	[TestCase (52, uint.MaxValue, "DSO table exceeds")]
	[TestCase (32, 0xfffffffeu, "JNI preload table exceeds")]
	[TestCase (56, uint.MaxValue, "JNI preload table exceeds")]
	public void RejectsTablesThatExceedTheBootstrapBody (int offset, uint value, string error)
	{
		byte [] body = CreateBootstrap (true);
		BitConverter.GetBytes (value).CopyTo (body, offset);
		var result = RunBootstrap (body);
		Assert.AreNotEqual (0, result.ExitCode);
		StringAssert.Contains (error, result.Error);
	}

	static byte [] CreateBootstrap (bool preloads) => CoreClrBootstrapBlob.Create (
		false, false, 0, 0, 0, 1, "com.example.test",
		new Dictionary<string, string> (), new Dictionary<string, string> (), new Dictionary<string, string> (),
		[(1u, false, true, "libfirst.so", "first"), (2u, false, true, "libsecond.so", "second")],
		preloads ? [0u, 1u] : [], preloads ? 2u : 1u);

	(int ExitCode, string Output, string Error) RunBootstrap (byte [] body)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string path = Path.Combine (directory, "bootstrap.bin");
		File.WriteAllBytes (path, body);
		return NativeAotBootstrapTestTools.Run (nativeProbe, "bootstrap", path);
	}
}
