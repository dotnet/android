#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests;

static class AndroidSdkTestHelper
{
	public static string GetAndroidSdkDirectory ()
	{
		string? directory = Environment.GetEnvironmentVariable ("TEST_ANDROID_SDK_PATH");
		if (string.IsNullOrWhiteSpace (directory)) {
			directory = Environment.GetEnvironmentVariable ("ANDROID_SDK_ROOT");
		}
		if (string.IsNullOrWhiteSpace (directory)) {
			directory = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), "android-toolchain", "sdk");
		}
		if (!Directory.Exists (directory)) {
			// Test assemblies can be built on one host and executed on another.
			string? buildDirectory = typeof (AndroidSdkTestHelper).Assembly.GetCustomAttributes<AssemblyMetadataAttribute> ()
				.Single (attribute => attribute.Key == "AndroidSdkDirectory").Value;
			if (buildDirectory is { Length: > 0 }) {
				directory = buildDirectory;
			}
		}
		DirectoryAssert.Exists (directory, "The configured Android SDK must exist on the executing test host.");
		return directory;
	}

	public static string GetAapt2Directory ()
	{
		string exe = OperatingSystem.IsWindows () ? "aapt2.exe" : "aapt2";
		string? directory = Directory.EnumerateDirectories (Path.Combine (GetAndroidSdkDirectory (), "build-tools"))
			.OrderByDescending (dir => Path.GetFileName (dir), StringComparer.Ordinal)
			.FirstOrDefault (dir => File.Exists (Path.Combine (dir, exe)));
		Assert.IsNotNull (directory, $"The configured Android SDK must contain `build-tools/*/{exe}`.");
		return directory ?? "";
	}

	/// <summary>
	/// Returns android.jar for the newest installed platform that is not newer than the latest stable API level.
	/// </summary>
	public static string GetAndroidJarPath ()
	{
		Version? max = null;
		string? jar = null;
		foreach (var dir in Directory.EnumerateDirectories (Path.Combine (GetAndroidSdkDirectory (), "platforms"), "android-*")) {
			string name = Path.GetFileName (dir).Substring ("android-".Length);
			if (!MonoAndroidHelper.TryParseApiLevel (name, out var version))
				continue;
			string path = Path.Combine (dir, "android.jar");
			if (version > XABuildConfig.AndroidLatestStableApiLevel || (max != null && version < max) || !File.Exists (path))
				continue;
			max = version;
			jar = path;
		}
		Assert.IsNotNull (jar, "The configured Android SDK must contain a stable `platforms/android-*/android.jar`.");
		return jar ?? "";
	}
}
