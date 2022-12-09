using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice"), Category ("Node-2")]
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
				proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86", "x86_64");
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
			}, Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"), 60), $"Output did not contain {expectedLogcatOutput}!");
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

#if NETCOREAPP
			string expectedLogcatOutput = "# Unhandled Exception: sender=System.Object; e.IsTerminating=True; e.ExceptionObject=System.Exception: CRASH";
#else   // NETCOREAPP
			string expectedLogcatOutput = "# Unhandled Exception: sender=RootDomain; e.IsTerminating=True; e.ExceptionObject=System.Exception: CRASH";
#endif  // NETCOREAPP
			Assert.IsTrue (
				MonitorAdbLogcat (CreateLineChecker (expectedLogcatOutput),
					logcatFilePath: Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"), timeout: 60),
				$"Output did not contain {expectedLogcatOutput}!");
		}


		public static Func<string, bool> CreateLineChecker (string expectedLogcatOutput)
		{
			// On .NET 6, `adb logcat` output may be line-wrapped in unexpected ways.
			// https://github.com/xamarin/xamarin-android/pull/6119#issuecomment-896246633
			// Try to see if *successive* lines match expected output
			var remaining   = expectedLogcatOutput;
			return line => {
				if (line.IndexOf (remaining, StringComparison.Ordinal) >= 0) {
					Reset ();
					return true;
				}
				int count   = Math.Min (line.Length, remaining.Length);
				for ( ; count > 0; count--) {
					var startMatch = remaining.Substring (0, count);
					if (line.IndexOf (startMatch, StringComparison.Ordinal) >= 0) {
						remaining = remaining.Substring (count);
						return false;
					}
				}
				Reset ();
				return false;
			};

			void Reset ()
			{
				remaining   = expectedLogcatOutput;
			}
		}

		Regex ObfuscatedStackRegex = new Regex ("in <.*>:0", RegexOptions.Compiled);

		void SymbolicateAndAssert (string symbolArchivePath, string logcatFilePath, IEnumerable<string> expectedStackTraceContents)
		{
			// 09-22 14:21:07.064 12786 12786 I MonoDroid:   at UnnamedProject.MainActivity.OnCreate (Android.OS.Bundle bundle) [0x00051] in <b3164619c4824e379aecfb7335bd4cce>:0
			Assert.IsTrue (ObfuscatedStackRegex.IsMatch (File.ReadAllText (logcatFilePath)), "Original logcat output did not contain obfuscated crash info.");
			var monoSymbolicate = IsWindows ? Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "mono-symbolicate.exe") : "mono-symbolicate";
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
				TestEnvironment.UseLocalBuildOutput
					? Path.Combine ("src", "Mono.Android", "obj", XABuildPaths.Configuration, "monoandroid10", $"android-{apiLevel}", "mcw", "Android.App.Activity.cs:")
					: $"src/Mono.Android/obj/Release/monoandroid10/android-{apiLevel}/mcw/Android.App.Activity.cs:",
			}) ;
		}

		[Test]
		[Category ("UsesDevice"), Category ("SmokeTests")]
		public void SmokeTestBuildAndRunWithSpecialCharacters ()
		{
			AssertHasDevices ();
			var testName = "テスト";

			var rootPath = Path.Combine (Root, "temp", TestName);
			var proj = new XamarinFormsAndroidApplicationProject () {
				ProjectName = testName,
				IsRelease = true,
			};
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86", "x86_64");
			proj.SetDefaultTargetDevice ();
			using (var builder = CreateApkBuilder (Path.Combine (rootPath, proj.ProjectName))){
				Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
				Assert.IsTrue (builder.RunTarget (proj, "_Run", doNotCleanupOnUpdate: true), "Project should have run.");
				var timeoutInSeconds = 120;
				Assert.IsTrue (WaitForActivityToStart (proj.PackageName, "MainActivity",
					Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"), timeoutInSeconds));
			}
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
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_FORMS_INIT}",
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
					Path.Combine (Root, builder.ProjectDirectory, "MainActivity.cs:23"),
					TestEnvironment.UseLocalBuildOutput
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

			proj = new XamarinAndroidApplicationProject () {
			};
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
					new BuildItem.Source ("HttpClientTest.cs") {
						TextContent = () => {
							using (var sr = new StreamReader (typeof (InstallAndRunTests).Assembly.GetManifestResourceStream ("Xamarin.Android.Build.Tests.Resources.LinkDescTest.HttpClientTest.cs")))
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
				PackageReferences = {
					KnownPackages.AndroidXMigration,
					KnownPackages.AndroidXAppCompat,
					KnownPackages.AndroidXAppCompatResources,
					KnownPackages.AndroidXBrowser,
					KnownPackages.AndroidXMediaRouter,
					KnownPackages.AndroidXLegacySupportV4,
					KnownPackages.AndroidXLifecycleLiveData,
					KnownPackages.XamarinGoogleAndroidMaterial,
				},
				Sources = {
					new BuildItem.Source ("MaterialTextChanged.cs") {
						TextContent = () => {
							using (var sr = new StreamReader (typeof (InstallAndRunTests).Assembly.GetManifestResourceStream ("Xamarin.Android.Build.Tests.Resources.LinkDescTest.MaterialTextChanged.cs")))
								return sr.ReadToEnd ();
						},
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
  <assembly fullname=""LinkTestLib"">
    <type fullname=""LinkTestLib.TodoTask"" />
  </assembly>
</linker>
",
					},
				},
			};
			if (Builder.UseDotNet) {
				// NOTE: workaround for netcoreapp3.0 dependency being included along with monoandroid8.0
				// See: https://www.nuget.org/packages/SQLitePCLRaw.bundle_green/2.0.3
				proj.PackageReferences.Add (new Package {
					Id = "SQLitePCLRaw.provider.dynamic_cdecl",
					Version = "2.0.3",
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

		[Test]
		public void JsonDeserializationCreatesJavaHandle ([Values (false, true)] bool isRelease)
		{
			AssertHasDevices ();

			proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			// error SYSLIB0011: 'BinaryFormatter.Serialize(Stream, object)' is obsolete: 'BinaryFormatter serialization is obsolete and should not be used. See https://aka.ms/binaryformatter for more information.'
			proj.SetProperty ("NoWarn", "SYSLIB0011");

			if (isRelease || !CommercialBuildAvailable) {
				proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86", "x86_64");
			}

			proj.References.Add (new BuildItem.Reference ("System.Runtime.Serialization"));

			if (Builder.UseDotNet)
				proj.References.Add (new BuildItem.Reference ("System.Runtime.Serialization.Json"));

			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}",
				@"TestJsonDeserializationCreatesJavaHandle();
		}

		void TestJsonDeserialization (Person p)
		{
			var stream      = new MemoryStream ();
			var serializer  = new DataContractJsonSerializer (typeof (Person));

			serializer.WriteObject (stream, p);

			stream.Position = 0;
			StreamReader sr = new StreamReader (stream);

			Console.WriteLine ($""JSON Person representation: {sr.ReadToEnd ()}"");

			stream.Position = 0;
			Person p2 = (Person) serializer.ReadObject (stream);

			Console.WriteLine ($""JSON Person parsed: Name '{p2.Name}' Age '{p2.Age}' Handle '0x{p2.Handle:X}'"");

			if (p2.Name != ""John Smith"")
				throw new InvalidOperationException (""JSON deserialization of Name"");
			if (p2.Age != 900)
				throw new InvalidOperationException (""JSON deserialization of Age"");
			if (p2.Handle == IntPtr.Zero)
				throw new InvalidOperationException (""Failed to instantiate new Java instance for Person!"");

			Console.WriteLine ($""JSON Person deserialized OK"");
		}

		void TestBinaryDeserialization (Person p)
		{
			var stream      = new MemoryStream ();
			var serializer  = new BinaryFormatter();

			serializer.Serialize (stream, p);

			stream.Position = 0;
			StreamReader sr = new StreamReader (stream);

			stream.Position = 0;
			serializer.Binder = new Person.Binder ();
			Person p2 = (Person) serializer.Deserialize (stream);

			Console.WriteLine ($""BinaryFormatter deserialzied: Name '{p2.Name}' Age '{p2.Age}' Handle '0x{p2.Handle:X}'"");

			if (p2.Name != ""John Smith"")
				throw new InvalidOperationException (""BinaryFormatter deserialization of Name"");
			if (p2.Age != 900)
				throw new InvalidOperationException (""BinaryFormatter deserialization of Age"");
			if (p2.Handle == IntPtr.Zero)
				throw new InvalidOperationException (""Failed to instantiate new Java instance for Person!"");

			Console.WriteLine ($""BinaryFormatter Person deserialized OK"");
		}

		void TestJsonDeserializationCreatesJavaHandle ()
		{
			Person p = new Person () {
				Name = ""John Smith"",
				Age = 900,
			};
#if !NET
			TestBinaryDeserialization (p);
#endif
			TestJsonDeserialization (p);").Replace ("//${AFTER_MAINACTIVITY}", @"
	[DataContract]
	[Serializable]
	class Person : Java.Lang.Object {
		[DataMember]
		public string Name;

		[DataMember]
		public int Age;

		internal sealed class Binder : SerializationBinder
		{
			public override Type BindToType (string assemblyName, string typeName)
			{
				if (typeName == ""Person"")
					return typeof (Person);

				return null;
			}
		}
	}");

			string usings =
@"using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization.Json;
";
			proj.MainActivity = usings + proj.MainActivity;

			builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
			ClearAdbLogcat ();
			AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");
			Assert.IsFalse (MonitorAdbLogcat ((line) => {
				return line.Contains ("TestJsonDeserializationCreatesJavaHandle");
			}, Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"), 60), $"Output did contain TestJsonDeserializationCreatesJavaHandle!");
		}

		[Test]
		public void RunWithInterpreterEnabled ([Values (false, true)] bool isRelease)
		{
			AssertHasDevices ();

			proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				AotAssemblies = false, // Release defaults to Profiled AOT for .NET 6
			};
			var abis = new string[] { "armeabi-v7a", "arm64-v8a", "x86", "x86_64" };
			proj.SetAndroidSupportedAbis (abis);
			proj.SetProperty (proj.CommonProperties, "UseInterpreter", "True");
			builder = CreateApkBuilder ();
			builder.BuildLogFile = "install.log";
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");

			if (!Builder.UseDotNet) {
				foreach (var abi in abis) {
					Assert.IsTrue (builder.LastBuildOutput.ContainsText (Path.Combine ($"interpreter-{abi}", "libmono-native.so")), $"interpreter-{abi}/libmono-native.so should be used.");
					Assert.IsTrue (builder.LastBuildOutput.ContainsText (Path.Combine ($"interpreter-{abi}", "libmonosgen-2.0.so")), $"interpreter-{abi}/libmonosgen-2.0.so should be used.");
				}
			}

			ClearAdbLogcat ();
			RunAdbCommand ("shell setprop debug.mono.log all");
			var logProp = RunAdbCommand ("shell getprop debug.mono.log")?.Trim ();
			Assert.AreEqual (logProp, "all", "The debug.mono.log prop was not set correctly.");

			builder.BuildLogFile = "run.log";
			if (CommercialBuildAvailable)
				Assert.True (builder.RunTarget (proj, "_Run"), "Project should have run.");
			else
				AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");

			Func<string, bool> checkForInterpMessage = line => {
				return line.Contains ("Enabling Mono Interpreter");
			};
			var timeoutInSeconds = 120;
			var didPrintInterpMessage = MonitorAdbLogcat (
				action: checkForInterpMessage,
				logcatFilePath: Path.Combine (Root, builder.ProjectDirectory, "interpreter-logcat.log"),
				timeout: timeoutInSeconds);
			var didStart = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"), timeoutInSeconds);
			RunAdbCommand ("shell setprop debug.mono.log \"''\"");
			logProp = RunAdbCommand ("shell getprop debug.mono.log")?.Trim ();
			Assert.AreEqual (logProp, string.Empty, "The debug.mono.log prop was not unset correctly.");
			Assert.IsTrue (didPrintInterpMessage, "logcat output did not contain 'Enabling Mono Interpreter'.");
			Assert.IsTrue (didStart, "Activity should have started.");
		}

		[Test]
		public void RunWithLLVMEnabled ()
		{
			AssertHasDevices ();

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86", "x86_64");
			proj.SetProperty ("EnableLLVM", true.ToString ());
			if (!Builder.UseDotNet) {
				proj.AotAssemblies = true;
			}

			builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");

			if (Builder.UseDotNet)
				Assert.True (builder.RunTarget (proj, "Run"), "Project should have run.");
			else if (CommercialBuildAvailable)
				Assert.True (builder.RunTarget (proj, "_Run"), "Project should have run.");
			else
				AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");

			Assert.IsTrue (WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log")));
		}

		[Test]
		public void ResourceDesignerWithNuGetReference ([Values ("net8.0-android33.0")] string dotnetTargetFramework)
		{
			AssertHasDevices ();

			string path = Path.Combine (Root, "temp", TestName);

			if (!Builder.UseDotNet) {
				Assert.Ignore ("Skipping. Test not relevant under Classic.");
			}
			// Build a NuGet Package
			var nuget = new XASdkProject (outputType: "Library") {
				Sdk = "Xamarin.Legacy.Sdk/0.2.0-alpha2",
				ProjectName = "Test.Nuget.Package",
				IsRelease = true,
			};
			nuget.AddNuGetSourcesForOlderTargetFrameworks ();
			nuget.Sources.Clear ();
			nuget.Sources.Add (new AndroidItem.AndroidResource ("Resources/values/Strings.xml") {
						TextContent = () => @"<resources>
    <string name='library_resouce_from_nuget'>Library Resource From Nuget</string>
</resources>",
			});
			nuget.SetProperty ("PackageName", "Test.Nuget.Package");
			var legacyTargetFrameworkVersion = "13.0";
			var legacyTargetFramework = $"monoandroid{legacyTargetFrameworkVersion}";
			nuget.SetProperty ("TargetFramework",  value: "");
			nuget.SetProperty ("TargetFrameworks", value: $"{dotnetTargetFramework};{legacyTargetFramework}");

			string directory = Path.Combine ("temp", TestName, "Test.Nuget.Package");
			var dotnet = CreateDotNetBuilder (nuget, directory);
			Assert.IsTrue (dotnet.Pack (), "`dotnet pack` should succeed");

			// Build an app which references it.
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86", "x86_64");
			proj.OtherBuildItems.Add (new BuildItem ("None", "NuGet.config") {
				TextContent = () => @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
	<add key='local' value='" + Path.Combine (Root, directory, "bin", "Release") + @"' />
  </packageSources>
</configuration>",
			});
			proj.PackageReferences.Add (new Package {
					Id = "Test.Nuget.Package",
					Version = "1.0.0",
				});
			builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName));
			Assert.IsTrue (builder.Install (proj, doNotCleanupOnUpdate: true), "Install should have succeeded.");
			string resource_designer = GetResourceDesignerPath (builder, proj);
			var contents = GetResourceDesignerText (proj, resource_designer);
			StringAssert.Contains ("public const int library_resouce_from_nuget =", contents);
		}

		[Test]
		public void SingleProject_ApplicationId ()
		{
			AssertHasDevices ();

			proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("ApplicationId", "com.i.should.get.overridden.by.the.manifest");

			var abis = new string [] { "armeabi-v7a", "arm64-v8a", "x86", "x86_64" };
			proj.SetAndroidSupportedAbis (abis);
			builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");

			if (Builder.UseDotNet)
				Assert.True (builder.RunTarget (proj, "Run"), "Project should have run.");
			else if (CommercialBuildAvailable)
				Assert.True (builder.RunTarget (proj, "_Run"), "Project should have run.");
			else
				AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");

			var didStart = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"));
			Assert.IsTrue (didStart, "Activity should have started.");
		}

		[Test]
		public void AppWithStyleableUsageRuns ([Values (true, false)] bool isRelease, [Values (true, false)] bool linkResources)
		{
			AssertHasDevices ();

			var rootPath = Path.Combine (Root, "temp", TestName);
			var lib = new XamarinAndroidLibraryProject () {
				ProjectName = "Styleable.Library"
			};

			lib.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\styleables.xml") {
				TextContent = () => @"<?xml version='1.0' encoding='utf-8'?>
<resources>
	<declare-styleable name='MyLibraryView'>
		<attr name='MyBool' format='boolean' />
		<attr name='MyInt' format='integer' />
	</declare-styleable>
</resources>",
			});
			lib.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\librarylayout.xml") {
				TextContent = () => @"<?xml version='1.0' encoding='utf-8'?>
<Styleable.Library.MyLibraryLayout xmlns:app='http://schemas.android.com/apk/res-auto' app:MyBool='true' app:MyInt='128'/>
",
			});
			lib.Sources.Add (new BuildItem.Source ("MyLibraryLayout.cs") {
				TextContent = () => @"using System;

namespace Styleable.Library {
	public class MyLibraryLayout : Android.Widget.LinearLayout
	{

		public MyLibraryLayout (Android.Content.Context context, Android.Util.IAttributeSet attrs) : base (context, attrs)
		{
			Android.Content.Res.TypedArray a = context.Theme.ObtainStyledAttributes (attrs, Resource.Styleable.MyLibraryView, 0,0);
			try {
					bool b = a.GetBoolean (Resource.Styleable.MyLibraryView_MyBool, defValue: false);
					if (!b)
						throw new Exception (""MyBool was not true."");
					int i = a.GetInteger (Resource.Styleable.MyLibraryView_MyInt, defValue: -1);
					if (i != 128)
						throw new Exception (""MyInt was not 128."");
			}
			finally {
				a.Recycle();
			}
		}
	}
}"
			});

			proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.AddReference (lib);

			proj.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\styleables.xml") {
				TextContent = () => @"<?xml version='1.0' encoding='utf-8'?>
<resources>
	<declare-styleable name='MyView'>
		<attr name='MyBool' format='boolean' />
		<attr name='MyInt' format='integer' />
	</declare-styleable>
</resources>",
			});
			proj.SetProperty ("AndroidLinkResources", linkResources ? "False" : "True");
			proj.LayoutMain = proj.LayoutMain.Replace ("<LinearLayout", "<UnnamedProject.MyLayout xmlns:app='http://schemas.android.com/apk/res-auto' app:MyBool='true' app:MyInt='128'")
				.Replace ("</LinearLayout>", "</UnnamedProject.MyLayout>");

			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_MAINACTIVITY}",
@"public class MyLayout : Android.Widget.LinearLayout
{

	public MyLayout (Android.Content.Context context, Android.Util.IAttributeSet attrs) : base (context, attrs)
	{
		Android.Content.Res.TypedArray a = context.Theme.ObtainStyledAttributes (attrs, Resource.Styleable.MyView, 0,0);
		try {
				bool b = a.GetBoolean (Resource.Styleable.MyView_MyBool, defValue: false);
				if (!b)
					throw new Exception (""MyBool was not true."");
				int i = a.GetInteger (Resource.Styleable.MyView_MyInt, defValue: -1);
				if (i != 128)
					throw new Exception (""MyInt was not 128."");
		}
		finally {
			a.Recycle();
		}
	}
}
");

			var abis = new string [] { "armeabi-v7a", "arm64-v8a", "x86", "x86_64" };
			proj.SetAndroidSupportedAbis (abis);
			var libBuilder = CreateDllBuilder (Path.Combine (rootPath, lib.ProjectName));
			Assert.IsTrue (libBuilder.Build (lib), "Library should have built succeeded.");
			builder = CreateApkBuilder (Path.Combine (rootPath, proj.ProjectName));


			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");

			if (Builder.UseDotNet)
				Assert.True (builder.RunTarget (proj, "Run"), "Project should have run.");
			else if (CommercialBuildAvailable)
				Assert.True (builder.RunTarget (proj, "_Run"), "Project should have run.");
			else
				AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");

			var didStart = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"));
			Assert.IsTrue (didStart, "Activity should have started.");
		}

		DotNetCLI CreateDotNetBuilder (string relativeProjectDir = null)
		{
			if (string.IsNullOrEmpty (relativeProjectDir)) {
				relativeProjectDir = Path.Combine ("temp", TestName);
			}
			string fullProjectDirectory = Path.Combine (Root, relativeProjectDir);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = fullProjectDirectory;

			new XASdkProject ().CopyNuGetConfig (relativeProjectDir);
			return new DotNetCLI (Path.Combine (fullProjectDirectory, $"{TestName}.csproj"));
		}

		DotNetCLI CreateDotNetBuilder (XASdkProject project, string relativeProjectDir = null)
		{
			if (string.IsNullOrEmpty (relativeProjectDir)) {
				relativeProjectDir = Path.Combine ("temp", TestName);
			}
			string fullProjectDirectory = Path.Combine (Root, relativeProjectDir);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = fullProjectDirectory;
			var files = project.Save ();
			project.Populate (relativeProjectDir, files);
			project.CopyNuGetConfig (relativeProjectDir);
			return new DotNetCLI (project, Path.Combine (fullProjectDirectory, project.ProjectFilePath));
		}
	}
}
