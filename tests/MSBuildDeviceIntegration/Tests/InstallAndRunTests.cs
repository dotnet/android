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
				MinSdkVersion = "23",
				TargetSdkVersion = null,
			};
			if (isRelease || !CommercialBuildAvailable) {
				proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86");
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
		[Category ("DotNetIgnore")] // TODO: UnhandledException not firing: https://github.com/dotnet/runtime/issues/44526
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

		Regex ObfuscatedStackRegex = new Regex ("in <.*>:0", RegexOptions.Compiled);

		void SymbolicateAndAssert (string symbolArchivePath, string logcatFilePath, IEnumerable<string> expectedStackTraceContents)
		{
			// 09-22 14:21:07.064 12786 12786 I MonoDroid:   at UnnamedProject.MainActivity.OnCreate (Android.OS.Bundle bundle) [0x00051] in <b3164619c4824e379aecfb7335bd4cce>:0
			Assert.IsTrue (ObfuscatedStackRegex.IsMatch (File.ReadAllText (logcatFilePath)), "Original logcat output did not contain obfuscated crash info.");
			var monoSymbolicate = IsWindows ? Path.Combine (TestEnvironment.MonoAndroidToolsDirectory, "mono-symbolicate.exe") : "mono-symbolicate";
			var symbolicatedOutput = RunProcess (monoSymbolicate, $"\"{symbolArchivePath}\" \"{logcatFilePath}\"");
			File.WriteAllText (Path.Combine (Path.GetDirectoryName (logcatFilePath), "mono-symbol.log"), symbolicatedOutput);
			Assert.IsFalse (ObfuscatedStackRegex.IsMatch (symbolicatedOutput), "Symbolicated logcat output did contain obfuscated crash info.");
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
					? Path.Combine ("src", "Mono.Android", "obj", XABuildPaths.Configuration, "monoandroid10", $"android-{apiLevel}", "mcw", "Android.App.Activity.cs:")
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
						? Path.Combine ("src", "Mono.Android", "obj", XABuildPaths.Configuration, "monoandroid10", $"android-{apiLevel}", "mcw", "Android.App.Activity.cs:")
						: $"src/Mono.Android/obj/Release/monoandroid10/android-{apiLevel}/mcw/Android.App.Activity.cs:",
				});
			}
		}

		public static string [] ProfilerOptions () => new string [] {
			"log:heapshot", // Heapshot
			"log:sample", // Sample
			"log:nodefaults,exception,monitor,counter,sample", // Sample5_8
			"log:nodefaults,exception,monitor,counter,sample-real", // SampleReal
			"log:alloc", // Allocations
			"log:nodefaults,gc,gcalloc,gcroot,gcmove,counter", // Allocations5_8
			"log:nodefaults,gc,nogcalloc,gcroot,gcmove,counter", // LightAllocations
			"log:calls,alloc,heapshot", // All
		};

		[Test]
		[Category ("DotNetIgnore")] // TODO: libmono-profiler-log.so is missing in .NET 6
		public void ProfilerLogOptions_ShouldCreateMlpdFiles ([ValueSource (nameof (ProfilerOptions))] string profilerOption)
		{
			AssertHasDevices ();
			AssertCommercialBuild ();

			proj = new XamarinAndroidApplicationProject ();
			builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
			string mlpdDestination = Path.Combine (Root, builder.ProjectDirectory, "profile.mlpd");
			if (File.Exists (mlpdDestination))
				File.Delete (mlpdDestination);

			RunAdbCommand ($"shell setprop debug.mono.profile {profilerOption}");
			Assert.True (builder.RunTarget (proj, "_Run"), "Project should have run.");
			Assert.True (WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30), "Activity should have started.");

			// Wait for seven seconds after the activity is displayed to get profiler results
			WaitFor (7000);
			string profilerFileDir = null;
			foreach (var dir in GetOverrideDirectoryPaths (proj.PackageName)) {
				var listing = RunAdbCommand ($"shell run-as {proj.PackageName} ls {dir}");
				if (listing.Contains ("profile.mlpd")) {
					profilerFileDir = dir;
					break;
				}
			}

			Assert.IsTrue (!string.IsNullOrEmpty (profilerFileDir), $"Unable to locate 'profile.mlpd' in any override directories.");
			var profilerContent = RunAdbCommand ($"shell run-as {proj.PackageName} cat {profilerFileDir}/profile.mlpd");
			File.WriteAllText (mlpdDestination, profilerContent);
			RunAdbCommand ($"shell run-as {proj.PackageName} rm {profilerFileDir}/profile.mlpd");
			RunAdbCommand ($"shell am force-stop {proj.PackageName}");
			RunAdbCommand ("shell setprop debug.mono.profile \"\"");
			Assert.IsTrue (new FileInfo (mlpdDestination).Length > 5000,
				$"profile.mlpd file created with option '{profilerOption}' was not larger than 5 kb. The application may have crashed.");
			Assert.IsTrue (profilerContent.Contains ("String") && profilerContent.Contains ("Java"),
				$"profile.mlpd file created with option '{profilerOption}' did not contain expected data.");
		}

		[Test]
		public void CustomLinkDescriptionPreserve ([Values (AndroidLinkMode.SdkOnly, AndroidLinkMode.Full)] AndroidLinkMode linkMode)
		{
			AssertHasDevices ();

			var lib1 = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
				Sources = {
					new BuildItem.Source ("SomeClass.cs") {
						TextContent = () => "namespace Library1 { public class SomeClass { } }"
					},
					new BuildItem.Source ("NonPreserved.cs") {
						TextContent = () => "namespace Library1 { public class NonPreserved { } }"
					},
					new BuildItem.Source ("LinkerClass.cs") {
						TextContent = () => @"
namespace Library1 {
	public class LinkerClass {
		public LinkerClass () { }

		public bool IsPreserved { get { return true; } }

		public bool ThisMethodShouldBePreserved () { return true; }

		public void WasThisMethodPreserved (string arg1) { }

		[Android.Runtime.Preserve]
		public void PreserveAttribMethod () { }
	}
}",
					}, new BuildItem.Source ("LinkModeFullClass.cs") {
						TextContent = () => @"
namespace Library1 {
	public class LinkModeFullClass {
		public bool ThisMethodShouldNotBePreserved () { return true; }
	}
}",
					},
				}
			};

			var lib2 = new DotNetStandard {
				ProjectName = "LinkTestLib",
				Sdk = "Microsoft.NET.Sdk",
				TargetFramework = "netstandard2.0",
				PackageReferences = {
					new Package {
						Id = "sqlite-net-pcl",
						Version = "1.7.335",
					}
				},
				Sources = {
					new BuildItem.Source ("Bug21578.cs") {
						TextContent = () => {
							using (var sr = new StreamReader (typeof (InstallAndRunTests).Assembly.GetManifestResourceStream ("Xamarin.Android.Build.Tests.Resources.LinkDescTest.Bug21578.cs")))
								return sr.ReadToEnd ();
						},
					},
					new BuildItem.Source ("Bug35195.cs") {
						TextContent = () => {
							using (var sr = new StreamReader (typeof (InstallAndRunTests).Assembly.GetManifestResourceStream ("Xamarin.Android.Build.Tests.Resources.LinkDescTest.Bug35195.cs")))
								return sr.ReadToEnd ();
						},
					},
				},
			};

			if (!Builder.UseDotNet) {
				// DataContractSerializer is not trimming safe
				// https://github.com/dotnet/runtime/issues/45559
				lib2.Sources.Add (new BuildItem.Source ("Bug36250.cs") {
					TextContent = () => {
						using (var sr = new StreamReader (typeof (InstallAndRunTests).Assembly.GetManifestResourceStream ("Xamarin.Android.Build.Tests.Resources.LinkDescTest.Bug36250.cs")))
							return sr.ReadToEnd ();
					},
				});
			}

			proj = new XamarinFormsAndroidApplicationProject () {
				IsRelease = true,
				AndroidLinkModeRelease = linkMode,
				References = {
					new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj"),
					new BuildItem ("ProjectReference", "..\\LinkTestLib\\LinkTestLib.csproj"),
				},
				OtherBuildItems = {
					new BuildItem ("LinkDescription", "linker.xml") {
						TextContent = () => linkMode == AndroidLinkMode.SdkOnly ? "<linker/>" : @"
<linker>
  <assembly fullname=""Library1"">
    <type fullname=""Library1.LinkerClass"">
      <method name="".ctor"" />
      <method name=""WasThisMethodPreserved"" />
      <method name=""get_IsPreserved"" />
    </type>
  </assembly>
  <assembly fullname=""LinkTestLib"">
    <type fullname=""LinkTestLib.TodoTask"" />
  </assembly>
</linker>
",
					},
				},
			};
			if (Builder.UseDotNet) {
				// NOTE: workaround for netcoreapp3.1 dependency preferred over monoandroid8.0
				proj.PackageReferences.Add (new Package {
					Id = "SQLitePCLRaw.lib.e_sqlite3.android",
					Version = "2.0.4",
				});
			}

			proj.AndroidManifest = proj.AndroidManifest.Replace ("</manifest>", "<uses-permission android:name=\"android.permission.INTERNET\" /></manifest>");
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86", "x86_64");
			using (var sr = new StreamReader (typeof (InstallAndRunTests).Assembly.GetManifestResourceStream ("Xamarin.Android.Build.Tests.Resources.LinkDescTest.MainActivityReplacement.cs")))
				proj.MainActivity = sr.ReadToEnd ();

			// Set up library projects
			var rootPath = Path.Combine (Root, "temp", TestName);
			using (var lb1 = CreateDllBuilder (Path.Combine (rootPath, lib1.ProjectName)))
				Assert.IsTrue (lb1.Build (lib1), "First library build should have succeeded.");
			using (var lb2 = CreateDllBuilder (Path.Combine (rootPath, lib2.ProjectName)))
				Assert.IsTrue (lb2.Build (lib2), "Second library build should have succeeded.");

			builder = CreateApkBuilder (Path.Combine (rootPath, proj.ProjectName));
			Assert.IsTrue (builder.Install (proj), "First install should have succeeded.");

			ClearAdbLogcat ();
			if (CommercialBuildAvailable)
				Assert.True (builder.RunTarget (proj, "_Run"), "Project should have run.");
			else
				AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");

			var logcatPath = Path.Combine (Root, builder.ProjectDirectory, "logcat.log");
			Assert.IsTrue (MonitorAdbLogcat ((line) => {
				return line.Contains ("All regression tests completed.");
			}, logcatPath, 90), "Linker test app did not run successfully.");

			var logcatOutput = File.ReadAllText (logcatPath);
			StringAssert.Contains ("[PASS]", logcatOutput);
			StringAssert.DoesNotContain ("[FAIL]", logcatOutput);
			if (linkMode == AndroidLinkMode.Full) {
				StringAssert.Contains ("[LINKALLPASS]", logcatOutput);
				StringAssert.DoesNotContain ("[LINKALLFAIL]", logcatOutput);
			}
		}

	}
}
