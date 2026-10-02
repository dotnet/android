#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

static class NativeToolTestHelper
{
	public static string GetToolPath (string name)
	{
		string? runtimeDirectory = Environment.GetEnvironmentVariable ("TEST_ANDROID_NDK_PATH");
		if (string.IsNullOrWhiteSpace (runtimeDirectory)) {
			runtimeDirectory = Environment.GetEnvironmentVariable ("ANDROID_NDK_LATEST_HOME");
		}
		string? buildDirectory = typeof (NativeToolTestHelper).Assembly.GetCustomAttributes<AssemblyMetadataAttribute> ()
			.Single (attribute => attribute.Key == "AndroidNdkDirectory").Value;
		string directory = ResolveNdkDirectory (
			runtimeDirectory, Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), buildDirectory);
		DirectoryAssert.Exists (directory, "The configured NDK must exist on the executing test host.");

		string host = OperatingSystem.IsMacOS () ? "darwin-x86_64" :
			OperatingSystem.IsLinux () ? "linux-x86_64" :
			OperatingSystem.IsWindows () ? "windows-x86_64" :
			throw new PlatformNotSupportedException ("No Android NDK toolchain is configured for this host.");
		string executable = name + (OperatingSystem.IsWindows () ? ".exe" : "");
		string path = Path.Combine (directory, "toolchains", "llvm", "prebuilt", host, "bin", executable);
		FileAssert.Exists (path, "The configured NDK must contain the host's LLVM inspection tools.");
		return path;
	}

	internal static string ResolveNdkDirectory (string? runtimeDirectory, string homeDirectory, string? buildDirectory)
	{
		if (!string.IsNullOrWhiteSpace (runtimeDirectory)) {
			return runtimeDirectory;
		}
		string defaultDirectory = Path.Combine (homeDirectory, "android-toolchain", "ndk");
		if (Directory.Exists (defaultDirectory)) {
			return defaultDirectory;
		}
		// Test assemblies can be built on one host and executed on another.
		return buildDirectory != null && Directory.Exists (buildDirectory) ? buildDirectory : defaultDirectory;
	}

	public static string Run (string name, params string [] arguments)
	{
		var startInfo = new ProcessStartInfo (GetToolPath (name)) {
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		foreach (string argument in arguments) {
			startInfo.ArgumentList.Add (argument);
		}
		var result = Capture (startInfo, TimeSpan.FromSeconds (30));
		Assert.AreEqual (0, result.ExitCode, $"{name} failed for {string.Join (" ", arguments)}:\n{result.StandardError}\n{result.StandardOutput}");
		return result.StandardOutput;
	}

	internal static (int ExitCode, string StandardOutput, string StandardError) Capture (ProcessStartInfo startInfo, TimeSpan timeout)
	{
#if NET11_0_OR_GREATER
		ProcessTextOutput result;
		try {
			result = Process.RunAndCaptureText (startInfo, timeout);
		} catch (TimeoutException ex) {
			throw new TimeoutException ($"Native tool timed out: {startInfo.FileName}", ex);
		}
		if (result.ExitStatus.Canceled) {
			throw new TimeoutException ($"Native tool timed out: {startInfo.FileName}");
		}
		return (result.ExitStatus.ExitCode, result.StandardOutput, result.StandardError);
#else // !NET11_0_OR_GREATER
		// This helper is also compiled into the net10.0 packaging tests.
		using var cancellation = new CancellationTokenSource (timeout);
		using var process = new Process { StartInfo = startInfo };
		process.Start ();
		var output = process.StandardOutput.ReadToEndAsync (cancellation.Token);
		var error = process.StandardError.ReadToEndAsync (cancellation.Token);
		try {
			Task.WhenAll (output, error, process.WaitForExitAsync (cancellation.Token))
				.WaitAsync (cancellation.Token).GetAwaiter ().GetResult ();
		} catch (OperationCanceledException ex) {
			if (!process.HasExited) {
				process.Kill (entireProcessTree: true);
			}
			throw new TimeoutException ($"Native tool timed out: {startInfo.FileName}", ex);
		}
		return (process.ExitCode, output.GetAwaiter ().GetResult (), error.GetAwaiter ().GetResult ());
#endif // NET11_0_OR_GREATER
	}

	public static JsonDocument ReadElf (string library) => JsonDocument.Parse (Run (
		"llvm-readobj", "--elf-output-style=JSON", "--file-headers", "--program-headers", "--sections", "--dyn-symbols", library));

	public static byte [] ReadSection (string library, string section)
	{
		string directory = Path.GetDirectoryName (library) ?? throw new InvalidOperationException ("The test library directory is missing.");
		string sectionFile = Path.Combine (directory, Path.GetRandomFileName ());
		string copy = Path.Combine (directory, Path.GetRandomFileName ());
		try {
			// Always supply an output copy: objcopy must not rewrite the library under inspection.
			Run ("llvm-objcopy", $"--dump-section={section}={sectionFile}", library, copy);
			return File.ReadAllBytes (sectionFile);
		} finally {
			File.Delete (sectionFile);
			File.Delete (copy);
		}
	}
}
