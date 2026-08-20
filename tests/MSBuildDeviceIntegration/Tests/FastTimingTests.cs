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
	[Test]
	public void ConcurrentEventsCanGrowAndDump ()
	{
		const string completedMessage = "FAST_TIMING_EVENTS_COMPLETED";
		const string bufferGrowthMessage = "Allocated timing event buffer from 4096 to 8192";
		const string dumpCompletedMessage = "[2/8] Assembly decompression";

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

		string previousMonoLog = RunAdbCommand ("shell getprop debug.mono.log").Trim ();
		try {
			RunAdbCommand ("shell setprop debug.mono.log timing=fast-bare");
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

			bool dumpCompleted = MonitorAdbLogcat (
				line => line.Contains (dumpCompletedMessage, StringComparison.Ordinal),
				Path.Combine (Root, builder.ProjectDirectory, "fast-timing-dump.log"),
				timeout: 60,
				onMonitoringStarted: () => RunAdbCommand (
					$"shell am broadcast -a mono.android.app.DUMP_TIMING_DATA -n {proj.PackageName}/mono.android.app.DumpTimingData"
				)
			);

			Assert.IsTrue (dumpCompleted, $"Output did not contain {dumpCompletedMessage}.");
		} finally {
			RunAdbCommand ($"shell am force-stop {proj.PackageName}");
			string value = previousMonoLog.Length == 0 ? "\"\"" : $"\"{previousMonoLog}\"";
			RunAdbCommand ($"shell setprop debug.mono.log {value}");
		}
	}
}
