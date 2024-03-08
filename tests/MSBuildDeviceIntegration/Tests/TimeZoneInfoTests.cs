using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
		ProjectBuilder builder;
		XamarinAndroidApplicationProject proj;
		string tzFileSuffix;

		[OneTimeSetUp]
		public void BeforeAllTests ()
		{
			AssertHasDevices ();

			string debuggable = RunAdbCommand ("shell getprop ro.debuggable");
			if (debuggable != "1") {
				Assert.Fail ("TimeZoneInfoTests need to use `su root` and this device does not support that feature. Try using an emulator.");
			}
			// Disable auto timezone
			RunAdbCommand ("shell settings put global auto_time_zone 0");

			proj = new XamarinAndroidApplicationProject (packageName: "TimeZoneInfoTests");
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", @"button.Text = $""TimeZoneInfo={TimeZoneInfo.Local.Id}"";
			Console.WriteLine ($""TimeZoneInfoNative={Java.Util.TimeZone.Default.ID}"");
			Console.WriteLine ($""TimeZoneInfoTests.TimeZoneInfo={TimeZoneInfo.Local.Id}"");
");

			builder = CreateApkBuilder (Path.Combine ("temp", "TimeZoneInfoTests"));
			builder.BuildLogFile = "onetimesetup-install.log";
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
		}

		[SetUp]
		public override void SetupTest ()
		{
			var tzParam = TestContext.CurrentContext.Test.Arguments[0] as string;
			if (!string.IsNullOrEmpty (tzParam)) {
				tzFileSuffix = tzParam.Replace ("/", "-");
			}
		}

		/// <summary>
		/// Calling BaseTest.CleanupTest() will cause our shared test directory to be deleted after any test passes
		/// This can be problematic for cases that run dozens or hundreds of tests with the same root (CheckTimeZoneInfoIsCorrect, CheckLocalizationIsCorrect)
		/// </summary>
		[TearDown]
		protected override void CleanupTest ()
		{
			string output = Path.Combine (Root, builder?.ProjectDirectory);
			if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed && Directory.Exists (output)) {
				foreach (var setupFile in Directory.GetFiles (output, $"*onetimesetup*log", SearchOption.AllDirectories)) {
					TestContext.AddTestAttachment (setupFile, Path.GetFileNameWithoutExtension (setupFile));
				}
				foreach (var testFile in Directory.GetFiles (output, $"*{tzFileSuffix}*log", SearchOption.AllDirectories)) {
					TestContext.AddTestAttachment (testFile, Path.GetFileNameWithoutExtension (testFile));
				}
			}
		}

		protected override void DeviceTearDown ()
		{
		}

		[OneTimeTearDown]
		protected override void AfterAllTests ()
		{
		}


		const int TIMEZONE_NODE_COUNT = 15;
		const int TIMEZONE_RETRY_COUNT = 3;

		static List<string> GetTimeZoneTestInfo ()
		{
			var tests = new List<string> ();
			var ignore = new string [] {
				"Asia/Qostanay",
				"US/Pacific-New"
			};

			foreach (var tz in NodaTime.DateTimeZoneProviders.Tzdb.Ids) {
				if (ignore.Contains (tz)) {
					TestContext.WriteLine ($"Ignoring {tz} TimeZone Test");
					continue;
				}
				tests.Add (tz);
			}
			return tests;
		}

		static object [] GetTimeZoneTestCases (int node)
		{
			var tests = GetTimeZoneTestInfo ();
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

		[Test]
		[Retry (TIMEZONE_RETRY_COUNT)]
		[TestCaseSource (nameof (GetTimeZoneTestInfo))]
		public void CheckTimeZoneInfoIsCorrectWithSlicer (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);


		public void CheckTimeZoneInfoIsCorrect (string timeZone)
		{
			AssertHasDevices ();

			// Attempt to reinstall the app that was installed during fixture setup if it is missing
			var packageOutput = RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim ();
			var expectedPackageOutput = $"package:{proj.PackageName}";
			if (packageOutput != expectedPackageOutput) {
				builder.BuildLogFile = $"setup-install-{tzFileSuffix}.log";
				Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
			}

			RunAdbCommand ($"shell am force-stop --user all {proj.PackageName}");
			RunAdbCommand ($"shell am kill --user all {proj.PackageName}");

			var appStartupLogcatFile = Path.Combine (Root, builder.ProjectDirectory, $"startup-logcat-{timeZone.Replace ("/", "-")}.log");
			RunAdbCommand ($"shell su root setprop persist.sys.timezone \"America/New_York\"");
			string deviceTz = RunAdbCommand ("shell getprop persist.sys.timezone")?.Trim ();
			TestContext.Out.WriteLine ($"test value:{timeZone}, prop value:{deviceTz}");

			for (int attempt = 0; attempt < 5; attempt++) {
				TestContext.Out.WriteLine ($"{nameof (CheckTimeZoneInfoIsCorrect)}: Setting TimeZone to {timeZone}, attempt {attempt}...");
				ClearAdbLogcat ();
				RunAdbCommand ($"shell su root setprop persist.sys.timezone \"{timeZone}\"");
				deviceTz = RunAdbCommand ("shell getprop persist.sys.timezone")?.Trim ();
				if (deviceTz == timeZone) {
					break;
				}
			}

			Assert.AreEqual (timeZone, deviceTz, $"The command to set the device timezone to {timeZone} failed. Current device timezone is {deviceTz}");
			ClearAdbLogcat ();
			Thread.Sleep (1000);
			StartActivityAndAssert (proj);

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
