using System;
using System.Collections.Generic;
using System.Diagnostics;
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

[assembly: NonParallelizable]

namespace Xamarin.Android.Build.Tests
{
	[SingleThreaded, Category ("UsesDevice")]
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

			proj = new XamarinFormsAndroidApplicationProject ();
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86");
			var mainPage = proj.Sources.First (x => x.Include () == "MainPage.xaml.cs");
			var source = mainPage.TextContent ().Replace ("InitializeComponent ();", @"InitializeComponent ();
			Console.WriteLine ($""TimeZoneInfo={TimeZoneInfo.Local.DisplayName}"");
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
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
		}

		[OneTimeTearDown]
		public void AfterDeploymentTests ()
		{
			if (HasDevices && proj != null)
				RunAdbCommand ($"uninstall {proj.PackageName}");

			if (TestContext.CurrentContext.Result.FailCount == 0 && builder != null && Directory.Exists (Path.Combine (Root, builder.ProjectDirectory)))
				Directory.Delete (Path.Combine (Root, builder.ProjectDirectory), recursive: true);
		}

		[Test]
		public void CheckResouceIsOverridden ([Values (true, false)] bool useAapt2)
		{
			AssertHasDevices ();

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
			library.SetProperty ("AndroidUseAapt2", useAapt2.ToString ());
			library2.SetProperty ("AndroidUseAapt2", useAapt2.ToString ());
			app.SetProperty ("AndroidUseAapt2", useAapt2.ToString ());
			app.LayoutMain = app.LayoutMain.Replace ("@string/hello", "@string/hello_me");
			using (var l1 = CreateApkBuilder (Path.Combine ("temp", TestName, library.ProjectName)))
			using (var l2 = CreateApkBuilder (Path.Combine ("temp", TestName, library2.ProjectName)))
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
				XDocument ui = GetUI ();
				XElement node = ui.XPathSelectElement ($"//node[contains(@resource-id,'myButton')]");
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
				app.SetProperty ("AndroidUseAapt2", useAapt2.ToString ());
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
			ClickButton (proj.PackageName, "myXFButton", "CLICK ME");
			Assert.IsTrue (MonitorAdbLogcat ((line) => {
				return line.Contains ("Button was Clicked!");
			}, Path.Combine (Root, builder.ProjectDirectory, "button-logcat.log")), "Button Should have been Clicked.");
		}

		private const int NODE_COUNT = 3;

		static object [] GetTimeZoneTestCases (int node)
		{
			List<object> tests = new List<object> ();
			var ignore = new string [] {
				"Asia/Qostanay",
				"US/Pacific-New"
			};

			foreach (var tz in NodaTime.DateTimeZoneProviders.Tzdb.Ids) {
				if (ignore.Contains (tz))
					continue;
				tests.Add (new object [] {
					tz,
				});
			}
			return tests.Where (p => tests.IndexOf (p) % NODE_COUNT == node).ToArray ();
		}

		[Test]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 0 })]
		[Category ("TimeZoneInfo")]
		public void CheckTimeZoneInfoIsCorrectNode1 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 1 })]
		[Category ("TimeZoneInfo")]
		public void CheckTimeZoneInfoIsCorrectNode2 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		[Test]
		[TestCaseSource (nameof (GetTimeZoneTestCases), new object [] { 2 })]
		[Category ("TimeZoneInfo")]
		public void CheckTimeZoneInfoIsCorrectNode3 (string timeZone) => CheckTimeZoneInfoIsCorrect (timeZone);

		public void CheckTimeZoneInfoIsCorrect (string timeZone)
		{
			AssertHasDevices ();

			string currentTimeZone = RunAdbCommand ("shell getprop persist.sys.timezone")?.Trim ();
			try {
				RunAdbCommand ($"shell su root setprop persist.sys.timezone \"{timeZone}\"");
				ClearAdbLogcat ();
				AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");
				Assert.IsTrue (WaitForActivityToStart (proj.PackageName, "MainActivity",
					Path.Combine (Root, builder.ProjectDirectory, $"startup-logcat-{timeZone.Replace ("/", "-")}.log")), "Activity should have started");
				Assert.IsTrue (MonitorAdbLogcat ((l) => {
					return l.Contains ($"TimeZoneInfo={timeZone}");
				}, Path.Combine (Root, builder.ProjectDirectory, $"timezone-logcat-{timeZone.Replace ("/", "-")}.log")), $"TimeZone should have been {timeZone}");
			} finally {
				if (!string.IsNullOrEmpty (currentTimeZone))
					RunAdbCommand ($"shell su root setprop persist.sys.timezone \"{currentTimeZone}\"");
			}
		}
	}
}
