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

		public bool ThisMethodShouldBePreserved ()
		{
			return true;
		}

		public void WasThisMethodPreserved (string arg1)
		{
		}

		[Android.Runtime.Preserve]
		public void PreserveAttribMethod ()
		{
		}
	}
}",
					}, new BuildItem.Source ("LinkModeFullClass.cs") {
						TextContent = () => @"
namespace Library1 {
	public class LinkModeFullClass {
		public bool ThisMethodShouldNotBePreserved ()
		{
			return true;
		}
	}
}",
					},
				}
			};

			var lib2 = new DotNetStandard {
				ProjectName = "Library2",
				Sdk = "Microsoft.NET.Sdk",
				TargetFramework = "netstandard2.0",
				Sources = {
					new BuildItem.Source ("Bug21578.cs") {
						TextContent = () => @"
using System;
using System.Net;
using System.Net.Sockets;
namespace Library2 {
	// https://bugzilla.xamarin.com/show_bug.cgi?id=21578
	// https://bugzilla.xamarin.com/show_bug.cgi?id=22183
	public static class Bug21578 {
		public static string MulticastOption_ShouldNotBeStripped ()
		{
			try {
				using (var client = new UdpClient ()) {
					var multicastAddress = IPAddress.Parse (""224.0.0.251"");
					const int ifaceIndex = 0;
					var multOpt = new MulticastOption (multicastAddress, ifaceIndex);
					client.Client.SetSocketOption (SocketOptionLevel.IP, SocketOptionName.AddMembership, multOpt);
					return ""[PASS] SetSocketOption was not stripped."";
				}
			} catch (Exception ex) {
				if (ex is SocketException && ex.Message.Contains(""Network subsystem is down"")) {
					return ""[IGNORE] SetSocketOption test was inconclusive."";
				}
				return $""[FAIL] SetSocketOption was stripped!\n{ex}"";
			}
		}

		public static string MulticastOption_ShouldNotBeStripped2 ()
		{
			try {
				using (var clientWriter = new UdpClient ()) {
					var multicastAddress = IPAddress.Parse (""224.0.0.224"");
					clientWriter.JoinMulticastGroup (multicastAddress);
					return ""[PASS] JoinMulticastGroup was not stripped"";
				}
			} catch (Exception ex) {
				if (ex is SocketException && ex.Message.Contains (""Network subsystem is down"")) {
					return ""[IGNORE] MulticastGroup test was inconclusive."";
				}
				return $""[FAIL] SetSocketOption was stripped!\n{ex}"";
			}
		}
	}
}",
					},
					new BuildItem.Source ("Bug35195.cs") {
						TextContent = () => @"
using System;
using System.IO;
using SQLite;
namespace Library2 {
	// https://bugzilla.xamarin.com/show_bug.cgi?id=35195
	public static class Bug35195 {
		public static string AttemptCreateTable ()
		{
			try {
				// Initialize the database name.
				const string sqliteFilename = ""TaskDB.db3"";
				string libraryPath = Environment.GetFolderPath (Environment.SpecialFolder.Personal);
				string path = Path.Combine (libraryPath, sqliteFilename);
				var db = new SQLiteAsyncConnection (path);
				db.CreateTableAsync<TodoTask> ().GetAwaiter ().GetResult ();
				return ""[PASS] Create table attempt did not throw"";
			} catch (Exception ex) {
				return $""[FAIL] Create table attempt failed!\n{ex}"";
			}
		}
	}

	public class TodoTask {
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }
		public string Name { get; set; }
		public string Notes { get; set; }
		public bool Done { get; set; }
	}
}",
					},
					new BuildItem.Source ("Bug36250.cs") {
						TextContent = () => @"
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization;
namespace Library2 {
	// https://bugzilla.xamarin.com/show_bug.cgi?id=36250
	public class Bug36250
	{
		// [Test]
		public static string SerializeSearchRequestWithDictionary ()
		{
			var req = new SearchRequest () {
				Query = ""query"",

				Users = new List<string> () {
					""user_a"", ""user_b""
				},

				Filters = new List<string> () {
					""filter_a"", ""filter_b""
				},

				Parameters = new Dictionary<string, string> () {
					{ ""param_key_b"", ""param_value_a"" },
					{ ""param_key_a"", ""param_value_b"" },
				}
			};

			try {
				using (MemoryStream memoryStream = new MemoryStream ()) {
					var dataContractSerializer = new DataContractSerializer (typeof (SearchRequest));
					dataContractSerializer.WriteObject (memoryStream, req);
					string serializedDataContract = Encoding.UTF8.GetString (memoryStream.ToArray (), 0, (int) memoryStream.Length);
					return $""[PASS] SearchRequest successfully serialized: {serializedDataContract.Substring (0, 14)}"";
				}
			} catch (Exception ex) {
				return $""[FAIL] SearchRequest serialization FAILED: {ex}"";
			}
		}
	}

	[DataContract]
	public class SearchRequest {
		[DataMember]
		public string Query { get; set; }
		[DataMember]
		public List<string> Users { get; set; }
		[DataMember]
		public List<string> Filters { get; set; }
		[DataMember]
		public Dictionary<string, string> Parameters { get; set; }
	}
}",
					},
				},
				PackageReferences = {
					new Package {
						Id = "sqlite-net-pcl",
						Version = "1.7.335",
					},
				},
			};

			proj = new XamarinFormsAndroidApplicationProject () {
				IsRelease = true,
				AndroidLinkModeRelease = linkMode,
				References = {
					new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj"),
					new BuildItem ("ProjectReference", "..\\Library2\\Library2.csproj"),
				},
				Sources = {
					new BuildItem.Source ("CustomLinkerDescriptionTests.cs") {
						TextContent = () => @"
using System;
namespace UnnamedProject {
	public class CustomLinkerDescriptionTests {
		Type t = typeof(Library1.LinkModeFullClass);
		public string TryAccessNonXmlPreservedMethodOfLinkerModeFullClass()
		{
			try
			{
				var m = t.GetMethod(""ThisMethodShouldNotBePreserved"");
				return $""[LINKALLFAIL] Able to locate method that should have been linked: '{m.Name}'"";
			}
			catch (NullReferenceException ex)
			{
				return $""[LINKALLPASS] Was unable to access 'ThisMethodShouldNotBePreserved ()' method of 'LinkerClass' as expected.\n{ex}"";
			}
		}
	}
}",
					},
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
  <assembly fullname=""Library2"">
    <type fullname=""Library2.TodoTask"" />
  </assembly>
</linker>
",
					},
				},
			};
			proj.AndroidManifest = proj.AndroidManifest.Replace ("</manifest>", "<uses-permission android:name=\"android.permission.INTERNET\" /></manifest>");
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86", "x86_64");
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", @"
			string TAG = ""XALINKERTESTS"";
			// [Test] TryCreateInstanceOfSomeClass
			try {
				var asm = typeof (Library1.SomeClass).Assembly;
				var o = Activator.CreateInstance (asm.GetType (""Library1.SomeClass""));
				Android.Util.Log.Info (TAG, $""[PASS] Able to create instance of '{o.GetType ().Name}'."");
			} catch (Exception ex) {
				Android.Util.Log.Info (TAG, $""[FAIL] Unable to create instance of 'SomeClass'.\n{ex}"");
			}

			// [Test] TryCreateInstanceOfXmlPreservedLinkerClass
			try {
				var asm = typeof (Library1.SomeClass).Assembly;
				var o = Activator.CreateInstance (asm.GetType (""Library1.LinkerClass""));
				Android.Util.Log.Info (TAG, $""[PASS] Able to create instance of '{o.GetType ().Name}'."");
			} catch (Exception ex) {
				Android.Util.Log.Info (TAG, $""[FAIL] Unable to create instance of 'LinkerClass'.\n{ex}"");
			}

			// [Test] TryAccessXmlPreservedMethodOfLinkerClass
			try {
				var asm = typeof (Library1.SomeClass).Assembly;
				var t = asm.GetType (""Library1.LinkerClass"");
				var m = t.GetMethod (""WasThisMethodPreserved"");
				Android.Util.Log.Info (TAG, $""[PASS] Able to locate method '{m.Name}'."");
			} catch (Exception ex) {
				Android.Util.Log.Info (TAG, $""[FAIL] Unable to access 'WasThisMethodPreserved ()' method of 'LinkerClass'.\n{ex}"");
			}

			// [Test] TryAccessAttributePreservedMethodOfLinkerClass
			try {
				var asm = typeof (Library1.SomeClass).Assembly;
				var t = asm.GetType (""Library1.LinkerClass"");
				var m = t.GetMethod (""PreserveAttribMethod"");
				Android.Util.Log.Info (TAG, $""[PASS] Able to locate method '{m.Name}'."");
			} catch (Exception ex) {
				Android.Util.Log.Info (TAG, $""[FAIL] Unable to access 'PreserveAttribMethod ()' method of 'LinkerClass'.\n{ex}"");
			}

			// [Test] TryAccessXmlPreservedFieldOfLinkerClass
			try {
				var asm = typeof (Library1.SomeClass).Assembly;
				var t = asm.GetType (""Library1.LinkerClass"");
				var m = t.GetProperty (""IsPreserved"");
				Android.Util.Log.Info (TAG, $""[PASS] Able to locate field '{m.Name}'."");
			} catch (Exception ex) {
				Android.Util.Log.Info (TAG, $""[FAIL] Unable to access 'IsPreserved' field of 'LinkerClass'.\n{ex}"");
			}

			// [Test] TryCreateInstanceOfNonXmlPreservedClass
			try
			{
				var asm = typeof (Library1.SomeClass).Assembly;
				var o = Activator.CreateInstance (asm.GetType (""Library1.NonPreserved""));
				Android.Util.Log.Info (TAG, $""[LINKALLFAIL] Able to create instance of '{o.GetType ().Name}' which should have been linked away."");
			} catch (Exception ex) {
				Android.Util.Log.Info (TAG, $""[LINKALLPASS] Unable to create instance of 'NonPreserved' as expected.\n{ex}"");
			}

			// [Test] TryAccessNonXmlPreservedMethodOfLinkerModeFullClass
			var newLinkerTestInstance = new CustomLinkerDescriptionTests ();
			Android.Util.Log.Info (TAG, newLinkerTestInstance.TryAccessNonXmlPreservedMethodOfLinkerModeFullClass ());

			Android.Util.Log.Info (TAG, Library2.Bug21578.MulticastOption_ShouldNotBeStripped ());
			Android.Util.Log.Info (TAG, Library2.Bug21578.MulticastOption_ShouldNotBeStripped2 ());
			Android.Util.Log.Info (TAG, Library2.Bug35195.AttemptCreateTable ());
			Android.Util.Log.Info (TAG, Library2.Bug36250.SerializeSearchRequestWithDictionary ());

			Android.Util.Log.Info (TAG, ""All regression tests completed."");
");

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
