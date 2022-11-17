using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Xamarin.ProjectTools;
using System.Xml.XPath;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice"), Category ("Node-2")]
	[NonParallelizable]
	public class DeploymentTest : DeviceTest {

		static ProjectBuilder builder;
		static XamarinFormsAndroidApplicationProject proj;

		[OneTimeSetUp]
		public void BeforeDeploymentTests ()
		{
			AssertHasDevices ();

			string debuggable = RunAdbCommand ("shell getprop ro.debuggable");
			if (debuggable != "1") {
				Assert.Ignore ("TimeZone tests need to use `su root` and this device does not support that feature. Try using an emulator.");
			}
			// Disable auto timezone
			RunAdbCommand ("shell settings put global auto_time_zone 0");

			proj = new XamarinFormsAndroidApplicationProject ();
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86", "x86_64");
			var mainPage = proj.Sources.First (x => x.Include () == "MainPage.xaml.cs");
			var source = mainPage.TextContent ().Replace ("InitializeComponent ();", @"InitializeComponent ();
			Console.WriteLine ($""TimeZoneInfoNative={Java.Util.TimeZone.Default.ID}"");
			Console.WriteLine ($""TimeZoneInfo={TimeZoneInfo.Local.DisplayName}"");
			Console.WriteLine ($""LocaleNative={Java.Util.Locale.Default.Language}-{Java.Util.Locale.Default.Country}"");
			Console.WriteLine ($""CurrentCulture={System.Globalization.CultureInfo.CurrentCulture.Name}"");
			Console.WriteLine ($""Strings.SomeString={Strings.SomeString}"");
			myLabel.Text = Strings.SomeString;
");
			source = source.Replace ("Console.WriteLine (\"Button was Clicked!\");", @"Console.WriteLine (""Button was Clicked!"");
			Console.WriteLine ($""TimeZoneInfoClick={TimeZoneInfo.Local.DisplayName}"");
			Console.WriteLine ($""CurrentCultureClick={System.Globalization.CultureInfo.CurrentCulture.Name}"");
			Console.WriteLine ($""StringsClick={Strings.SomeString}"");
");
			mainPage.TextContent = () => source;
			builder = CreateApkBuilder (Path.Combine ("temp", "DeploymentTests"));
			string apiLevel;
			proj.TargetFrameworkVersion = builder.LatestTargetFrameworkVersion (out apiLevel);

			proj.PackageName = "Xamarin.TimeZoneTest";
			proj.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""Xamarin.TimeZoneTest"">
	<uses-sdk android:minSdkVersion=""24"" android:targetSdkVersion=""{apiLevel}"" />
	<application android:label=""${{PROJECT_NAME}}"">
	</application >
</manifest> ";
			InlineData.AddCultureResourcesToProject (proj, "Strings", "SomeString");
			InlineData.AddCultureResourceDesignerToProject (proj, proj.RootNamespace ?? proj.ProjectName, "Strings", "SomeString");

			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			builder.BuildLogFile = "install.log";
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
		}

		[OneTimeTearDown]
		public void AfterDeploymentTests ()
		{
			if (HasDevices && proj != null)
				RunAdbCommand ($"uninstall {proj.PackageName}");

			if (builder != null)
				return;

			string output = Path.Combine (Root, builder.ProjectDirectory);
			if (TestContext.CurrentContext.Result.FailCount == 0 && Directory.Exists (output)) {
				Directory.Delete (output, recursive: true);
				return;
			}

			foreach (var file in Directory.GetFiles (output, "*.log", SearchOption.AllDirectories)) {
				TestContext.AddTestAttachment (file, Path.GetFileName (output));
			}
		}

		[Test]
		public void CheckResouceIsOverridden ([Values (true, false)] bool useAapt2)
		{
			AssertHasDevices ();
			AssertAaptSupported (useAapt2);

			var library = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				AndroidResources = {
					new AndroidItem.AndroidResource (() => "Resources\\values\\strings2.xml") {
						TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""hello_me"">Click Me! One</string>
</resources>",
					},
				},
			};
			var library2 = new XamarinAndroidLibraryProject () {
				ProjectName = "Library2",
				AndroidResources = {
					new AndroidItem.AndroidResource (() => "Resources\\values\\strings2.xml") {
						TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""hello_me"">Click Me! Two</string>
</resources>",
					},
				},
			};
			var app = new XamarinAndroidApplicationProject () {
				PackageName = "Xamarin.ResourceTest",
				References = {
					new BuildItem.ProjectReference ("..\\Library1\\Library1.csproj"),
					new BuildItem.ProjectReference ("..\\Library2\\Library2.csproj"),
				},
			};
			library.AndroidUseAapt2 =
				library2.AndroidUseAapt2 =
				app.AndroidUseAapt2 = useAapt2;
			app.LayoutMain = app.LayoutMain.Replace ("@string/hello", "@string/hello_me");
			using (var l1 = CreateDllBuilder (Path.Combine ("temp", TestName, library.ProjectName)))
			using (var l2 = CreateDllBuilder (Path.Combine ("temp", TestName, library2.ProjectName)))
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName, app.ProjectName))) {
				b.ThrowOnBuildFailure = false;
				string apiLevel;
				app.TargetFrameworkVersion = b.LatestTargetFrameworkVersion (out apiLevel);

				app.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{app.PackageName}"">
	<uses-sdk android:minSdkVersion=""24"" android:targetSdkVersion=""{apiLevel}"" />
	<application android:label=""${{PROJECT_NAME}}"">
	</application >
</manifest> ";
				Assert.IsTrue (l1.Build (library, doNotCleanupOnUpdate: true), $"Build of {library.ProjectName} should have suceeded.");
				Assert.IsTrue (l2.Build (library2, doNotCleanupOnUpdate: true), $"Build of {library2.ProjectName} should have suceeded.");
				b.BuildLogFile = "build1.log";
				Assert.IsTrue (b.Build (app, doNotCleanupOnUpdate: true), $"Build of {app.ProjectName} should have suceeded.");
				b.BuildLogFile = "install1.log";
				Assert.IsTrue (b.Install (app, doNotCleanupOnUpdate: true), "Install should have suceeded.");
				AdbStartActivity ($"{app.PackageName}/{app.JavaPackageName}.MainActivity");
				WaitForPermissionActivity (Path.Combine (Root, builder.ProjectDirectory, "permission-logcat.log"));
				WaitForActivityToStart (app.PackageName, "MainActivity",
					Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"), 15);
				ClearBlockingDialogs ();
				XDocument ui = GetUI ();
				XElement node = ui.XPathSelectElement ($"//node[contains(@resource-id,'myButton')]");
				Assert.IsNotNull (node , "Could not find `my-Button` in the user interface. Check the screenshot of the test failure.");
				StringAssert.AreEqualIgnoringCase ("Click Me! One", node.Attribute ("text").Value, "Text of Button myButton should have been \"Click Me! One\"");
				b.BuildLogFile = "clean.log";
				Assert.IsTrue (b.Clean (app, doNotCleanupOnUpdate: true), "Clean should have suceeded.");

				app = new XamarinAndroidApplicationProject () {
					PackageName = "Xamarin.ResourceTest",
					References = {
						new BuildItem.ProjectReference ("..\\Library1\\Library1.csproj"),
						new BuildItem.ProjectReference ("..\\Library2\\Library2.csproj"),
					},
				};

				library2.References.Add (new BuildItem.ProjectReference ("..\\Library1\\Library1.csproj"));
				app.AndroidUseAapt2 = useAapt2;
				app.LayoutMain = app.LayoutMain.Replace ("@string/hello", "@string/hello_me");
				app.TargetFrameworkVersion = b.LatestTargetFrameworkVersion (out apiLevel);

				app.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{app.PackageName}"">
	<uses-sdk android:minSdkVersion=""24"" android:targetSdkVersion=""{apiLevel}"" />
	<application android:label=""${{PROJECT_NAME}}"">
	</application >
</manifest> ";
				b.BuildLogFile = "build.log";
				Assert.IsTrue (b.Build (app, doNotCleanupOnUpdate: true), $"Build of {app.ProjectName} should have suceeded.");
				b.BuildLogFile = "install.log";
				Assert.IsTrue (b.Install (app, doNotCleanupOnUpdate: true), "Install should have suceeded.");
				AdbStartActivity ($"{app.PackageName}/{app.JavaPackageName}.MainActivity");
				WaitForPermissionActivity (Path.Combine (Root, builder.ProjectDirectory, "permission-logcat.log"));
				WaitForActivityToStart (app.PackageName, "MainActivity",
					Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"), 15);
				ui = GetUI ();
				node = ui.XPathSelectElement ($"//node[contains(@resource-id,'myButton')]");
				StringAssert.AreEqualIgnoringCase ("Click Me! One", node.Attribute ("text").Value, "Text of Button myButton should have been \"Click Me! One\"");

			}
		}


		[Test]
		public void CheckXamarinFormsAppDeploysAndAButtonWorks ()
		{
			AssertHasDevices ();

			AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");
			WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"), 15);
			ClearAdbLogcat ();
			ClearBlockingDialogs ();
			ClickButton (proj.PackageName, "myXFButton", "CLICK ME");
			Assert.IsTrue (MonitorAdbLogcat ((line) => {
				return line.Contains ("Button was Clicked!");
			}, Path.Combine (Root, builder.ProjectDirectory, "button-logcat.log")), "Button Should have been Clicked.");
		}

		private const int TIMEZONE_NODE_COUNT = 4;

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
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 0 })]
		[Category ("TimeZoneInfo")]
		[Retry (2)]
		public void CheckTimeZoneInfoIsCorrectNode1 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 1 })]
		[Category ("TimeZoneInfo")]
		[Retry (2)]
		public void CheckTimeZoneInfoIsCorrectNode2 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 2 })]
		[Category ("TimeZoneInfo")]
		[Retry (2)]
		public void CheckTimeZoneInfoIsCorrectNode3 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 3 })]
		[Category ("TimeZoneInfo")]
		[Retry (2)]
		public void CheckTimeZoneInfoIsCorrectNode4 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		public void CheckTimeZoneInfoIsCorrect (string timeZone)
		{
			AssertHasDevices ();

			string currentTimeZone = RunAdbCommand ("shell getprop persist.sys.timezone")?.Trim ();
			string deviceTz = string.Empty;
			string logFile = Path.Combine (Root, builder.ProjectDirectory, $"startup-logcat-{timeZone.Replace ("/", "-")}.log");
			try {
				for (int attempt = 0; attempt < 5; attempt++) {
					RunAdbCommand ($"shell su root setprop persist.sys.timezone \"{timeZone}\"");
					deviceTz = RunAdbCommand ("shell getprop persist.sys.timezone")?.Trim ();
					if (deviceTz == timeZone) {
						break;
					}
				}
				Assert.AreEqual (timeZone, deviceTz, $"The command to set the device timezone to {timeZone} failed. Current device timezone is {deviceTz}");
				ClearAdbLogcat ();
				RunAdbCommand ($"shell am force-stop --user all {proj.PackageName}");
				RunAdbCommand ($"shell am kill --user all {proj.PackageName}");
				WaitFor ((int)TimeSpan.FromSeconds (2).TotalMilliseconds);
				ClearAdbLogcat ();
				AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");
				Assert.IsTrue (WaitForActivityToStart (proj.PackageName, "MainActivity", logFile), "Activity should have started");
				string line = "";
				string logCatFile = Path.Combine (Root, builder.ProjectDirectory, $"timezone-logcat-{timeZone.Replace ("/", "-")}.log");
				ClickButton (proj.PackageName, "myXFButton", "CLICK ME");
				Assert.IsTrue (MonitorAdbLogcat ((l) => {
					if (l.Contains ("TimeZoneInfoClick=")) {
						line = l;
						return l.Contains ($"{timeZone}");
					}
					return false;
				}, logCatFile, timeout:30), $"TimeZone should have been {timeZone}. We found : {line}");
			} finally {
				RunAdbCommand ($"shell am force-stop --user all {proj.PackageName}");
				RunAdbCommand ($"shell am kill --user all {proj.PackageName}");
				if (!string.IsNullOrEmpty (currentTimeZone)) {
					RunAdbCommand ($"shell su root setprop persist.sys.timezone \"{currentTimeZone}\"");
				}
				if (File.Exists (logFile)) {
					TestContext.AddTestAttachment (logFile);
				}
			}
		}

		private const int LOCALIZATION_NODE_COUNT = 12;

		static object [] GetLocalizationTestCases (int node)
		{
			List<object> tests = new List<object> ();
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
				tests.Add (new object [] {
					ci.Name,
				});
			}

			return tests.Where (p => tests.IndexOf (p) % LOCALIZATION_NODE_COUNT == node).ToArray ();
		}

		[Test]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 0 })]
		[Category ("Localization")]
		[Retry (2)]
		public void CheckLocalizationIsCorrectNode1 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 1 })]
		[Category ("Localization")]
		[Retry (2)]
		public void CheckLocalizationIsCorrectNode2 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 2 })]
		[Category ("Localization")]
		[Retry (2)]
		public void CheckLocalizationIsCorrectNode3 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 3 })]
		[Category ("Localization")]
		[Retry (2)]
		public void CheckLocalizationIsCorrectNode4 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 4 })]
		[Category ("Localization")]
		[Retry (2)]
		public void CheckLocalizationIsCorrectNode5 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 5 })]
		[Category ("Localization")]
		[Retry (2)]
		public void CheckLocalizationIsCorrectNode6 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 6 })]
		[Category ("Localization")]
		[Retry (2)]
		public void CheckLocalizationIsCorrectNode7 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 7 })]
		[Category ("Localization")]
		[Retry (2)]
		public void CheckLocalizationIsCorrectNode8 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 8 })]
		[Category ("Localization")]
		[Retry (2)]
		public void CheckLocalizationIsCorrectNode9 (string locale) => CheckLocalizationIsCorrect (locale);

		[Test]
		[TestCaseSource (nameof (GetLocalizationTestCases), new object [] { 9 })]
		[Category ("Localization")]
		[Retry (2)]
		public void CheckLocalizationIsCorrectNode10 (string locale) => CheckLocalizationIsCorrect (locale);

		public void CheckLocalizationIsCorrect (string locale)
		{
			AssertHasDevices ();

			string currentLocale = RunAdbCommand ("shell getprop persist.sys.locale")?.Trim ();
			TestContext.Out.WriteLine ($"{nameof(CheckLocalizationIsCorrect)}: Current Locale is {currentLocale}");
			string deviceLocale = currentLocale;
			string logFile = Path.Combine (Root, builder.ProjectDirectory, $"startup-logcat-{locale.Replace ("/", "-")}.log");
			string monitorLogFile = Path.Combine (Root, builder.ProjectDirectory, $"monitor-logcat-{locale.Replace ("/", "-")}.log");
			try {
				TestContext.Out.WriteLine ($"{nameof(CheckLocalizationIsCorrect)}: Setting Locale to {locale}");
				if (deviceLocale != locale) {
					for (int attempt = 0; attempt < 5; attempt++) {
						TestContext.Out.WriteLine ($"{nameof(CheckLocalizationIsCorrect)}: attempt {attempt}");
						RunAdbCommand ($"shell su root setprop persist.sys.locale {locale}");
						RunAdbCommand ("shell su root setprop ctl.restart zygote");
						if (!MonitorAdbLogcat ((l) => {
							if (l.Contains ("Finished processing BOOT_COMPLETED for"))
								return true;
							return false;
						}, monitorLogFile, timeout:60)) {
							TestContext.Out.WriteLine ($"{nameof(CheckLocalizationIsCorrect)}: wating for boot to complete failed or timed out.");
						}
						WaitFor ((int)TimeSpan.FromSeconds (5).TotalMilliseconds);
						deviceLocale = RunAdbCommand ("shell getprop persist.sys.locale")?.Trim ();
						if (deviceLocale == locale) {
							break;
						}
					}
				}
				Assert.AreEqual (locale, deviceLocale, $"The command to set the device locale to {locale} failed. Current device locale is {deviceLocale}");
				ClearAdbLogcat ();
				RunAdbCommand ($"shell am force-stop --user all {proj.PackageName}");
				RunAdbCommand ($"shell am kill --user all {proj.PackageName}");
				WaitFor ((int)TimeSpan.FromSeconds (2).TotalMilliseconds);
				ClearAdbLogcat ();
				AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");
				Assert.IsTrue (WaitForActivityToStart (proj.PackageName, "MainActivity", logFile, timeout: 120), "Activity should have started");
				string line = "";
				string logCatFile = Path.Combine (Root, builder.ProjectDirectory, $"locale-logcat-{locale.Replace ("/", "-")}.log");
				ClickButton (proj.PackageName, "myXFButton", "CLICK ME");
				Assert.IsTrue (MonitorAdbLogcat ((l) => {
					if (l.Contains ("StringsClick=") || l.Contains ("Strings.SomeString=")) {
						line = l;
						bool result = l.Contains ($"{locale}");
						if (l.Contains ("Strings.SomeString=") && !result)
							return false;
						return result;
					}
					return false;
				}, logCatFile, timeout:30), $"Locale should have been {locale}. We found : {line}");
			} finally {
				RunAdbCommand ($"shell am force-stop --user all {proj.PackageName}");
				RunAdbCommand ($"shell am kill --user all {proj.PackageName}");
				if (!string.IsNullOrEmpty (currentLocale)) {
					TestContext.Out.WriteLine ($"{nameof(CheckLocalizationIsCorrect)}: Setting Locale back to {currentLocale}");
					RunAdbCommand ($"shell su root setprop persist.sys.locale \"{currentLocale}\"");
					RunAdbCommand ("shell su root setprop ctl.restart zygote");
					MonitorAdbLogcat ((l) => {
						if (l.Contains ("Finished processing BOOT_COMPLETED for"))
							return true;
						return false;
					}, monitorLogFile, timeout:60);
					WaitFor ((int)TimeSpan.FromSeconds (2).TotalMilliseconds);
				}
				if (File.Exists (logFile)) {
					TestContext.AddTestAttachment (logFile);
				}
				if (File.Exists (monitorLogFile)) {
					TestContext.AddTestAttachment (monitorLogFile);
				}
			}
		}
	}
}
