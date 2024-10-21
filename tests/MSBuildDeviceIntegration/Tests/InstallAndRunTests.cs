using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using Mono.Cecil;
using NUnit.Framework;

using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class InstallAndRunTests : DeviceTest
	{
		static ProjectBuilder builder;
		static XamarinAndroidApplicationProject proj;

		[TearDown]
		public void Teardown ()
		{
			builder?.Dispose ();
			builder = null;
			proj = null;
		}

		[Test]
		public void NativeAssemblyCacheWithSatelliteAssemblies ([Values (true, false)] bool enableMarshalMethods)
		{
			var path = Path.Combine ("temp", TestName);
			var lib = new XamarinAndroidLibraryProject {
				ProjectName = "Localization",
				OtherBuildItems = {
					new BuildItem ("EmbeddedResource", "Foo.resx") {
						TextContent = () => InlineData.ResxWithContents ("<data name=\"CancelButton\"><value>Cancel</value></data>")
					},
				}
			};

			var languages = new string[] {"es", "de", "fr", "he", "it", "pl", "pt", "ru", "sl" };
			foreach (string lang in languages) {
				lib.OtherBuildItems.Add (
					new BuildItem ("EmbeddedResource", $"Foo.{lang}.resx") {
						TextContent = () => InlineData.ResxWithContents ($"<data name=\"CancelButton\"><value>{lang}</value></data>")
					}
				);
			}

			proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				EnableMarshalMethods = enableMarshalMethods,
			};
			proj.References.Add (new BuildItem.ProjectReference ($"..\\{lib.ProjectName}\\{lib.ProjectName}.csproj", lib.ProjectName, lib.ProjectGuid));
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86", "x86_64");

			using (var libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName))) {
				builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName));
				Assert.IsTrue (libBuilder.Build (lib), "Library Build should have succeeded.");
				Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");

				var apk = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
				var helper = new ArchiveAssemblyHelper (apk);

				foreach (string lang in languages) {
					Assert.IsTrue (helper.Exists ($"assemblies/{lang}/{lib.ProjectName}.resources.dll"), $"Apk should contain satellite assembly for language '{lang}'!");
				}

				RunProjectAndAssert (proj, builder);
				Assert.True (WaitForActivityToStart (proj.PackageName, "MainActivity",
				                                     Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30), "Activity should have started.");
			}
		}

		[Test]
		public void GlobalLayoutEvent_ShouldRegisterAndFire_OnActivityLaunch ([Values (false, true)] bool isRelease)
		{
			string expectedLogcatOutput = "Bug 29730: GlobalLayout event handler called!";

			proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				SupportedOSPlatformVersion = "23",
			};
			if (isRelease || !TestEnvironment.CommercialBuildAvailable) {
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
			AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");
			Assert.IsTrue (MonitorAdbLogcat ((line) => {
				return line.Contains (expectedLogcatOutput);
			}, Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"), 60), $"Output did not contain {expectedLogcatOutput}!");
		}

		[Test]
		public void SubscribeToAppDomainUnhandledException ()
		{
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
			RunProjectAndAssert (proj, builder);

			string expectedLogcatOutput = "# Unhandled Exception: sender=System.Object; e.IsTerminating=True; e.ExceptionObject=System.Exception: CRASH";
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

		[Test]
		[Category ("UsesDevice")]
		[TestCase ("テスト")]
		[TestCase ("随机生成器")]
		[TestCase ("中国")]
		public void SmokeTestBuildAndRunWithSpecialCharacters (string testName)
		{
			var rootPath = Path.Combine (Root, "temp", TestName);
			var proj = new XamarinFormsAndroidApplicationProject () {
				ProjectName = testName,
				IsRelease = true,
			};
			proj.SetAndroidSupportedAbis (DeviceAbi);
			proj.SetDefaultTargetDevice ();
			using (var builder = CreateApkBuilder (Path.Combine (rootPath, proj.ProjectName))){
				Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
				RunProjectAndAssert (proj, builder);
				var timeoutInSeconds = 120;
				Assert.IsTrue (WaitForActivityToStart (proj.PackageName, "MainActivity",
					Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"), timeoutInSeconds));
			}
		}

		[Test]
		public void CustomLinkDescriptionPreserve ([Values (AndroidLinkMode.SdkOnly, AndroidLinkMode.Full)] AndroidLinkMode linkMode)
		{
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
						TextContent = () => getResource("Bug21578")
					},
					new BuildItem.Source ("Bug35195.cs") {
						TextContent = () => getResource("Bug35195")
					},
					new BuildItem.Source ("HttpClientTest.cs") {
						TextContent = () => getResource("HttpClientTest")
					},
					new BuildItem.Source ("PreserveTest.cs") {
						TextContent = () => getResource("PreserveTest")
					},
				},
			};

			proj = new XamarinFormsAndroidApplicationProject () {
				IsRelease = true,
				AndroidLinkModeRelease = linkMode,
				References = {
					new BuildItem ("ProjectReference", "..\\Library1\\Library1.csproj"),
					new BuildItem ("ProjectReference", "..\\LinkTestLib\\LinkTestLib.csproj"),
				},
				PackageReferences = {
					KnownPackages.AndroidXAppCompat,
					KnownPackages.XamarinGoogleAndroidMaterial,
				},
				Sources = {
					new BuildItem.Source ("MaterialTextChanged.cs") {
						TextContent = () => getResource ("MaterialTextChanged")
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

			// NOTE: workaround for netcoreapp3.0 dependency being included along with monoandroid8.0
			// See: https://www.nuget.org/packages/SQLitePCLRaw.bundle_green/2.0.3
			proj.PackageReferences.Add (new Package {
				Id = "SQLitePCLRaw.provider.dynamic_cdecl",
				Version = "2.0.3",
			});

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
			RunProjectAndAssert (proj, builder);

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

			string getResource (string name)
			{
				using (var sr = new StreamReader (typeof (InstallAndRunTests).Assembly.GetManifestResourceStream ($"Xamarin.Android.Build.Tests.Resources.LinkDescTest.{name}.cs")))
					return sr.ReadToEnd ();
			}
		}

		[Test]
		public void JsonDeserializationCreatesJavaHandle ([Values (false, true)] bool isRelease)
		{
			proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			// error SYSLIB0011: 'BinaryFormatter.Serialize(Stream, object)' is obsolete: 'BinaryFormatter serialization is obsolete and should not be used. See https://aka.ms/binaryformatter for more information.'
			proj.SetProperty ("NoWarn", "SYSLIB0011");

			if (isRelease || !TestEnvironment.CommercialBuildAvailable) {
				proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86", "x86_64");
			}

			proj.References.Add (new BuildItem.Reference ("System.Runtime.Serialization"));
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

		void TestJsonDeserializationCreatesJavaHandle ()
		{
			Person p = new Person () {
				Name = ""John Smith"",
				Age = 900,
			};

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
			AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");
			Assert.IsFalse (MonitorAdbLogcat ((line) => {
				return line.Contains ("TestJsonDeserializationCreatesJavaHandle");
			}, Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"), 60), $"Output did contain TestJsonDeserializationCreatesJavaHandle!");
		}

		[Test]
		public void RunWithInterpreterEnabled ([Values (false, true)] bool isRelease)
		{
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

			RunAdbCommand ("shell setprop debug.mono.log all");
			var logProp = RunAdbCommand ("shell getprop debug.mono.log")?.Trim ();
			Assert.AreEqual (logProp, "all", "The debug.mono.log prop was not set correctly.");
			RunProjectAndAssert (proj, builder);

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
			ClearShellProp ("debug.mono.log");
			logProp = RunAdbCommand ("shell getprop debug.mono.log")?.Trim ();
			Assert.AreEqual (logProp, string.Empty, "The debug.mono.log prop was not unset correctly.");
			Assert.IsTrue (didPrintInterpMessage, "logcat output did not contain 'Enabling Mono Interpreter'.");
			Assert.IsTrue (didStart, "Activity should have started.");
		}

		[Test]
		public void RunWithLLVMEnabled ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
			};
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86", "x86_64");
			proj.SetProperty ("EnableLLVM", true.ToString ());

			builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
			RunProjectAndAssert (proj, builder);

			var activityNamespace   = proj.PackageName;
			var activityName        = "MainActivity";
			var logcatFilePath      = Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log");
			var failedToLoad        = new List<string> ();
			bool appLaunched        = MonitorAdbLogcat ((line) => {
				if (SeenFailedToLoad (line))
					failedToLoad.Add (line);
				return SeenActivityDisplayed (line);
			}, logcatFilePath, timeout: 120);

			Assert.IsTrue (appLaunched, "LLVM app did not launch");
			Assert.AreEqual (0, failedToLoad.Count, $"LLVM .so files not loaded:\n{string.Join ("\n", failedToLoad)}");

			bool SeenActivityDisplayed (string line)
			{
				var idx1 = line.IndexOf ("ActivityManager: Displayed", StringComparison.OrdinalIgnoreCase);
				var idx2 = idx1 > 0 ? 0 : line.IndexOf ("ActivityTaskManager: Displayed", StringComparison.OrdinalIgnoreCase);
				return (idx1 > 0 || idx2 > 0) && line.Contains (activityNamespace) && line.Contains (activityName);
			}

			bool SeenFailedToLoad (string line)
			{
				return line.Contains ("Failed to load shared library");
			}
		}

		[Test]
		public void SingleProject_ApplicationId ([Values (false, true)] bool testOnly)
		{
			AssertCommercialBuild ();

			proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("ApplicationId", "com.i.should.get.overridden.by.the.manifest");
			if (testOnly)
				proj.AndroidManifest = proj.AndroidManifest.Replace ("<application", "<application android:testOnly=\"true\"");

			var abis = new string [] { "armeabi-v7a", "arm64-v8a", "x86", "x86_64" };
			proj.SetAndroidSupportedAbis (abis);
			builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
			RunProjectAndAssert (proj, builder);

			var didStart = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"));
			Assert.IsTrue (didStart, "Activity should have started.");
		}

		[Test]
		public void AppWithStyleableUsageRuns ([Values (true, false)] bool isRelease, [Values (true, false)] bool linkResources)
		{
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
			RunProjectAndAssert (proj, builder);

			var didStart = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"));
			Assert.IsTrue (didStart, "Activity should have started.");
		}

		[Test]
		public void CheckXamarinFormsAppDeploysAndAButtonWorks ()
		{
			var proj = new XamarinFormsAndroidApplicationProject ();
			proj.SetAndroidSupportedAbis (DeviceAbi);
			var builder = CreateApkBuilder ();

			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			builder.BuildLogFile = "install.log";
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");

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

		[Test]
		public void SkiaSharpCanvasBasedAppRuns ([Values (true, false)] bool isRelease, [Values (true, false)] bool addResource)
		{
			var app = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				PackageName = "Xamarin.SkiaSharpCanvasTest",
				PackageReferences = {
					KnownPackages.SkiaSharp,
					KnownPackages.SkiaSharp_Views,
					KnownPackages.AndroidXAppCompat,
					KnownPackages.AndroidXAppCompatResources,
				},
			};
			app.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\styles.xml") {
				TextContent = () => @"<resources><style name='AppTheme' parent='Theme.AppCompat.Light.DarkActionBar'/></resources>",
			});
			// begin Remove these lines when the new fixed SkiaSharp is released.
			if (addResource) {
				app.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\values\\attrs.xml") {
					TextContent = () => @"<resources><declare-styleable name='SKCanvasView'>
		<attr name='ignorePixelScaling' format='boolean'/>
	</declare-styleable></resources>",
				});
			}
			// end
			app.LayoutMain = app.LayoutMain.Replace ("<LinearLayout", @"<FrameLayout
	xmlns:android='http://schemas.android.com/apk/res/android'
	xmlns:app='http://schemas.android.com/apk/res-auto'
	android:layout_width='match_parent'
	android:layout_height='match_parent'>
	<SkiaSharp.Views.Android.SKCanvasView
		android:layout_width='match_parent'
		android:layout_height='match_parent'
		android:id='@+id/skiaView' />")
				.Replace ("</LinearLayout>", "</FrameLayout>");
			app.MainActivity = @"using Android.App;
using Android.OS;
using AndroidX.AppCompat.App;

using SkiaSharp;
using SkiaSharp.Views.Android;

namespace UnnamedProject
{
	[Activity(MainLauncher = true, Theme = ""@style/AppTheme"")]
	public class MainActivity : AppCompatActivity
	{
		private SKCanvasView skiaView;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			SetContentView(Resource.Layout.Main);

			skiaView = FindViewById<SKCanvasView>(Resource.Id.skiaView);
		}

		protected override void OnResume()
		{
			base.OnResume();

			skiaView.PaintSurface += OnPaintSurface;
		}

		protected override void OnPause()
		{
			skiaView.PaintSurface -= OnPaintSurface;

			base.OnPause();
		}

		private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
		{
			// the the canvas and properties
			var canvas = e.Surface.Canvas;

			// make sure the canvas is blank
			canvas.Clear(SKColors.White);

			// draw some text
			var paint = new SKPaint
			{
				Color = SKColors.Black,
				IsAntialias = true,
				Style = SKPaintStyle.Fill,
				TextAlign = SKTextAlign.Center,
				TextSize = 24
			};
			var coord = new SKPoint(e.Info.Width / 2, (e.Info.Height + paint.TextSize) / 2);
			canvas.DrawText(""SkiaSharp"", coord, paint);
		}
	}
}
";
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName, app.ProjectName))) {
				b.BuildLogFile = "build1.log";
				b.ThrowOnBuildFailure = false;
				if (!addResource) {
					Assert.IsFalse (b.Build (app, doNotCleanupOnUpdate: true), $"Build of {app.ProjectName} should have failed.");
					Assert.IsTrue (b.LastBuildOutput.ContainsText (isRelease ? "IL8000" : "XA8000"));
					Assert.IsTrue (b.LastBuildOutput.ContainsText ("@styleable/SKCanvasView"), "Expected '@styleable/SKCanvasView' in build output.");
					Assert.IsTrue (b.LastBuildOutput.ContainsText ("@styleable/SKCanvasView_ignorePixelScaling"), "Expected '@styleable/SKCanvasView_ignorePixelScaling' in build output.");
					return;
				}
				Assert.IsTrue (b.Build (app, doNotCleanupOnUpdate: true), $"Build of {app.ProjectName} should have succeeded.");
				b.BuildLogFile = "install1.log";
				Assert.IsTrue (b.Install (app, doNotCleanupOnUpdate: true), "Install should have suceeded.");
				AdbStartActivity ($"{app.PackageName}/{app.JavaPackageName}.MainActivity");
				WaitForPermissionActivity (Path.Combine (Root, b.ProjectDirectory, "permission-logcat.log"));
				ClearAdbLogcat ();
				WaitForActivityToStart (app.PackageName, "MainActivity",
					Path.Combine (Root, b.ProjectDirectory, "startup-logcat.log"), 15);
			}
		}


		[Test]
		public void CheckResouceIsOverridden ()
		{
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
			app.LayoutMain = app.LayoutMain.Replace ("@string/hello", "@string/hello_me");
			using (var l1 = CreateDllBuilder (Path.Combine ("temp", TestName, library.ProjectName)))
			using (var l2 = CreateDllBuilder (Path.Combine ("temp", TestName, library2.ProjectName)))
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName, app.ProjectName))) {
				b.ThrowOnBuildFailure = false;
				b.LatestTargetFrameworkVersion (out string apiLevel);
				app.SupportedOSPlatformVersion  = "24";
				app.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{app.PackageName}"">
	<uses-sdk android:targetSdkVersion=""{apiLevel}"" />
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
				WaitForPermissionActivity (Path.Combine (Root, b.ProjectDirectory, "permission-logcat.log"));
				WaitForActivityToStart (app.PackageName, "MainActivity",
					Path.Combine (Root, b.ProjectDirectory, "startup-logcat.log"), 15);
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
				app.LayoutMain = app.LayoutMain.Replace ("@string/hello", "@string/hello_me");
				b.LatestTargetFrameworkVersion (out apiLevel);
				app.SupportedOSPlatformVersion  = "24";
				app.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{app.PackageName}"">
	<uses-sdk android:targetSdkVersion=""{apiLevel}"" />
	<application android:label=""${{PROJECT_NAME}}"">
	</application >
</manifest> ";
				b.BuildLogFile = "build.log";
				Assert.IsTrue (b.Build (app, doNotCleanupOnUpdate: true), $"Build of {app.ProjectName} should have suceeded.");
				b.BuildLogFile = "install.log";
				Assert.IsTrue (b.Install (app, doNotCleanupOnUpdate: true), "Install should have suceeded.");
				AdbStartActivity ($"{app.PackageName}/{app.JavaPackageName}.MainActivity");
				WaitForPermissionActivity (Path.Combine (Root, b.ProjectDirectory, "permission-logcat.log"));
				WaitForActivityToStart (app.PackageName, "MainActivity",
					Path.Combine (Root, b.ProjectDirectory, "startup-logcat.log"), 15);
				ui = GetUI ();
				node = ui.XPathSelectElement ($"//node[contains(@resource-id,'myButton')]");
				StringAssert.AreEqualIgnoringCase ("Click Me! One", node.Attribute ("text").Value, "Text of Button myButton should have been \"Click Me! One\"");
			}
		}

		[Test]
		[Category ("WearOS")]
		public void DotNetInstallAndRunPreviousSdk ([Values (false, true)] bool isRelease)
		{
			var proj = new XamarinFormsAndroidApplicationProject () {
				TargetFramework = "net8.0-android",
				IsRelease = isRelease,
				EnableDefaultItems = true,
			};

			var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "`dotnet build` should succeed");
			RunProjectAndAssert (proj, builder);

			WaitForPermissionActivity (Path.Combine (Root, builder.ProjectDirectory, "permission-logcat.log"));
			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30);
			Assert.IsTrue(didLaunch, "Activity should have started.");
		}

		[Test]
		public void TypeAndMemberRemapping ([Values (false, true)] bool isRelease)
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				EnableDefaultItems = true,
				OtherBuildItems = {
					new AndroidItem._AndroidRemapMembers ("RemapActivity.xml") {
						Encoding = Encoding.UTF8,
						TextContent = () => ResourceData.RemapActivityXml,
					},
					new AndroidItem.AndroidJavaSource ("RemapActivity.java") {
						Encoding = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false),
						TextContent = () => ResourceData.RemapActivityJava,
						Metadata = {
							{ "Bind", "True" },
						},
					},
				},
			};
			proj.MainActivity = proj.DefaultMainActivity.Replace (": Activity", ": global::Example.RemapActivity");
			var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "`dotnet build` should succeed");
			RunProjectAndAssert (proj, builder);
			var appStartupLogcatFile = Path.Combine (Root, builder.ProjectDirectory, "logcat.log");
			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity", appStartupLogcatFile);
			Assert.IsTrue (didLaunch, "MainActivity should have launched!");
			var logcatOutput = File.ReadAllText (appStartupLogcatFile);

			StringAssert.Contains (
					"RemapActivity.onMyCreate() invoked!",
					logcatOutput,
					"Activity.onCreate() wasn't remapped to RemapActivity.onMyCreate()!"
			);
			StringAssert.Contains (
					"ViewHelper.mySetOnClickListener() invoked!",
					logcatOutput,
					"View.setOnClickListener() wasn't remapped to ViewHelper.mySetOnClickListener()!"
			);
		}

		[Test]
		public void SupportDesugaringStaticInterfaceMethods ()
		{
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				EnableDefaultItems = true,
				OtherBuildItems = {
					new AndroidItem.AndroidJavaSource ("StaticMethodsInterface.java") {
						Encoding = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false),
						TextContent = () => ResourceData.IdmStaticMethodsInterface,
						Metadata = {
							{ "Bind", "True" },
						},
					},
				},
			};

			// Note: To properly test, Desugaring must be *enabled*, which requires that
			// `$(SupportedOSPlatformVersion)` be *less than* 23.  21 is currently the default,
			// but set this explicitly anyway just so that this implicit requirement is explicit.
			proj.SupportedOSPlatformVersion = "21";

			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", @"
		Console.WriteLine ($""# jonp static interface default method invocation; IStaticMethodsInterface.Value={Example.IStaticMethodsInterface.Value}"");
");
			var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "`dotnet build` should succeed");
			RunProjectAndAssert (proj, builder);
			var appStartupLogcatFile = Path.Combine (Root, builder.ProjectDirectory, "logcat.log");
			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity", appStartupLogcatFile);
			Assert.IsTrue (didLaunch, "MainActivity should have launched!");
			var logcatOutput = File.ReadAllText (appStartupLogcatFile);

			StringAssert.Contains (
					"IStaticMethodsInterface.Value=3",
					logcatOutput,
					"Was IStaticMethodsInterface.Value executed?"
			);
		}

		[Test]
		public void FastDeployEnvironmentFiles ([Values (false, true)] bool isRelease)
		{
			var proj = new XamarinAndroidApplicationProject {
				ProjectName = nameof (FastDeployEnvironmentFiles),
				RootNamespace = nameof (FastDeployEnvironmentFiles),
				IsRelease = isRelease,
				EnableDefaultItems = true,
				OtherBuildItems = {
					new BuildItem("AndroidEnvironment", "env.txt") {
						TextContent = () => @"Foo=Bar
Bar34=Foo55",
					}
				}
			};
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", @"
		Console.WriteLine (""Foo="" + Environment.GetEnvironmentVariable(""Foo""));
		Console.WriteLine (""Bar34="" + Environment.GetEnvironmentVariable(""Bar34""));");
			var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "`dotnet build` should succeed");
			RunProjectAndAssert (proj, builder);

			WaitForPermissionActivity (Path.Combine (Root, builder.ProjectDirectory, "permission-logcat.log"));
			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30);
			Assert.IsTrue(didLaunch, "Activity should have started.");
			var appStartupLogcatFile = Path.Combine (Root, builder.ProjectDirectory, "logcat.log");
			var logcatOutput = File.ReadAllText (appStartupLogcatFile);

			StringAssert.Contains (
					"Foo=Bar",
					logcatOutput,
					"The Environment variable \"Foo\" was not set."
			);
			StringAssert.Contains (
					"Bar34=Foo55",
					logcatOutput,
					"The Environment variable \"Bar34\" was not set."
			);
		}

		[Test]
		public void EnableAndroidStripILAfterAOT ([Values (false, true)] bool profiledAOT)
		{
			var proj = new XamarinAndroidApplicationProject {
				ProjectName = nameof (EnableAndroidStripILAfterAOT),
				RootNamespace = nameof (EnableAndroidStripILAfterAOT),
				IsRelease = true,
				EnableDefaultItems = true,
			};
			proj.SetProperty("AndroidStripILAfterAOT", "true");
			proj.SetProperty("AndroidEnableProfiledAot", profiledAOT.ToString ());
			// So we can use Mono.Cecil to open assemblies directly
			proj.SetProperty ("AndroidEnableAssemblyCompression", "false");

			var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "`dotnet build` should succeed");

			var apk = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
			FileAssert.Exists (apk);
			var helper = new ArchiveAssemblyHelper (apk);
			Assert.IsTrue (helper.Exists ($"assemblies/{proj.ProjectName}.dll"), $"{proj.ProjectName}.dll should exist in apk!");
			using (var stream = helper.ReadEntry ($"assemblies/{proj.ProjectName}.dll")) {
				stream.Position = 0;
				using var assembly = AssemblyDefinition.ReadAssembly (stream);
				var type = assembly.MainModule.GetType ($"{proj.RootNamespace}.MainActivity");
				var method = type.Methods.FirstOrDefault (p => p.Name == "OnCreate");
				Assert.IsNotNull (method, $"{proj.RootNamespace}.MainActivity.OnCreate should exist!");
				Assert.IsTrue (!method.HasBody || method.Body.Instructions.Count == 0, $"{proj.RootNamespace}.MainActivity.OnCreate should have no body!");
			}

			RunProjectAndAssert (proj, builder);

			WaitForPermissionActivity (Path.Combine (Root, builder.ProjectDirectory, "permission-logcat.log"));
			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30);
			Assert.IsTrue(didLaunch, "Activity should have started.");
		}

		[Test]
		public void FixLegacyResourceDesignerStep ([Values (true, false)] bool isRelease)
		{
			string previousTargetFramework = "net8.0-android";

			var library1 = new XamarinAndroidLibraryProject {
				IsRelease = isRelease,
				TargetFramework = previousTargetFramework,
				ProjectName = "Library1",
				AndroidResources = {
					new AndroidItem.AndroidResource (() => "Resources\\values\\strings2.xml") {
						TextContent = () => @"<?xml version=""1.0"" encoding=""utf-8""?>
<resources>
	<string name=""hello"">Hi!</string>
</resources>",
					},
				},
			};
			var library2 = new XamarinAndroidLibraryProject {
				IsRelease = isRelease,
				TargetFramework = previousTargetFramework,
				ProjectName = "Library2",
				OtherBuildItems = {
					new BuildItem.Source("Foo.cs") {
						TextContent = () => "public class Foo { public static int Hello => Library1.Resource.String.hello; } ",
					}
				}
			};
			library2.AndroidResources.Clear ();
			library2.SetProperty ("AndroidGenerateResourceDesigner", "false"); // Disable Android Resource Designer generation
			library2.AddReference (library1);
			proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				ProjectName = "MyApp",
			};
			proj.AddReference (library2);
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", "Console.WriteLine(Foo.Hello);");

			using (var library1Builder = CreateDllBuilder (Path.Combine ("temp", TestName, library1.ProjectName)))
			using (var library2Builder = CreateDllBuilder (Path.Combine ("temp", TestName, library2.ProjectName))) {
				builder = CreateApkBuilder (Path.Combine ("temp", TestName, proj.ProjectName));
				Assert.IsTrue (library1Builder.Build (library1, doNotCleanupOnUpdate: true), $"Build of {library1.ProjectName} should have succeeded.");
				Assert.IsTrue (library2Builder.Build (library2, doNotCleanupOnUpdate: true), $"Build of {library2.ProjectName} should have succeeded.");
				Assert.IsTrue (builder.Build (proj), $"Build of {proj.ProjectName} should have succeeded.");

				RunProjectAndAssert (proj, builder);

				WaitForPermissionActivity (Path.Combine (Root, builder.ProjectDirectory, "permission-logcat.log"));
				bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
					Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30);
				Assert.IsTrue (didLaunch, "Activity should have started.");
			}
		}

		[Test]
		public void MicrosoftIntune ([Values (false, true)] bool isRelease)
		{
			Assert.Ignore ("https://github.com/xamarin/xamarin-android/issues/8548");

			proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				PackageReferences = {
					KnownPackages.AndroidXAppCompat,
					KnownPackages.Microsoft_Intune_Maui_Essentials_android,
				},
			};
			proj.MainActivity = proj.DefaultMainActivity
				.Replace ("Icon = \"@drawable/icon\")]", "Icon = \"@drawable/icon\", Theme = \"@style/Theme.AppCompat.Light.DarkActionBar\")]")
				.Replace ("public class MainActivity : Activity", "public class MainActivity : AndroidX.AppCompat.App.AppCompatActivity");
			var abis = new string [] { "armeabi-v7a", "arm64-v8a", "x86", "x86_64" };
			proj.SetAndroidSupportedAbis (abis);
			builder = CreateApkBuilder ();
			builder.BuildLogFile = "install.log";
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");

			var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			var dexFile = Path.Combine (intermediate, "android", "bin", "classes.dex");
			FileAssert.Exists (dexFile);
			var className = "Lcom/xamarin/microsoftintune/MainActivity;";
			var methodName = "onMAMCreate";
			Assert.IsTrue (DexUtils.ContainsClassWithMethod (className, methodName, "(Landroid/os/Bundle;)V", dexFile, AndroidSdkPath), $"`{dexFile}` should include `{className}` and `{methodName}!");

			RunProjectAndAssert (proj, builder);

			WaitForPermissionActivity (Path.Combine (Root, builder.ProjectDirectory, "permission-logcat.log"));
			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30);
			Assert.IsTrue (didLaunch, "Activity should have started.");
		}

		[Test]
		public void GradleFBProj ([Values (false, true)] bool isRelease)
		{
			var moduleName = "Library";
			var gradleTestProjectDir = Path.Combine (Root, "temp", "gradle", TestName);
			var gradleModule = new AndroidGradleModule (Path.Combine (gradleTestProjectDir, moduleName));
			gradleModule.PackageName = "com.microsoft.mauifacebook";
			gradleModule.BuildGradleFileContent = $@"
plugins {{
    id(""com.android.library"")
}}
android {{
    namespace = ""{gradleModule.PackageName}""
    compileSdk = {XABuildConfig.AndroidDefaultTargetDotnetApiLevel}
    defaultConfig {{
        minSdk = {XABuildConfig.AndroidMinimumDotNetApiLevel}
    }}
}}
dependencies {{
    implementation(""androidx.appcompat:appcompat:1.7.0"")
    implementation(""com.google.android.material:material:1.11.0"")
    implementation(""com.facebook.android:facebook-android-sdk:17.0.2"")
}}
";
			gradleModule.JavaSources.Add (new AndroidItem.AndroidJavaSource ("FacebookSdk.java") {
				TextContent = () => $@"
package com.microsoft.mauifacebook;
public class FacebookSdk {{
    static com.facebook.appevents.AppEventsLogger _logger;
    public static void initializeSDK(android.app.Activity activity, Boolean isDebug) {{
        android.app.Application application = activity.getApplication();
        com.facebook.FacebookSdk.sdkInitialize(application);
        com.facebook.FacebookSdk.addLoggingBehavior(com.facebook.LoggingBehavior.APP_EVENTS);
        com.facebook.appevents.AppEventsLogger.activateApp(application);
        _logger = com.facebook.appevents.AppEventsLogger.newLogger(activity);
    }}
    public static void logEvent(String eventName) {{
        _logger.logEvent(eventName);
    }}
}}
",
			});

			var gradleProject = new AndroidGradleProject (gradleTestProjectDir) {
				Modules = {
					gradleModule,
				},
			};
			gradleProject.Create ();

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				ExtraNuGetConfigSources = {
					"https://api.nuget.org/v3/index.json",
				},
				OtherBuildItems = {
					new AndroidItem.TransformFile ("Transforms\\Metadata.xml") {
						TextContent = () => $@"<metadata><attr path=""/api/package[@name='{gradleModule.PackageName}']"" name=""managedName"">Facebook</attr></metadata>",
					},
					new BuildItem (KnownProperties.AndroidGradleProject, gradleProject.BuildFilePath) {
						Metadata = {
							{ "ModuleName", moduleName },
						},
					},
					new BuildItem ("AndroidMavenLibrary", "com.facebook.android:facebook-core") {
						Metadata = {
							{ "Version", "17.0.2" },
							{ "Bind", "false" },
						},
					},
					new BuildItem ("AndroidMavenLibrary", "com.facebook.android:facebook-bolts") {
						Metadata = {
							{ "Version", "17.0.2" },
							{ "Bind", "false" },
						},
					},
				},
				PackageReferences = {
					new Package {
						Id = "Xamarin.AndroidX.AppCompat",
						Version = "1.7.0.3",
					},
					new Package {
						Id = "Xamarin.AndroidX.Annotation",
						Version = "1.8.2.1",
					},
					new Package {
						Id = "Xamarin.AndroidX.Legacy.Support.Core.Utils",
						Version = "1.0.0.29",
					},
					new Package {
						Id = "Xamarin.Google.Android.InstallReferrer",
						Version = "1.1.2.6",
					},
					new Package {
						Id = "Xamarin.AndroidX.Core.Core.Ktx",
						Version = "1.13.1.5",
					},
					new Package {
						Id = "Xamarin.Kotlin.StdLib",
						Version = "2.0.21",
					},
				},
			};
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", @"
Facebook.FacebookSdk.InitializeSDK(this, Java.Lang.Boolean.True);
Facebook.FacebookSdk.LogEvent(""TestFacebook"");
");
			proj.AndroidManifest =@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" xmlns:tools=""http://schemas.android.com/tools"" android:versionCode=""1"" android:versionName=""1.0"" package=""com.xamarin.gradleproj"">
  <application android:label=""UnnamedProject"">
    <meta-data android:name=""com.facebook.sdk.ApplicationId"" android:value=""appid""/>
    <meta-data android:name=""com.facebook.sdk.ClientToken"" android:value=""token""/>
  </application>
</manifest>";

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj));
			RunProjectAndAssert (proj, builder);
		}

	}
}
