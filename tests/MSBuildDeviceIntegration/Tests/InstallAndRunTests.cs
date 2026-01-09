using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.Tasks;
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

		static IEnumerable<object[]> Get_DotNetRun_Data ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in Enum.GetValues (typeof (AndroidRuntime))) {
				AddTestData (true, "llvm-ir", runtime);
				AddTestData (false, "llvm-ir", runtime);
				AddTestData (true, "managed", runtime);
				// NOTE: TypeMappingStep is not yet setup for Debug mode
				//AddTestData (false, "managed", runtime);
			}

			return ret;

			void AddTestData (bool isRelease, string typemapImplementation, AndroidRuntime runtime)
			{
				ret.Add (new object[] {
					isRelease,
					typemapImplementation,
					runtime,
				});
			}
		}

		[Test]
		[TestCaseSource (nameof (Get_DotNetRun_Data))]
		public void DotNetRun (bool isRelease, string typemapImplementation, AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			if (runtime == AndroidRuntime.NativeAOT && typemapImplementation == "llvm-ir") {
				Assert.Ignore ("NativeAOT doesn't work with LLVM-IR typemaps");
			}

			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("_AndroidTypeMapImplementation", typemapImplementation);
			using var builder = CreateApkBuilder ();
			builder.Save (proj);

			var dotnet = new DotNetCLI (Path.Combine (Root, builder.ProjectDirectory, proj.ProjectFilePath));
			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");
			Assert.IsTrue (dotnet.Run (), "`dotnet run --no-build` should succeed");

			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30);
			Assert.IsTrue (didLaunch, "Activity should have started.");
		}

		[Test]
		public void DotNetRunWaitForExit ()
		{
			const string logcatMessage = "DOTNET_RUN_TEST_MESSAGE_12345";
			var proj = new XamarinAndroidApplicationProject ();

			// Enable verbose output from Microsoft.Android.Run for debugging
			proj.SetProperty ("_AndroidRunExtraArgs", "--verbose");

			// Add a Console.WriteLine that will appear in logcat
			proj.MainActivity = proj.DefaultMainActivity.Replace (
				"//${AFTER_ONCREATE}",
				$"Console.WriteLine (\"{logcatMessage}\");");

			using var builder = CreateApkBuilder ();
			builder.Save (proj);

			var dotnet = new DotNetCLI (Path.Combine (Root, builder.ProjectDirectory, proj.ProjectFilePath));
			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");

			// Start dotnet run with WaitForExit=true, which uses Microsoft.Android.Run
			using var process = dotnet.StartRun ();

			var locker = new Lock ();
			var output = new StringBuilder ();
			var outputReceived = new ManualResetEventSlim (false);
			bool foundMessage = false;

			process.OutputDataReceived += (sender, e) => {
				if (e.Data != null) {
					lock (locker) {
						output.AppendLine (e.Data);
						if (e.Data.Contains (logcatMessage)) {
							foundMessage = true;
							outputReceived.Set ();
						}
					}
				}
			};
			process.ErrorDataReceived += (sender, e) => {
				if (e.Data != null) {
					lock (locker) {
						output.AppendLine ($"STDERR: {e.Data}");
					}
				}
			};

			process.BeginOutputReadLine ();
			process.BeginErrorReadLine ();

			// Wait for the expected message or timeout
			bool messageFound = outputReceived.Wait (TimeSpan.FromSeconds (60));

			// Kill the process (simulating Ctrl+C)
			if (!process.HasExited) {
				process.Kill (entireProcessTree: true);
				process.WaitForExit ();
			}

			// Write the output to a log file for debugging
			string logPath = Path.Combine (Root, builder.ProjectDirectory, "dotnet-run-output.log");
			File.WriteAllText (logPath, output.ToString ());
			TestContext.AddTestAttachment (logPath);

			Assert.IsTrue (foundMessage, $"Expected message '{logcatMessage}' was not found in output. See {logPath} for details.");
		}

		[Test]
		public void DeployToDevice ([Values] bool isRelease, [Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease
			};
			proj.SetRuntime (runtime);
			using var builder = CreateApkBuilder ();
			builder.Save (proj);

			var dotnet = new DotNetCLI (Path.Combine (Root, builder.ProjectDirectory, proj.ProjectFilePath));
			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");
			Assert.IsTrue (dotnet.Build ("DeployToDevice"), "`dotnet build -t:DeployToDevice` should succeed");

			// Verify correct targets ran based on FastDev support
			if (TestEnvironment.CommercialBuildAvailable) {
				dotnet.AssertTargetIsNotSkipped ("_Upload");
				dotnet.AssertTargetIsSkipped ("_DeployApk", defaultIfNotUsed: true);
				dotnet.AssertTargetIsSkipped ("_DeployAppBundle", defaultIfNotUsed: true);
			} else {
				dotnet.AssertTargetIsSkipped ("_Upload", defaultIfNotUsed: true);
				dotnet.AssertTargetIsNotSkipped ("_DeployApk");
				dotnet.AssertTargetIsNotSkipped ("_DeployAppBundle");
			}

			// Launch the app using adb
			ClearAdbLogcat ();
			var result = AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");
			Assert.IsTrue (result.Contains ("Starting: Intent"), $"Activity should have launched. adb output:\n{result}");

			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30);
			Assert.IsTrue (didLaunch, "Activity should have started.");
		}

		[Test]
		public void ActivityAliasRuns ([Values] bool isRelease, [Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease
			};
			proj.SetRuntime (runtime);
			proj.AndroidManifest = proj.AndroidManifest.Replace ("</application>", @"
<activity-alias
			android:name="".MainActivityAlias""
			android:enabled=""true""
			android:icon=""@drawable/icon""
			android:targetActivity="".MainActivity""
			android:exported=""true"">
			<intent-filter>
				<action android:name=""android.intent.action.MAIN"" />
				<category android:name=""android.intent.category.LAUNCHER"" />
			</intent-filter>
		</activity-alias>
</application>");
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${ATTRIBUTES}",$"[Register(\"{proj.PackageName}.MainActivity\")]").Replace("MainLauncher = true", "MainLauncher = false");
			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
			RunProjectAndAssert (proj, builder);
			Assert.True (WaitForActivityToStart (proj.PackageName, "MainActivityAlias",
				Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30), "Activity MainActivityAlias should have started.");
		}

		[Test]
		public void NativeAssemblyCacheWithSatelliteAssemblies ([Values] bool enableMarshalMethods, [Values] AndroidRuntime runtime)
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("NativeAOT doesn't support individual assemblies");
			}

			if (enableMarshalMethods && runtime == AndroidRuntime.CoreCLR) {
				// This currently fails with the following exception:
				//
				// error XARMM7015: System.NotSupportedException: Writing mixed-mode assemblies is not supported
				//  at Mono.Cecil.ModuleWriter.Write(ModuleDefinition module, Disposable`1 stream, WriterParameters parameters)
				//  at Mono.Cecil.ModuleWriter.WriteModule(ModuleDefinition module, Disposable`1 stream, WriterParameters parameters)
				//  at Mono.Cecil.ModuleDefinition.Write(String fileName, WriterParameters parameters)
				//  at Mono.Cecil.AssemblyDefinition.Write(String fileName, WriterParameters parameters)
				//  at Xamarin.Android.Tasks.MarshalMethodsAssemblyRewriter.Rewrite(Boolean brokenExceptionTransitions) in src/Xamarin.Android.Build.Tasks/Utilities/MarshalMethodsAssemblyRewriter.cs:line 165
				//  at Xamarin.Android.Tasks.RewriteMarshalMethods.RewriteMethods(NativeCodeGenState state, Boolean brokenExceptionTransitionsEnabled) in src/Xamarin.Android.Build.Tasks/Tasks/RewriteMarshalMethods.cs:line 160
				Assert.Ignore ("CoreCLR: fails because of a Mono.Cecil lack of support");
				return;
			}

			var path = Path.Combine ("temp", TestName);
			var lib = new XamarinAndroidLibraryProject {
				IsRelease = isRelease,
				ProjectName = "Localization",
				OtherBuildItems = {
					new BuildItem ("EmbeddedResource", "Foo.resx") {
						TextContent = () => InlineData.ResxWithContents ("<data name=\"CancelButton\"><value>Cancel</value></data>")
					},
				}
			};
			lib.SetRuntime (runtime);

			var languages = new string[] {"es", "de", "fr", "he", "it", "pl", "pt", "ru", "sl" };
			foreach (string lang in languages) {
				lib.OtherBuildItems.Add (
					new BuildItem ("EmbeddedResource", $"Foo.{lang}.resx") {
						TextContent = () => InlineData.ResxWithContents ($"<data name=\"CancelButton\"><value>{lang}</value></data>")
					}
				);
			}

			proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
				EnableMarshalMethods = enableMarshalMethods,
			};
			proj.SetRuntime (runtime);
			proj.References.Add (new BuildItem.ProjectReference ($"..\\{lib.ProjectName}\\{lib.ProjectName}.csproj", lib.ProjectName, lib.ProjectGuid));

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
		public void GlobalLayoutEvent_ShouldRegisterAndFire_OnActivityLaunch ([Values] bool isRelease, [Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			string expectedLogcatOutput = "Bug 29730: GlobalLayout event handler called!";

			proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
				SupportedOSPlatformVersion = "23",
			};
			proj.SetRuntime (runtime);

			if (isRelease || !TestEnvironment.CommercialBuildAvailable) {
				if (runtime == AndroidRuntime.MonoVM) {
					proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86", "x86_64");
				} else {
					proj.SetRuntimeIdentifiers (new [] {"arm64-v8a", "x86_64"});
				}
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
		public void SubscribeToAppDomainUnhandledException ([Values] AndroidRuntime runtime)
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			if (runtime == AndroidRuntime.CoreCLR || runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("AppDomain.CurrentDomain.UnhandledException doesn't work in CoreCLR or NativeAOT");
				return;
			}

			proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			if (runtime == AndroidRuntime.MonoVM) {
				proj.SetAndroidSupportedAbis ("armeabi-v7a", "arm64-v8a", "x86", "x86_64");
			} else {
				proj.SetRuntimeIdentifiers (new [] {"arm64-v8a", "x86_64"});
			}

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

			string? expectedSender = runtime switch
			{
				AndroidRuntime.MonoVM => "System.Object", // MonoVM passes the current domain as the sender
				AndroidRuntime.CoreCLR => null, // CoreCLR explicitly passes a `null` sender
				_ => throw new NotImplementedException($"Test does not support runtime {runtime}"),
			};
			string expectedLogcatOutput = $"# Unhandled Exception: sender={expectedSender}; e.IsTerminating=True; e.ExceptionObject=System.Exception: CRASH";
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

		static IEnumerable<object[]> Get_SmokeTestBuildAndRunWithSpecialCharacters_Data ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in Enum.GetValues (typeof (AndroidRuntime))) {
				AddTestData ("テスト", runtime);
				AddTestData ("随机生成器", runtime);
				AddTestData ("中国", runtime);
			}

			return ret;

			void AddTestData (string testName, AndroidRuntime runtime)
			{
				ret.Add (new object[] {
					testName,
					runtime,
				});
			}
		}

		[Test]
		[Category ("UsesDevice")]
		[TestCaseSource (nameof (Get_SmokeTestBuildAndRunWithSpecialCharacters_Data))]
		public void SmokeTestBuildAndRunWithSpecialCharacters (string testName, AndroidRuntime runtime)
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			// TODO: fix NativeAOT builds. Despite the .so library being preset in the apk (and named correctly)
			//       all the tests fail with one of:
			//
			//  java.lang.UnsatisfiedLinkError: dlopen failed: library "libテスト.so" not found
			//  java.lang.UnsatisfiedLinkError: dlopen failed: library "lib中国.so" not found
			//  java.lang.UnsatisfiedLinkError: dlopen failed: library "lib随机生成器.so" not found
			//
			// It might be an issue with the Android shared libary loader or name encoding in the archive. It might
			// be a good idea to limit .so names to ASCII.
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("NativeAOT doesn't work well with diacritics in the application library name");
			}

			var rootPath = Path.Combine (Root, "temp", TestName);
			var proj = new XamarinFormsAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				ProjectName = testName,
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
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

		// TODO: fix/review it for CoreCLR. It currently fails with the following exception:
		//
		// System.TypeInitializationException: TypeInitialization_Type, SQLite.SQLiteConnection
		//  ---> System.MissingMethodException: .ctor
		//    at System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointerInternal(IntPtr, RuntimeType)
		//    at SQLitePCL.SQLite3Provider_dynamic_cdecl.NativeMethods.Setup(IGetFunctionPointer)
		//    at SQLitePCL.Batteries_V2.DoDynamic_cdecl(String, Int32)
		//    Exception_EndOfInnerExceptionStack
		//    at SQLite.SQLiteConnection..ctor(SQLiteConnectionString)
		//    at SQLite.SQLiteConnectionPool.Entry..ctor(SQLiteConnectionString)
		//    at SQLite.SQLiteConnectionPool.GetConnectionAndTransactionLock(SQLiteConnectionString, Object& )
		//    at SQLite.SQLiteAsyncConnection.<>c__DisplayClass33_0`1.<WriteAsync>b__0()
		//    at System.Threading.Tasks.Task`1.InnerInvoke()
		//    at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread, ExecutionContext, ContextCallback, Object)
		// --- End of stack trace from previous location ---
		//    at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread, ExecutionContext, ContextCallback, Object)
		//    at System.Threading.Tasks.Task.ExecuteWithThreadLocal(Task&, Thread )
		// --- End of stack trace from previous location ---
		//    at LinkTestLib.Bug35195.AttemptCreateTable()
		//
		[Test]
		public void CustomLinkDescriptionPreserve (
		  [Values (AndroidLinkMode.SdkOnly, AndroidLinkMode.Full)] AndroidLinkMode linkMode,
		  [Values] AndroidRuntime runtime
		)
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			if (runtime == AndroidRuntime.CoreCLR) {
				Assert.Ignore ("Currently broken on CoreCLR");
			}

			// TODO: NativeAOT perhaps should work here (ignoring all the MonoAOT settings?), but for now it fails with
			//
			//  Microsoft.NET.Sdk.FrameworkReferenceResolution.targets(120,5): error NETSDK1207: Ahead-of-time compilation is not supported for the target framework.
			//
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("NativeAOT is currently broken here");
			}

			var lib1 = new XamarinAndroidLibraryProject () {
				IsRelease = isRelease,
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
		[System.Diagnostics.CodeAnalysis.DynamicDependency (""DynamicDependencyTargetMethod()"", typeof(Library1.LinkerClass))]
		public LinkerClass () { }

		public bool IsPreserved { get { return true; } }

		public bool ThisMethodShouldBePreserved () { return true; }

		public void WasThisMethodPreserved (string arg1) { }

		public void DynamicDependencyTargetMethod () { }
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
			lib1.SetRuntime (runtime);

			var lib2 = new DotNetStandard {
				IsRelease = isRelease,
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
			lib2.SetRuntime (runtime);

			proj = new XamarinFormsAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
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
			proj.SetRuntime (runtime);

			// NOTE: workaround for netcoreapp3.0 dependency being included along with monoandroid8.0
			// See: https://www.nuget.org/packages/SQLitePCLRaw.bundle_green/2.0.3
			proj.PackageReferences.Add (new Package {
				Id = "SQLitePCLRaw.provider.dynamic_cdecl",
				Version = "2.0.3",
			});

			proj.AndroidManifest = proj.AndroidManifest.Replace ("</manifest>", "<uses-permission android:name=\"android.permission.INTERNET\" /></manifest>");
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
		public void JsonDeserializationCreatesJavaHandle ([Values] bool isRelease, [Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}
			proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			// error SYSLIB0011: 'BinaryFormatter.Serialize(Stream, object)' is obsolete: 'BinaryFormatter serialization is obsolete and should not be used. See https://aka.ms/binaryformatter for more information.'
			proj.SetProperty ("NoWarn", "SYSLIB0011");

			if (isRelease || !TestEnvironment.CommercialBuildAvailable) {
				proj.SetAndroidSupportedAbis (DeviceAbi);
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
/
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
		public void RunWithInterpreterEnabled ([Values] bool isRelease, [Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			// MonoVM-only test, for now (until CoreCLR has interpreter we can use)
			if (runtime != AndroidRuntime.MonoVM) {
				Assert.Ignore ("MonoVM-only test for the moment");
			}

			proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
				AotAssemblies = false, // Release defaults to Profiled AOT for .NET 6
			};
			proj.SetRuntime (runtime);
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
			// Mono-only test
			proj.SetRuntime (AndroidRuntime.MonoVM);
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
		public void SingleProject_ApplicationId ([Values] bool testOnly, [Values] AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			AssertCommercialBuild ();

			proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("ApplicationId", "com.i.should.get.overridden.by.the.manifest");
			if (testOnly)
				proj.AndroidManifest = proj.AndroidManifest.Replace ("<application", "<application android:testOnly=\"true\"");

			proj.SetAndroidSupportedAbis (DeviceAbi);
			builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
			RunProjectAndAssert (proj, builder);

			var didStart = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"));
			Assert.IsTrue (didStart, "Activity should have started.");
		}

		[Test]
		public void AppWithStyleableUsageRuns ([Values] bool isRelease,	[Values] bool linkResources, [Values] bool useStringTypeMaps, [Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			// Not all combinations are valid, ignore those that aren't
			if (runtime == AndroidRuntime.MonoVM && useStringTypeMaps) {
				Assert.Ignore ("String-based typemaps mode is used only in CoreCLR and NativeAOT apps");
			}

			if (runtime != AndroidRuntime.MonoVM && isRelease && useStringTypeMaps) {
				Assert.Ignore ("String-based typemaps mode is available only in Debug CoreCLR builds");
			}

			// TODO: fix this for NativeAOT
			if (runtime == AndroidRuntime.NativeAOT && isRelease && !useStringTypeMaps) {
				// This configuration currently fails with a long stack trace, the gist of it is:
				//
				//  AndroidRuntime: java.lang.RuntimeException: Unable to start activity ComponentInfo{com.xamarin.appwithstyleableusageruns_nativeaot/com.xamarin.appwithstyleableusageruns_nativeaot.MainActivity}
				//  AndroidRuntime: Caused by: android.view.InflateException: Binary XML file line #1 in com.xamarin.appwithstyleableusageruns_nativeaot:layout/main: Binary XML file line #1 in com.xamarin.appwithstyleableusageruns_nativeaot:layout/main: Error inflating class crc64f75eeacfa0ca1368.MyLayout
				//  AndroidRuntime: Caused by: android.view.InflateException: Binary XML file line #1 in com.xamarin.appwithstyleableusageruns_nativeaot:layout/main: Error inflating class crc64f75eeacfa0ca1368.MyLayout
				//  AndroidRuntime: Caused by: java.lang.reflect.InvocationTargetException
				//  AndroidRuntime: Caused by: net.dot.jni.internal.JavaProxyThrowable: System.NotSupportedException: Could not activate { PeerReference=0x7fe9706698/I IdentityHashCode=0xd12aeee Java.Type=crc64f75eeacfa0ca1368/MyLayout } for managed type 'UnnamedProject.MyLayout'.
				//  AndroidRuntime:  ---> System.Reflection.TargetInvocationException: Arg_TargetInvocationException
				//  AndroidRuntime:  ---> System.IO.FileNotFoundException: IO_FileNotFound_FileName, _Microsoft.Android.Resource.Designer
				//  AndroidRuntime: IO_FileName_Name, _Microsoft.Android.Resource.Designer
				//  DOTNET  : FATAL UNHANDLED EXCEPTION: Java.Lang.Exception: Unable to start activity ComponentInfo{com.xamarin.appwithstyleableusageruns_nativeaot/com.xamarin.appwithstyleableusageruns_nativeaot.MainActivity}
				//  DOTNET  :  ---> Java.Lang.Exception: Binary XML file line #1 in com.xamarin.appwithstyleableusageruns_nativeaot:layout/main: Binary XML file line #1 in com.xamarin.appwithstyleableusageruns_nativeaot:layout/main: Error inflating class crc64f75eeacfa0ca1368.MyLayout
				//  DOTNET  :  ---> Java.Lang.Exception: Binary XML file line #1 in com.xamarin.appwithstyleableusageruns_nativeaot:layout/main: Error inflating class crc64f75eeacfa0ca1368.MyLayout
				//  DOTNET  :  ---> Java.Lang.ReflectiveOperationException: Exception_WasThrown, Java.Lang.ReflectiveOperationException
				//  DOTNET  :  ---> System.NotSupportedException: Could not activate { PeerReference=0x7fe9706698/I IdentityHashCode=0xd12aeee Java.Type=crc64f75eeacfa0ca1368/MyLayout } for managed type 'UnnamedProject.MyLayout'.
				//  DOTNET  :  ---> System.Reflection.TargetInvocationException: Arg_TargetInvocationException
				//  DOTNET  :  ---> System.IO
				//  eruns_nativeaot: No implementation found for void mono.android.Runtime.propagateUncaughtException(java.lang.Thread, java.lang.Throwable) (tried Java_mono_android_Runtime_propagateUncaughtException and Java_mono_android_Runtime_propagateUncaughtException__Ljava_lang_Thread_2Ljava_lang_Throwable_2) - is the library loaded, e.g. System.loadLibrary?
				Assert.Ignore ("NativeAOT is broken without string-based typemaps");
			}

			var rootPath = Path.Combine (Root, "temp", TestName);
			var lib = new XamarinAndroidLibraryProject () {
				IsRelease = isRelease,
				ProjectName = "Styleable.Library"
			};
			lib.SetRuntime (runtime);

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

			proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
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

			string[] abis = runtime switch {
				AndroidRuntime.CoreCLR => new string [] { "arm64-v8a", "x86_64" },
				AndroidRuntime.NativeAOT => new string [] { "arm64-v8a", "x86_64" },
				AndroidRuntime.MonoVM => new string [] { "armeabi-v7a", "arm64-v8a", "x86", "x86_64" },
				_ => throw new NotSupportedException ($"Unsupported runtime {runtime}")
			};

			proj.SetAndroidSupportedAbis (abis);
			var libBuilder = CreateDllBuilder (Path.Combine (rootPath, lib.ProjectName));
			Assert.IsTrue (libBuilder.Build (lib), "Library should have built succeeded.");
			builder = CreateApkBuilder (Path.Combine (rootPath, proj.ProjectName));

			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");

			Dictionary<string, string>? environmentVariables = null;
			if (runtime == AndroidRuntime.CoreCLR && !isRelease && useStringTypeMaps) {
				// The variable must have content to enable string-based typemaps
				environmentVariables = new (StringComparer.Ordinal) {
					{"CI_TYPEMAP_DEBUG_USE_STRINGS", "yes"}
				};
			}

			RunProjectAndAssert (proj, builder, environmentVariables: environmentVariables);

			var didStart = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "startup-logcat.log"));
			Assert.IsTrue (didStart, "Activity should have started.");
		}

		[Test]
		public void CheckXamarinFormsAppDeploysAndAButtonWorks ([Values] AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			// TODO: fix for NativeAOT. Currently fails with:
			//
			//  DOTNET  : FATAL UNHANDLED EXCEPTION: System.InvalidCastException: Unable to convert instance of type 'AndroidX.AppCompat.Widget.AppCompatImageButton' to type 'AndroidX.AppCompat.Widget.Toolbar'.
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("NativeAOT type mapping fails");
			}

			string packageName = PackageUtils.MakePackageName (runtime);
			var proj = new XamarinFormsAndroidApplicationProject (packageName: packageName) {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetAndroidSupportedAbis (DeviceAbi);
			var builder = CreateApkBuilder (packageName: packageName);

			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			builder.BuildLogFile = "install.log";
			Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");

			ClearAdbLogcat ();
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
		public void SkiaSharpCanvasBasedAppRuns ([Values] bool isRelease, [Values] bool addResource, [Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var app = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime, "SkiaSharpCanvasTest")) {
				IsRelease = isRelease,
				PackageReferences = {
					KnownPackages.SkiaSharp,
					KnownPackages.SkiaSharp_Views,
					KnownPackages.AndroidXAppCompat,
					KnownPackages.AndroidXAppCompatResources,
				},
			};
			app.SetRuntime (runtime);
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
				// TODO: fix for NativeAOT
				if (!addResource && runtime != AndroidRuntime.NativeAOT) {
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
		public void CheckResouceIsOverridden ([Values] AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var library = new XamarinAndroidLibraryProject () {
				IsRelease = isRelease,
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
			library.SetRuntime (runtime);
			var library2 = new XamarinAndroidLibraryProject () {
				IsRelease = isRelease,
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
			library2.SetRuntime (runtime);
			var app = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime, "ResourceTest")) {
				IsRelease = isRelease,
				References = {
					new BuildItem.ProjectReference ("..\\Library1\\Library1.csproj"),
					new BuildItem.ProjectReference ("..\\Library2\\Library2.csproj"),
				},
			};
			app.SetRuntime (runtime);
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
		public void DotNetInstallAndRunPreviousSdk (
				[Values] bool isRelease,
				[Values ("net9.0-android")] string targetFramework,
				[Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			// Mono-only test for the moment (until net10 or later is the "previous" framework)
			if (runtime != AndroidRuntime.MonoVM) {
				Assert.Ignore ("Mono-only test util net9 is no longer the 'previous' SDK");
			}

			var proj = new XamarinFormsAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				TargetFramework = targetFramework,
				IsRelease = isRelease,
				EnableDefaultItems = true,
			};
			proj.SetRuntime (runtime);

			// Requires 32-bit ABIs
			proj.SetAndroidSupportedAbis (["armeabi-v7a", "arm64-v8a", "x86", "x86_64"]);

			var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "`dotnet build` should succeed");
			RunProjectAndAssert (proj, builder);

			WaitForPermissionActivity (Path.Combine (Root, builder.ProjectDirectory, "permission-logcat.log"));
			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30);
			Assert.IsTrue(didLaunch, "Activity should have started.");
		}

		[Test]
		public void DotNetInstallAndRunMinorAPILevels (
				[Values] bool isRelease,
				[Values ("net10.0-android36.1")] string targetFramework,
				[Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				TargetFramework = targetFramework,
				IsRelease = isRelease,
				ExtraNuGetConfigSources = {
					Path.Combine (XABuildPaths.BuildOutputDirectory, "nuget-unsigned"),
				}
			};
			proj.SetRuntime (runtime);

			// TODO: update on new minor API levels to use an introduced minor API
			proj.MainActivity = proj.DefaultMainActivity
				.Replace ("//${USINGS}", "using Android.Telecom;\nusing Android.Graphics.Pdf.Component;")
				.Replace ("//${AFTER_ONCREATE}", """
					if (OperatingSystem.IsAndroidVersionAtLeast (36, 1)) {
						Console.WriteLine ($"TelecomManager.ActionCallBack={TelecomManager.ActionCallBack}");
					} else {
						Console.WriteLine ("TelecomManager.ActionCallBack not available");
					}
				""")
				.Replace ("//${AFTER_MAINACTIVITY}", """
					#pragma warning disable CA1416 // Type only available on Android 36.1 and later
					class MyTextObjectFont : PdfPageTextObjectFont
					{
						public MyTextObjectFont (PdfPageTextObjectFont font) : base (font)
						{
						}
					}
					#pragma warning restore CA1416 // Type only available on Android 36.1 and later
				""");

			var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "`dotnet build` should succeed");
			builder.AssertHasNoWarnings ();
			RunProjectAndAssert (proj, builder);

			WaitForPermissionActivity (Path.Combine (Root, builder.ProjectDirectory, "permission-logcat.log"));
			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30);
			Assert.IsTrue(didLaunch, "Activity should have started.");
		}

		[Test]
		public void TypeAndMemberRemapping ([Values] bool isRelease, [Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			// TODO: fix for NativeAOT, if possible
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("Type and member mapping is currently unsupported under NativeAOT");
			}

			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
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
			proj.SetRuntime (runtime);
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
		public void SupportDesugaringStaticInterfaceMethods ([Values] AndroidRuntime runtime)
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			// TODO: fix for NativeAOT, if possible. Currently fails with:
			//
			//  Process: com.xamarin.supportdesugaringstaticinterfacemethods_nativeaot, PID: 13888
			//  java.lang.NoSuchMethodError: no static method "Lexample/StaticMethodsInterface;.getValue()I"
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("Currently broken on NativeAOT");
			}

			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
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
			proj.SetRuntime (runtime);

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
		public void FastDeployEnvironmentFiles ([Values] bool isRelease, [Values] bool embedAssembliesIntoApk, [Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			if (!isRelease && !embedAssembliesIntoApk) {
				Assert.Ignore ("Not a FastDev configuration");
			}

			if (embedAssembliesIntoApk) {
				AssertCommercialBuild ();
			}

			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				ProjectName = nameof (FastDeployEnvironmentFiles),
				RootNamespace = nameof (FastDeployEnvironmentFiles),
				IsRelease = isRelease,
				EmbedAssembliesIntoApk = embedAssembliesIntoApk,
				EnableDefaultItems = true,
				OtherBuildItems = {
					new BuildItem("AndroidEnvironment", "env.txt") {
						TextContent = () => @"Foo=Bar
Bar34=Foo55
Empty=
MONO_GC_PARAMS=bridge-implementation=new",
					}
				}
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("DiagnosticAddress", "127.0.0.1");
			proj.SetProperty ("DiagnosticPort", "9000");
			proj.SetProperty ("DiagnosticSuspend", "false");
			proj.SetProperty ("DiagnosticListenMode", "connect");
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", @"
		Console.WriteLine (""Foo="" + Environment.GetEnvironmentVariable(""Foo""));
		Console.WriteLine (""Bar34="" + Environment.GetEnvironmentVariable(""Bar34""));
		Console.WriteLine (""Empty="" + Environment.GetEnvironmentVariable(""Empty""));
		Console.WriteLine (""MONO_GC_PARAMS="" + Environment.GetEnvironmentVariable(""MONO_GC_PARAMS""));
		Console.WriteLine (""DOTNET_MODIFIABLE_ASSEMBLIES="" + Environment.GetEnvironmentVariable(""DOTNET_MODIFIABLE_ASSEMBLIES""));
		Console.WriteLine (""DOTNET_DiagnosticPorts="" + Environment.GetEnvironmentVariable(""DOTNET_DiagnosticPorts""));
		");
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
					"The Environment variable \"Foo\" was not set to expected value \"Bar\"."
			);
			StringAssert.Contains (
					"Bar34=Foo55",
					logcatOutput,
					"The Environment variable \"Bar34\" was not set to expected value \"Foo55\"."
			);
			// NOTE: `Empty=` test case is to ensure a blank value doesn't cause a build error
			StringAssert.Contains (
					"Empty=",
					logcatOutput,
					"The Environment variable \"Empty\" was not set."
			);
			StringAssert.Contains (
					"MONO_GC_PARAMS=bridge-implementation=new",
					logcatOutput,
					"The Environment variable \"MONO_GC_PARAMS\" was not set to expected value \"bridge-implementation=new\"."
			);
			StringAssert.Contains (
					"DOTNET_DiagnosticPorts=127.0.0.1:9000,connect,nosuspend",
					logcatOutput,
					"The Environment variable \"DOTNET_DiagnosticPorts\" was not set to expected value \"127.0.0.1:9000,connect,nosuspend\"."
			);
			// NOTE: set when $(UseInterpreter) is true, default for Debug mode
			if (!isRelease) {
				StringAssert.Contains (
						"DOTNET_MODIFIABLE_ASSEMBLIES=Debug",
						logcatOutput,
						"The Environment variable \"DOTNET_MODIFIABLE_ASSEMBLIES\" was not set."
				);
			}
		}

		[Test]
		public void FixLegacyResourceDesignerStep ([Values] bool isRelease, [Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}
			string previousTargetFramework = "net9.0-android";

			// Don't call SetRuntime on library projects (at least until "previous" framework bumps to at least 10.0)
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
			proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
				ProjectName = "MyApp",
			};
			proj.SetRuntime (runtime);
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
		public void MicrosoftIntune ([Values] bool isRelease, [Values] AndroidRuntime runtime)
		{
			Assert.Ignore ("https://github.com/xamarin/xamarin-android/issues/8548");
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
				PackageReferences = {
					KnownPackages.AndroidXAppCompat,
					KnownPackages.Microsoft_Intune_Maui_Essentials_android,
				},
			};
			proj.SetRuntime (runtime);
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
		public void GradleFBProj ([Values] bool isRelease, [Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

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
    compileSdk = {XABuildConfig.AndroidDefaultTargetDotnetApiLevel.Major}
    defaultConfig {{
        minSdk = {XABuildConfig.AndroidMinimumDotNetApiLevel.Major}
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

			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
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
			proj.SetRuntime (runtime);
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

		[Test]
		public void NativeAOTSample ()
		{
			string [] properties = [
				$"AndroidNdkDirectory={AndroidNdkPath}",
				"Configuration=Release",
			];
			var projectDirectory = Path.Combine (XABuildPaths.TopDirectory, "samples", "NativeAOT");
			try {
				var dotnet = new DotNetCLI (Path.Combine (projectDirectory, "NativeAOT.csproj"));
				Assert.IsTrue (dotnet.Build (target: "Run", parameters: properties), "`dotnet build -t:Run` should succeed");

				bool didLaunch = WaitForActivityToStart ("my", "MainActivity",
					Path.Combine (projectDirectory, "logcat.log"), 30);
				Assert.IsTrue (didLaunch, "Activity should have started.");
			} catch {
				foreach (var file in Directory.GetFiles (projectDirectory, "*.log", SearchOption.AllDirectories)) {
					TestContext.AddTestAttachment (file);
				}
				foreach (var bl in Directory.GetFiles (projectDirectory, "*.binlog", SearchOption.AllDirectories)) {
					TestContext.AddTestAttachment (bl);
				}
				throw;
			}
		}

		[Test]
		public void AppStartsWithManagedMarshalMethodsLookupEnabled ([Values] AndroidRuntime runtime)
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime)) {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidUseMarshalMethods", "true");
			proj.SetProperty ("_AndroidUseManagedMarshalMethodsLookup", "true");

			using var builder = CreateApkBuilder ();
			builder.Save (proj);

			var dotnet = new DotNetCLI (Path.Combine (Root, builder.ProjectDirectory, proj.ProjectFilePath));
			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");
			Assert.IsTrue (dotnet.Run (), "`dotnet run --no-build` should succeed");

			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 30);
			Assert.IsTrue (didLaunch, "Activity should have started.");
		}
	}
}
