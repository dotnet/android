using System;
using System.IO;

using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Category ("UsesDevice")]
public class FastTimingTests : DeviceTest
{
	[TestCase (false)]
	[TestCase (true)]
	public void ConcurrentEventsCanGrowAndDump (bool useBundledLongFileName)
	{
		const string completedMessage = "FAST_TIMING_EVENTS_COMPLETED";
		const string bufferGrowthMessage = "Allocated timing event buffer from 4096 to 8192";
		const string dumpCompletedMessage = "[2/8] Assembly decompression";
		string timingFileName = useBundledLongFileName ? $"fast-timing-{new string ('x', 128)}.txt" : "fast-timing.txt";

		if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
			return;
		}

		string packageName = PackageUtils.MakePackageName (AndroidRuntime.CoreCLR, "fasttiming");
		var proj = new XamarinAndroidApplicationProject (packageName: packageName);
		proj.SetRuntime (AndroidRuntime.CoreCLR);
		proj.SetRuntimeIdentifiers ([DeviceAbi]);
		proj.SetProperty ("AndroidTypeMapImplementation", "llvm-ir");
		proj.SetProperty ("_AndroidFastTiming", "True");
		proj.SetDefaultTargetDevice ();
		if (useBundledLongFileName) {
			proj.OtherBuildItems.Add (new BuildItem ("AndroidEnvironment", "env.txt") {
				TextContent = () => $"debug.dotnet.timing=to-file,filename={timingFileName}",
			});
		}
		proj.MainActivity = proj.DefaultMainActivity
			.Replace ("//${USINGS}", "using System.Threading.Tasks;")
			.Replace (
				"//${AFTER_ONCREATE}",
				$$"""
			Parallel.For (0, 8, _ => {
				for (int i = 0; i < 1024; i++) {
					Android.Runtime.JNIEnv.GetJniName (typeof (MainActivity));
				}
			});
			Android.Util.Log.Info ("FastTimingTest", "{{completedMessage}}");
"""
			);

		using var builder = CreateApkBuilder (packageName: packageName);
		Assert.IsTrue (builder.Install (proj), "Project should have installed.");

		string previousDotnetLog = RunAdbCommand ("shell getprop debug.dotnet.log").Trim ();
		string previousDotnetTiming = RunAdbCommand ("shell getprop debug.dotnet.timing").Trim ();
		try {
			RunAdbCommand ("shell setprop debug.dotnet.log timing=fast-bare");
			if (useBundledLongFileName) {
				RunAdbCommand ("shell setprop debug.dotnet.timing \"\"");
			} else {
				RunAdbCommand ($"shell setprop debug.dotnet.timing to-file,filename={timingFileName}");
			}
			ClearAdbLogcat ();

			bool sawBufferGrowth = false;
			bool appCompleted = MonitorAdbLogcat (
				line => {
					sawBufferGrowth |= line.Contains (bufferGrowthMessage, StringComparison.Ordinal);
					return line.Contains (completedMessage, StringComparison.Ordinal);
				},
				Path.Combine (Root, builder.ProjectDirectory, "fast-timing-events.log"),
				timeout: 60,
				onMonitoringStarted: () => StartActivityAndAssert (proj)
			);

			Assert.IsTrue (appCompleted, $"Output did not contain {completedMessage}.");
			Assert.IsTrue (sawBufferGrowth, $"Output did not contain {bufferGrowthMessage}.");

			RunAdbCommand ($"shell run-as {proj.PackageName} rm -f cache/{timingFileName}");
			RunAdbCommand (
				$"shell am broadcast -a mono.android.app.DUMP_TIMING_DATA -n {proj.PackageName}/mono.android.app.DumpTimingData",
				timeout: 60
			);

			string timingOutput = RunAdbCommand ($"exec-out run-as {proj.PackageName} cat cache/{timingFileName}");
			Assert.IsTrue (timingOutput.Contains (dumpCompletedMessage, StringComparison.Ordinal), $"Output did not contain {dumpCompletedMessage}.");
		} finally {
			RunAdbCommand ($"shell am force-stop {proj.PackageName}");
			RunAdbCommand ($"shell run-as {proj.PackageName} rm -f cache/{timingFileName}");
			string dotnetLogValue = previousDotnetLog.Length == 0 ? "\"\"" : $"\"{previousDotnetLog}\"";
			RunAdbCommand ($"shell setprop debug.dotnet.log {dotnetLogValue}");
			string dotnetTimingValue = previousDotnetTiming.Length == 0 ? "\"\"" : $"\"{previousDotnetTiming}\"";
			RunAdbCommand ($"shell setprop debug.dotnet.timing {dotnetTimingValue}");
		}
	}
}
