#nullable enable
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Build.Tests;

[TestClass]
[DoNotParallelize]
public class NativeToolTestHelperTests : BaseTest
{
	[TestMethod]
	public void RuntimeNdkOverridesBuildHostPath ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);

		Assert.AreEqual (directory, NativeToolTestHelper.ResolveNdkDirectory (
			directory, Path.Combine (directory, "home"), Path.Combine (directory, "missing-build-host-ndk")));
	}

	[TestMethod]
	public void ExecutingHostNdkOverridesExistingBuildHostPath ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		string homeDirectory = Path.Combine (directory, "home");
		string runtimeNdk = Path.Combine (homeDirectory, "android-toolchain", "ndk");
		string buildNdk = Path.Combine (directory, "build-host-ndk");
		Directory.CreateDirectory (runtimeNdk);
		Directory.CreateDirectory (buildNdk);

		Assert.AreEqual (runtimeNdk, NativeToolTestHelper.ResolveNdkDirectory (null, homeDirectory, buildNdk));
	}

	[TestMethod]
	public void StaleBuildHostPathUsesExecutingHostConvention ()
	{
		string homeDirectory = Path.Combine (Root, "temp", TestName, "home");
		string missingBuildNdk = Path.Combine (Root, "temp", TestName, "missing-build-host-ndk");

		Assert.AreEqual (Path.Combine (homeDirectory, "android-toolchain", "ndk"),
			NativeToolTestHelper.ResolveNdkDirectory (null, homeDirectory, missingBuildNdk));
	}

	[TestMethod]
	public void ExplicitRuntimeConfigurationIsNotSilentlyIgnored ()
	{
		string missingDirectory = Path.Combine (Root, "temp", TestName, "missing-runtime-ndk");

		Assert.AreEqual (missingDirectory, NativeToolTestHelper.ResolveNdkDirectory (
			missingDirectory, Path.Combine (Root, "temp", TestName, "home"), null));
	}

	[TestMethod]
	public void ExistingBuildHostNdkRemainsUsableLocally ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);

		Assert.AreEqual (directory, NativeToolTestHelper.ResolveNdkDirectory ("", Path.Combine (directory, "home"), directory));
	}

	[TestMethod]
	public void CapturesBothStreamsBeyondPipeCapacity ()
	{
		const int lines = 1024;
		string outputLine = new string ('o', 1024);
		string errorLine = new string ('e', 1024);
		var startInfo = CreateShell (
			$"i=0; while [ \"$i\" -lt {lines} ]; do printf '{outputLine}\\n'; printf '{errorLine}\\n' >&2; i=$((i + 1)); done",
			$"for ($i=0; $i -lt {lines}; $i++) {{ [Console]::Out.WriteLine('{outputLine}'); [Console]::Error.WriteLine('{errorLine}'); }}");

		var result = NativeToolTestHelper.Capture (startInfo, TimeSpan.FromSeconds (10));

		Assert.AreEqual (0, result.ExitCode);
		Assert.AreEqual (string.Concat (Enumerable.Repeat (outputLine + "\n", lines)), result.StandardOutput.Replace ("\r\n", "\n"));
		Assert.AreEqual (string.Concat (Enumerable.Repeat (errorLine + "\n", lines)), result.StandardError.Replace ("\r\n", "\n"));
	}

	[TestMethod]
	public void CapturesDiagnosticsForNonzeroExit ()
	{
		var startInfo = CreateShell (
			"printf output; printf error >&2; exit 7",
			"[Console]::Out.Write('output'); [Console]::Error.Write('error'); exit 7");

		var result = NativeToolTestHelper.Capture (startInfo, TimeSpan.FromSeconds (10));

		Assert.AreEqual (7, result.ExitCode);
		Assert.AreEqual ("output", result.StandardOutput);
		Assert.AreEqual ("error", result.StandardError);
	}

	[TestMethod]
	public void TimesOutWhileWaitingForOutput ()
	{
		var startInfo = CreateShell ("exec sleep 30", "[Threading.Thread]::Sleep(30000)");
		var elapsed = Stopwatch.StartNew ();

		Assert.ThrowsExactly<TimeoutException> (() => NativeToolTestHelper.Capture (startInfo, TimeSpan.FromSeconds (1)));

		Assert.IsLessThan (TimeSpan.FromSeconds (10), elapsed.Elapsed, "The timeout must include output draining, not just process exit.");
	}

	[TestMethod]
	[OSCondition (ConditionMode.Exclude, OperatingSystems.Windows)]
	public void TimesOutAfterBothStreamsClose ()
	{
		var startInfo = CreateShell ("exec 1>&- 2>&-; exec sleep 30", "");
		var elapsed = Stopwatch.StartNew ();

		Assert.ThrowsExactly<TimeoutException> (() => NativeToolTestHelper.Capture (startInfo, TimeSpan.FromSeconds (1)));

		Assert.IsLessThan (TimeSpan.FromSeconds (10), elapsed.Elapsed, "Closing the streams must not start an unbounded process wait.");
	}

	[TestMethod]
	[OSCondition (ConditionMode.Exclude, OperatingSystems.Windows)]
	public void TimesOutWhenAnExitedParentLeavesInheritedPipesOpen ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string childIdFile = Path.Combine (directory, "child.pid");
		var startInfo = CreateShell ("sleep 30 & printf '%s' \"$!\" > \"$CHILD_PID_FILE\"; exit 0", "");
		startInfo.Environment ["CHILD_PID_FILE"] = childIdFile;
		var elapsed = Stopwatch.StartNew ();
		try {
			Assert.ThrowsExactly<TimeoutException> (() => NativeToolTestHelper.Capture (startInfo, TimeSpan.FromSeconds (1)));
			Assert.IsLessThan (TimeSpan.FromSeconds (10), elapsed.Elapsed, "Parent exit must not make inherited-pipe draining unbounded.");
		} finally {
			if (File.Exists (childIdFile)) {
				using var child = Process.GetProcessById (int.Parse (File.ReadAllText (childIdFile), CultureInfo.InvariantCulture));
				if (!child.HasExited) {
					child.Kill ();
				}
				Assert.IsTrue (child.WaitForExit (5000), "The test-owned pipe holder must be cleaned up.");
			}
		}
	}

	static ProcessStartInfo CreateShell (string unixCommand, string windowsCommand)
	{
		var startInfo = new ProcessStartInfo (OperatingSystem.IsWindows () ? "powershell.exe" : "/bin/sh") {
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		if (OperatingSystem.IsWindows ()) {
			startInfo.ArgumentList.Add ("-NoLogo");
			startInfo.ArgumentList.Add ("-NoProfile");
			startInfo.ArgumentList.Add ("-NonInteractive");
			startInfo.ArgumentList.Add ("-Command");
			startInfo.ArgumentList.Add (windowsCommand);
		} else {
			startInfo.ArgumentList.Add ("-c");
			startInfo.ArgumentList.Add (unixCommand);
		}
		return startInfo;
	}
}
