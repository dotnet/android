using System;
using System.Diagnostics;
using System.IO;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

internal static class NativeAotBootstrapTestTools
{
	internal static string FindRepositoryRoot ()
	{
		var directory = new DirectoryInfo (TestContext.CurrentContext.TestDirectory);
		while (directory != null) {
			if (File.Exists (Path.Combine (directory.FullName, "src", "native", "native.targets"))) {
				return directory.FullName;
			}
			directory = directory.Parent;
		}
		throw new DirectoryNotFoundException ("Unable to find NativeAOT bootstrap sources");
	}

	internal static (int ExitCode, string Output, string Error) Run (string executable, params string [] arguments)
	{
		var startInfo = new ProcessStartInfo (executable) {
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		foreach (string argument in arguments) {
			startInfo.ArgumentList.Add (argument);
		}
		return NativeToolTestHelper.Capture (startInfo, TimeSpan.FromMinutes (2));
	}

	internal static string JavaTool (string name)
	{
		string? javaHome = Environment.GetEnvironmentVariable ("JAVA_HOME")
			?? Environment.GetEnvironmentVariable ("TEST_ANDROID_JDK_PATH");
		if (string.IsNullOrEmpty (javaHome)) {
			Assert.Ignore ("Host JNI bootstrap tests require JAVA_HOME or TEST_ANDROID_JDK_PATH");
		}
		return Path.Combine (javaHome ?? throw new InvalidOperationException ("Java home is required"),
			"bin", name + (OperatingSystem.IsWindows () ? ".exe" : ""));
	}
}
