#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

static class NativeToolTestHelper
{
	public static string GetToolPath (string name)
	{
		string directory = typeof (NativeToolTestHelper).Assembly.GetCustomAttributes<AssemblyMetadataAttribute> ()
			.Single (attribute => attribute.Key == "AndroidNdkDirectory").Value ?? "";
		if (!Directory.Exists (directory)) {
			Assert.Ignore ("An Android NDK is required for native tool validation. Set AndroidNdkDirectory when building this test project.");
		}

		string host = OperatingSystem.IsMacOS () ? "darwin-x86_64" :
			OperatingSystem.IsLinux () ? "linux-x86_64" :
			OperatingSystem.IsWindows () ? "windows-x86_64" :
			throw new PlatformNotSupportedException ("No Android NDK toolchain is configured for this host.");
		string executable = name + (OperatingSystem.IsWindows () ? ".exe" : "");
		string path = Path.Combine (directory, "toolchains", "llvm", "prebuilt", host, "bin", executable);
		FileAssert.Exists (path, "The configured NDK must contain the host's LLVM inspection tools.");
		return path;
	}
}
