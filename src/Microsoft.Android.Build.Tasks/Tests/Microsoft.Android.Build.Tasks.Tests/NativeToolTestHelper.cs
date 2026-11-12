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
		using var cancellation = new CancellationTokenSource (timeout);
		using var process = Process.Start (startInfo) ?? throw new InvalidOperationException ($"Could not start {startInfo.FileName}.");
		// Select native capture mode before borrowing readers for deterministic disposal.
		var capture = process.ReadAllTextAsync (cancellation.Token);
		using var stdout = process.StandardOutput;
		using var stderr = process.StandardError;
		var exited = process.WaitForExitAsync (cancellation.Token);
		try {
			Task.WhenAll (capture, exited).GetAwaiter ().GetResult ();
			var result = capture.GetAwaiter ().GetResult ();
			return (process.ExitCode, result.StandardOutput, result.StandardError);
		} catch (OperationCanceledException ex) when (cancellation.IsCancellationRequested) {
			throw new TimeoutException ($"Native tool timed out: {startInfo.FileName}", ex);
		} finally {
			try {
				if (!process.HasExited) {
					try {
						process.Kill (entireProcessTree: true);
					} catch (InvalidOperationException) when (process.HasExited) {
						// The command exited before the kill request.
					}
					if (!process.WaitForExit (5000))
						throw new TimeoutException ($"Native tool did not exit after termination: {startInfo.FileName}");
				}
			} finally {
				cancellation.Cancel ();
				try {
					Task.WhenAll (capture, exited).GetAwaiter ().GetResult ();
				} catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
					TestContext.Progress.WriteLine ($"Stopped native-tool capture: {startInfo.FileName}");
				}
			}
		}
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
