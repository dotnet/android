using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("TimeZoneInfo")]
	[NonParallelizable]
	public class TimeZoneInfoTests : DeviceTest
	{
		static ProjectBuilder builder;

		[OneTimeSetUp]
		public void BeforeAllTests ()
		{
			string debuggable = RunAdbCommand ("shell getprop ro.debuggable");
			if (debuggable != "1") {
				Assert.Fail ("TimeZoneInfoTests need to use `su root` and this device does not support that feature. Try using an emulator.");
			}
			// Disable auto timezone
			RunAdbCommand ("shell settings put global auto_time_zone 0");

			builder = CreateApkBuilder (Path.Combine ("temp", "TimeZoneInfoTests"));
		}

		[SetUp]
		public override void SetupTest ()
		{
			if (!IsDeviceAttached (refreshCachedValue: true)) {
				RestartDevice ();
				AssertHasDevices ();
			}
		}

		/// <summary>
		/// Calling BaseTest.CleanupTest() will cause our shared test directory to be deleted after any test passes
		/// This can be problematic for cases that run dozens or hundreds of tests with the same root (CheckTimeZoneInfoIsCorrect, CheckLocalizationIsCorrect)
		/// </summary>
		[TearDown]
		protected override void CleanupTest ()
		{
			var tzParam = TestContext.CurrentContext.Test.Arguments[0] as string;
			if (!string.IsNullOrEmpty (tzParam)) {
				tzParam = tzParam.Replace ("/", "-");
			}

			string output = Path.Combine (Root, builder?.ProjectDirectory);
			if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed && Directory.Exists (output)) {
				foreach (var testFile in Directory.GetFiles (output, $"*{tzParam}*log", SearchOption.AllDirectories)) {
					TestContext.AddTestAttachment (testFile, Path.GetFileNameWithoutExtension (testFile));
				}
			}
		}

		[OneTimeTearDown]
		public void AfterAllTests ()
		{
			string output = Path.Combine (Root, builder?.ProjectDirectory);
			if (TestContext.CurrentContext.Result.FailCount == 0 && Directory.Exists (output)) {
				try {
					Directory.Delete (output, recursive: true);
				} catch (IOException ex) {
					// This happens on CI occasionally, let's not fail the test
					TestContext.Out.WriteLine ($"Failed to delete '{output}': {ex}");
				}
			}
		}


		const int TIMEZONE_NODE_COUNT = 15;
		const int TIMEZONE_RETRY_COUNT = 3;

		static object [] GetTimeZoneTestCases (int node)
		{
			List<object> tests = new List<object> ();
			var ignore = new string [] {
				"Asia/Qostanay",
				"US/Pacific-New"
			};

			foreach (var tz in NodaTime.DateTimeZoneProviders.Tzdb.Ids) {
				if (ignore.Contains (tz)) {
					TestContext.WriteLine ($"Ignoring {tz} TimeZone Test");
					continue;
				}
				tests.Add (new object [] {
					tz,
				});
			}
			return tests.Where (p => tests.IndexOf (p) % TIMEZONE_NODE_COUNT == node).ToArray ();
		}

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 0 })]
		public void CheckTimeZoneInfoIsCorrectNode1 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 1 })]
		public void CheckTimeZoneInfoIsCorrectNode2 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 2 })]
		public void CheckTimeZoneInfoIsCorrectNode3 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 3 })]
		public void CheckTimeZoneInfoIsCorrectNode4 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 4 })]
		public void CheckTimeZoneInfoIsCorrectNode5 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 5 })]
		public void CheckTimeZoneInfoIsCorrectNode6 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 6 })]
		public void CheckTimeZoneInfoIsCorrectNode7 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 7 })]
		public void CheckTimeZoneInfoIsCorrectNode8 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 8 })]
		public void CheckTimeZoneInfoIsCorrectNode9 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 9 })]
		public void CheckTimeZoneInfoIsCorrectNode10 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 10 })]
		public void CheckTimeZoneInfoIsCorrectNode11 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 11 })]
		public void CheckTimeZoneInfoIsCorrectNode12 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 12 })]
		public void CheckTimeZoneInfoIsCorrectNode13 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 13 })]
		public void CheckTimeZoneInfoIsCorrectNode14 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 14 })]
		public void CheckTimeZoneInfoIsCorrectNode15 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);


		public void CheckTimeZoneInfoIsCorrect (string timeZone)
		{
			var proj = new XamarinAndroidApplicationProject (packageName: "TimeZoneInfoTests");
			if (Builder.UseDotNet) {
				proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", @"button.Text = $""TimeZoneInfo={TimeZoneInfo.Local.Id}"";
				Console.WriteLine ($""TimeZoneInfoNative={Java.Util.TimeZone.Default.ID}"");
				Console.WriteLine ($""TimeZoneInfoTests.TimeZoneInfo={TimeZoneInfo.Local.Id}"");
");
			} else {
				proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", @"button.Text = $""TimeZoneInfo={TimeZoneInfo.Local.DisplayName}"";
				Console.WriteLine ($""TimeZoneInfoNative={Java.Util.TimeZone.Default.ID}"");
				Console.WriteLine ($""TimeZoneInfoTests.TimeZoneInfo={TimeZoneInfo.Local.DisplayName}"");
");
			}

			var appStartupLogcatFile = Path.Combine (Root, builder.ProjectDirectory, $"startup-logcat-{timeZone.Replace ("/", "-")}.log");
			string deviceTz = RunAdbCommand ("shell getprop persist.sys.timezone")?.Trim ();

			if (deviceTz != timeZone) {
				for (int attempt = 0; attempt < 5; attempt++) {
					TestContext.Out.WriteLine ($"{nameof (CheckTimeZoneInfoIsCorrect)}: Setting TimeZone to {timeZone}, attempt {attempt}...");
					ClearAdbLogcat ();
					RunAdbCommand ($"shell su root setprop persist.sys.timezone \"{timeZone}\"");
					deviceTz = RunAdbCommand ("shell getprop persist.sys.timezone")?.Trim ();
					if (deviceTz == timeZone) {
						break;
					}
				}
			}

			Assert.AreEqual (timeZone, deviceTz, $"The command to set the device timezone to {timeZone} failed. Current device timezone is {deviceTz}");
			builder.BuildLogFile = $"install-{timeZone.Replace ("/", "-")}.log";
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
			ClearAdbLogcat ();
			RunAdbCommand ($"shell am force-stop --user all {proj.PackageName}");
			RunAdbCommand ($"shell am kill --user all {proj.PackageName}");
			RunProjectAndAssert (proj, builder, logName: $"run-{timeZone.Replace ("/", "-")}.log");

			string logcatSearchString = "TimeZoneInfoTests.TimeZoneInfo=";
			string expectedLogcatOutput = $"{logcatSearchString}{timeZone}";
			string logLine = string.Empty;

			Assert.IsTrue (MonitorAdbLogcat ((line) => {
				if (line.Contains (logcatSearchString)) {
					logLine = line;
					return true;
				}
				return false;
			}, appStartupLogcatFile, 45), $"App output did not contain '{logcatSearchString}'");

			Assert.IsTrue (logLine.Contains (expectedLogcatOutput), $"Line '{logLine}' did not contain '{expectedLogcatOutput}'");
		}
	}
}
