using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[SingleThreaded]
	[Category ("UsesDevices")]
	public class InstallAndRunTests : DeviceTest
	{
		static ProjectBuilder builder;
		static XamarinAndroidApplicationProject proj;

		[TearDown]
		public void Teardown ()
		{
			if (HasDevices && proj != null)
				RunAdbCommand ($"uninstall {proj.PackageName}");

			if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Passed
				&& builder != null && Directory.Exists (builder.ProjectDirectory))
				Directory.Delete (builder.ProjectDirectory, recursive: true);

			builder?.Dispose ();
			proj = null;
		}

		[Test]
		public void GlobalLayoutEvent_ShouldRegisterAndFire_OnActivityLaunch ([Values (false, true)] bool isRelease)
		{
			AssertHasDevices ();

			string expectedLogcatOutput = "Bug 29730: GlobalLayout event handler called!";

			proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			if (isRelease || !CommercialBuildAvailable) {
				proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86");
			} else {
				proj.MinSdkVersion = "23";
				proj.TargetSdkVersion = null;
			}
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}",
$@"button.ViewTreeObserver.GlobalLayout += Button_ViewTreeObserver_GlobalLayout;
		}}
		void Button_ViewTreeObserver_GlobalLayout (object sender, EventArgs e)
		{{
			Android.Util.Log.Debug (""BugzillaTests"", ""{expectedLogcatOutput}"");
");
			builder = CreateApkBuilder (Path.Combine ("temp", $"Bug29730-{isRelease}"));
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
			ClearAdbLogcat ();
			AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");
			Assert.IsTrue (MonitorAdbLogcat ((line) => {
				return line.Contains (expectedLogcatOutput);
			}, Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"), 45), $"Output did not contain {expectedLogcatOutput}!");
		}

		[Test]
		public void SubscribeToAppDomainUnhandledException ()
		{
			AssertHasDevices ();

			proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86", "x86_64");
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}",
@"			AppDomain.CurrentDomain.UnhandledException += (sender, e) => {
				Console.WriteLine (""# Unhandled Exception: sender={0}; e.IsTerminating={1}; e.ExceptionObject={2}"",
					sender, e.IsTerminating, e.ExceptionObject);
			};
			throw new Exception (""CRASH"");
");
			builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
			ClearAdbLogcat ();
			if (CommercialBuildAvailable)
				Assert.True (builder.RunTarget (proj, "_Run"), "Project should have run.");
			else
				AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");

			string expectedLogcatOutput = "# Unhandled Exception: sender=RootDomain; e.IsTerminating=True; e.ExceptionObject=System.Exception: CRASH";
			Assert.IsTrue (MonitorAdbLogcat ((line) => {
				return line.Contains (expectedLogcatOutput);
			}, Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"), 45), $"Output did not contain {expectedLogcatOutput}!");
		}


		void SymbolicateAndAssert (string symbolArchivePath, string logcatFilePath, IEnumerable<string> expectedStackTraceContents)
		{
			// 09-22 14:21:07.064 12786 12786 I MonoDroid:   at UnnamedProject.MainActivity.OnCreate (Android.OS.Bundle bundle) [0x00051] in <b3164619c4824e379aecfb7335bd4cce>:0
			var obfuscatedStackRegex = new Regex ("in <.*>:0");
			Assert.IsTrue (obfuscatedStackRegex.IsMatch (File.ReadAllText (logcatFilePath)), "Original logcat output did not contain obfuscated crash info.");
			var monoSymbolicate = IsWindows ? Path.Combine (TestEnvironment.MonoAndroidToolsDirectory, "mono-symbolicate.exe") : "mono-symbolicate";
			var symbolicatedOutput = RunProcess (monoSymbolicate, $"\"{symbolArchivePath}\" \"{logcatFilePath}\"");
			File.WriteAllText (Path.Combine (Path.GetDirectoryName (logcatFilePath), "mono-symbol.log"), symbolicatedOutput);
			Assert.IsFalse (obfuscatedStackRegex.IsMatch (symbolicatedOutput), "Symbolicated logcat output did contain obfuscated crash info.");
			foreach (string expectedString in expectedStackTraceContents) {
				StringAssert.Contains (expectedString, symbolicatedOutput);
			}
		}

		[Test, Category ("MonoSymbolicate")]
		public void MonoSymbolicateAndroidStackTrace ()
		{
			AssertHasDevices ();

			proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86", "x86_64");
			proj.SetProperty (proj.ReleaseProperties, "MonoSymbolArchive", "True");
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}",
@"			throw new Android.OS.RemoteException (""We've thrown an unhandled Android.OS.RemoteException!"");
");
			builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
			var archivePath = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}.apk.mSYM");
			Assert.IsTrue (Directory.Exists (archivePath), $"Symbol archive path {archivePath} should exist.");

			ClearAdbLogcat ();
			if (CommercialBuildAvailable)
				Assert.True (builder.RunTarget (proj, "_Run"), "Project should have run.");
			else
				AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");

			var logcatPath = Path.Combine (Root, builder.ProjectDirectory, "crash-logcat.log");
			MonitorAdbLogcat ((line) => {
				return line.Contains ($"Force finishing activity {proj.PackageName}");
			}, logcatPath, 30);

			var didParse = int.TryParse (proj.TargetSdkVersion, out int apiLevel);
			Assert.IsTrue (didParse, $"Unable to parse {proj.TargetSdkVersion} as an int.");
			SymbolicateAndAssert (archivePath, logcatPath, new string [] {
				Path.Combine (Root, builder.ProjectDirectory, "MainActivity.cs:32"),
				Directory.Exists (builder.BuildOutputDirectory)
					? Path.Combine ("src","Mono.Android", "obj", XABuildPaths.Configuration,"monoandroid10", $"android-{apiLevel}", "mcw", "Android.App.Activity.cs:")
					: $"src/Mono.Android/obj/Release/monoandroid10/android-{apiLevel}/mcw/Android.App.Activity.cs:",
			}) ;
		}

		[Test, Category ("MonoSymbolicate")]
		public void MonoSymbolicateNetStandardStackTrace ()
		{
			AssertHasDevices ();

			var lib = new DotNetStandard {
				ProjectName = "Library1",
				Sdk = "Microsoft.NET.Sdk",
				TargetFramework = "netstandard2.0",
				Sources = {
					new BuildItem.Source ("Class1.cs") {
						TextContent = () => @"
using System;
namespace Library1 {
	public class Class1 {
		string Data { get; set; }
		public Class1(string data) {
			Data = data;
		}

		public string GetData() {
			if (Data == null)
				throw new NullReferenceException();
			return Data;
		}
	}
}",
					},
				}
			};

			proj = new XamarinFormsAndroidApplicationProject () {
				IsRelease = true,
				References = {
					new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj"),
				},
			};
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86", "x86_64");
			proj.SetProperty (proj.ReleaseProperties, "MonoSymbolArchive", "True");
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}",
@"			var cl = new Library1.Class1(null);
			cl.GetData();
");
			var rootPath = Path.Combine (Root, "temp", TestName);
			using (var lb = CreateDllBuilder (Path.Combine (Path.Combine (Root, "temp", TestName), lib.ProjectName))) {
				Assert.IsTrue (lb.Build (lib), "Library build should have succeeded.");

				builder = CreateApkBuilder (Path.Combine (rootPath, proj.ProjectName));
				Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
				var archivePath = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}.apk.mSYM");
				Assert.IsTrue (Directory.Exists (archivePath), $"Symbol archive path {archivePath} should exist.");

				ClearAdbLogcat ();
				if (CommercialBuildAvailable)
					Assert.True (builder.RunTarget (proj, "_Run"), "Project should have run.");
				else
					AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");

				var logcatPath = Path.Combine (Root, builder.ProjectDirectory, "crash-logcat.log");
				MonitorAdbLogcat ((line) => {
					return line.Contains ($"Force finishing activity {proj.PackageName}");
				}, logcatPath, 30);

				var didParse = int.TryParse (proj.TargetSdkVersion, out int apiLevel);
				Assert.IsTrue (didParse, $"Unable to parse {proj.TargetSdkVersion} as an int.");
				SymbolicateAndAssert (archivePath, logcatPath, new string [] {
					Path.Combine (Root, lb.ProjectDirectory, "Class1.cs:12"),
					Path.Combine (Root, builder.ProjectDirectory, "MainActivity.cs:33"),
					Directory.Exists (builder.BuildOutputDirectory)
						? Path.Combine("src","Mono.Android", "obj", XABuildPaths.Configuration,"monoandroid10", $"android-{apiLevel}", "mcw", "Android.App.Activity.cs:")
						: $"src/Mono.Android/obj/Release/monoandroid10/android-{apiLevel}/mcw/Android.App.Activity.cs:",
				});
			}
		}

	}
}
