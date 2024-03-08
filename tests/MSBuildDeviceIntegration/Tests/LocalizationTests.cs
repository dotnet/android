using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Mono.Unix.Native;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Xamarin.ProjectTools;
using Humanizer;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("Localization")]
	[NonParallelizable]
	public class LocalizationTests : DeviceTest
	{
		ProjectBuilder builder;
		XamarinAndroidApplicationProject proj;
		string localeFileSuffix;

		[OneTimeSetUp]
		public void BeforeAllTests ()
		{
			AssertHasDevices ();

			string debuggable = RunAdbCommand ("shell getprop ro.debuggable");
			if (debuggable != "1") {
				Assert.Fail ("LocalizationTests need to use `su root` and this device does not support that feature. Try using an emulator.");
			}

			proj = new XamarinAndroidApplicationProject (packageName: "LocalizationTests");
			proj.PackageReferences.Add (new Package {
				Id = "Humanizer",
				Version = "2.14.1",
			});
			var source = proj.DefaultMainActivity
				.Replace ("//${USINGS}", @"using Humanizer;
using System.Globalization;");
			source = source.Replace ("//${AFTER_ONCREATE}", @"button.Text = $""Strings.SomeString={Strings.SomeString}"";
			Console.WriteLine ($""LocaleNative={Java.Util.Locale.Default.Language}-{Java.Util.Locale.Default.Country}"");
			Console.WriteLine ($""CurrentCulture={System.Globalization.CultureInfo.CurrentCulture.Name}"");
			Console.WriteLine ($""Strings.SomeString={Strings.SomeString}"");
			Console.WriteLine ($""Humanizer={DateTime.UtcNow.AddHours(-30).Humanize()}"");
");
			proj.MainActivity = source;
			InlineData.AddCultureResourcesToProject (proj, "Strings", "SomeString");
			InlineData.AddCultureResourceDesignerToProject (proj, proj.RootNamespace ?? proj.ProjectName, "Strings", "SomeString");

			builder = CreateApkBuilder (Path.Combine ("temp", "LocalizationTests"));
			builder.BuildLogFile = "onetimesetup-install.log";
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
		}

		[SetUp]
		public override void SetupTest ()
		{
			var localeParam = TestContext.CurrentContext.Test.Arguments[0] as string;
			if (!string.IsNullOrEmpty (localeParam)) {
				localeFileSuffix = localeParam.Replace ("/", "-");
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
				foreach (var testFile in Directory.GetFiles (output, $"*{localeFileSuffix}*log", SearchOption.AllDirectories)) {
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


		const int LOCALIZATION_NODE_COUNT = 15;
		const int LOCALIZATION_RETRY_COUNT = 3;

		static List<string> GetLocalizationTestInfo ()
		{
			var tests = new List<string> ();
			var ignore = new string [] {
				"he-IL", // maps to wi-IL on Android.
				"id-ID", // maps to in-ID on Android
			};
			foreach (CultureInfo ci in CultureInfo.GetCultures(CultureTypes.SpecificCultures)) {
				if (ci.Name.Length > 5) {
					TestContext.WriteLine ($"Skipping {ci.Name} Localization Test");
					continue;
				}
				if (ignore.Contains (ci.Name)) {
					TestContext.WriteLine ($"Ignoring {ci.Name} Localization Test");
					continue;
				}
				tests.Add (ci.Name);
			}
			return tests;
		}

		static object [] GetLocalizationTestCases (int node)
		{
			var tests = GetLocalizationTestInfo ();
			return tests.Where (p => tests.IndexOf (p) % LOCALIZATION_NODE_COUNT == node).ToArray ();
		}

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 0 })]
		public void CheckLocalizationIsCorrectNode1 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 1 })]
		public void CheckLocalizationIsCorrectNode2 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 2 })]
		public void CheckLocalizationIsCorrectNode3 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 3 })]
		public void CheckLocalizationIsCorrectNode4 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 4 })]
		public void CheckLocalizationIsCorrectNode5 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 5 })]
		public void CheckLocalizationIsCorrectNode6 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 6 })]
		public void CheckLocalizationIsCorrectNode7 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 7 })]
		public void CheckLocalizationIsCorrectNode8 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 8 })]
		public void CheckLocalizationIsCorrectNode9 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 9 })]
		public void CheckLocalizationIsCorrectNode10 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 10 })]
		public void CheckLocalizationIsCorrectNode11 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 11 })]
		public void CheckLocalizationIsCorrectNode12 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 12 })]
		public void CheckLocalizationIsCorrectNode13 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 13 })]
		public void CheckLocalizationIsCorrectNode14 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 14 })]
		public void CheckLocalizationIsCorrectNode15 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[Retry (LOCALIZATION_RETRY_COUNT)]
		[TestCaseSource (nameof (GetLocalizationTestInfo))]
		public void CheckLocalizationIsCorrectWithSlicer (string locale) => CheckLocalizationIsCorrect (locale);


		public void CheckLocalizationIsCorrect (string locale)
		{
			AssertHasDevices ();

			// Attempt to reinstall the app that was installed during fixture setup if it is missing
			var packageOutput = RunAdbCommand ($"shell pm list packages {proj.PackageName}").Trim ();
			var expectedPackageOutput = $"package:{proj.PackageName}";
			if (packageOutput != expectedPackageOutput) {
				builder.BuildLogFile = $"setup-install-{localeFileSuffix}.log";
				Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
			}

			RunAdbCommand ($"shell am force-stop --user all {proj.PackageName}");
			RunAdbCommand ($"shell am kill --user all {proj.PackageName}");

			var appStartupLogcatFile = Path.Combine (Root, builder.ProjectDirectory, $"startup-logcat-{locale.Replace ("/", "-")}.log");
			string deviceLocale = RunAdbCommand ("shell getprop persist.sys.locale")?.Trim ();
			TestContext.Out.WriteLine ($"test value:{locale}, prop value:{deviceLocale}");

			for (int attempt = 0; attempt < 5; attempt++) {
				TestContext.Out.WriteLine ($"{nameof(CheckLocalizationIsCorrect)}: Setting Locale to {locale}, attempt {attempt}...");
				ClearAdbLogcat ();
				var rebootLogcatFile = Path.Combine (Root, builder.ProjectDirectory, $"reboot{attempt}-logcat-{locale.Replace ("/", "-")}.log");

				// https://developer.android.com/guide/topics/resources/localization#changing-the-emulator-locale-from-the-adb-shell
				RunAdbCommand ($"shell \"su root setprop persist.sys.locale {locale};su root stop;sleep 5;su root start;\"");

				if (!MonitorAdbLogcat ((l) => {
					if (l.Contains ("ActivityManager: Finished processing BOOT_COMPLETED"))
						return true;
					return false;
				}, rebootLogcatFile, timeout: 60)) {
					TestContext.Out.WriteLine ($"{nameof(CheckLocalizationIsCorrect)}: wating for boot to complete failed or timed out.");
				}
				deviceLocale = RunAdbCommand ("shell getprop persist.sys.locale")?.Trim ();
				if (deviceLocale == locale) {
					break;
				}
			}

			Assert.AreEqual (locale, deviceLocale, $"The command to set the device locale to {locale} failed. Current device locale is {deviceLocale}");
			ClearAdbLogcat ();
			Thread.Sleep (1000);
			StartActivityAndAssert (proj);

			string logcatSearchString = "Strings.SomeString=";
			string expectedLogcatOutput = $"{logcatSearchString}{locale}";
			string logLine = string.Empty;

			Assert.IsTrue (MonitorAdbLogcat ((line) => {
				if (line.Contains (logcatSearchString)) {
					logLine = line;
					return true;
				}
				return false;
			}, appStartupLogcatFile, 45), $"App output did not contain '{logcatSearchString}'");
			Assert.IsTrue (logLine.Contains (expectedLogcatOutput), $"Line '{logLine}' did not contain '{expectedLogcatOutput}'");

			string humanizerLogCatFile = Path.Combine (Root, builder.ProjectDirectory, $"humanizer-logcat-{locale.Replace ("/", "-")}.log");
			var culture = new CultureInfo (locale);
			expectedLogcatOutput = DateTime.UtcNow.AddHours(-30).Humanize(culture: culture);
			logcatSearchString = "Humanizer=";
			Assert.IsTrue (MonitorAdbLogcat ((line) => {
				if (line.Contains (logcatSearchString)) {
					logLine = line;
					return true;
				}
				return false;
			}, humanizerLogCatFile, timeout:45), $"App output did not contain '{logcatSearchString}'");
			Assert.IsTrue (logLine.Contains (expectedLogcatOutput), $"Line '{logLine}' did not contain '{expectedLogcatOutput}'");
		}
	}
}
