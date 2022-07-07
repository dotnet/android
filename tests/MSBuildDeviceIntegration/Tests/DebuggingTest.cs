using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.Collections.Generic;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class DebuggingTest : DeviceTest {
		[TearDown]
		public void ClearDebugProperties ()
		{
			ClearDebugProperty ();
		}

		void SetTargetFrameworkAndManifest(XamarinAndroidApplicationProject proj, Builder builder)
		{
			builder.LatestTargetFrameworkVersion (out string apiLevel);
			proj.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{proj.PackageName}"">
	<uses-sdk android:minSdkVersion=""24"" android:targetSdkVersion=""{apiLevel}"" />
	<application android:label=""${{PROJECT_NAME}}"">
	</application >
</manifest>";
		}

		int FindTextInFile (string file, string text) {
			int lineNumber = 1;
			foreach (var line in File.ReadAllLines (file)) {
				if (line.Contains (text)) {
					return lineNumber;
				}
				lineNumber++;
			}
			Console.WriteLine ($"Could not find '{text}' in '{file}'");
			return -1;
		}

		[Test]
		[Category ("Node-3")]
		public void ApplicationRunsWithoutDebugger ([Values (false, true)] bool isRelease, [Values (false, true)] bool extractNativeLibs, [Values (false, true)] bool useEmbeddedDex)
		{
			AssertHasDevices ();
			SwitchUser ();

			var proj = new XamarinFormsAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			if (isRelease || !CommercialBuildAvailable) {
				proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86", "x86_64");
			}
			proj.SetDefaultTargetDevice ();
			if (Builder.UseDotNet && isRelease) {
				// bundle tool does NOT support embeddedDex files it seems.
				useEmbeddedDex = false;
			}
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				SetTargetFrameworkAndManifest (proj, b);
				proj.AndroidManifest = proj.AndroidManifest.Replace ("<application ", $"<application android:extractNativeLibs=\"{extractNativeLibs.ToString ().ToLowerInvariant ()}\" android:useEmbeddedDex=\"{useEmbeddedDex.ToString ().ToLowerInvariant ()}\" ");
				Assert.True (b.Install (proj), "Project should have installed.");
				var manifest = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "AndroidManifest.xml");
				AssertExtractNativeLibs (manifest, extractNativeLibs);
				ClearAdbLogcat ();
				b.BuildLogFile = "run.log";
				if (CommercialBuildAvailable)
					Assert.True (b.RunTarget (proj, "_Run"), "Project should have run.");
				else
					AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");

				Assert.True (WaitForActivityToStart (proj.PackageName, "MainActivity",
					Path.Combine (Root, b.ProjectDirectory, "logcat.log"), 30), "Activity should have started.");
				b.BuildLogFile = "uninstall.log";
				Assert.True (b.Uninstall (proj), "Project should have uninstalled.");
			}
		}

		[Test]
		[Category ("Node-3")]
		public void ClassLibraryMainLauncherRuns ([Values (true, false)] bool preloadAssemblies)
		{
			AssertHasDevices ();
			SwitchUser ();

			var path = Path.Combine ("temp", TestName);

			var app = new XamarinAndroidApplicationProject {
				ProjectName = "MyApp",
			};
			if (!CommercialBuildAvailable) {
				app.SetAndroidSupportedAbis ("armeabi-v7a", "x86", "x86_64");
			}
			app.SetDefaultTargetDevice ();
			app.SetProperty ("AndroidEnablePreloadAssemblies", preloadAssemblies.ToString ());

			var lib = new XamarinAndroidLibraryProject {
				ProjectName = "MyLibrary"
			};
			lib.Sources.Add (new BuildItem.Source ("MainActivity.cs") {
				TextContent = () => lib.ProcessSourceTemplate (app.DefaultMainActivity).Replace ("${JAVA_PACKAGENAME}", app.JavaPackageName),
			});
			lib.AndroidResources.Clear ();
			foreach (var resource in app.AndroidResources) {
				lib.AndroidResources.Add (resource);
			}
			var reference = $"..\\{lib.ProjectName}\\{lib.ProjectName}.csproj";
			app.References.Add (new BuildItem.ProjectReference (reference, lib.ProjectName, lib.ProjectGuid));

			// Remove the default MainActivity.cs & AndroidResources
			app.AndroidResources.Clear ();
			app.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\foo.xml") {
				TextContent = () =>
@"<?xml version=""1.0"" encoding=""utf-8""?>
<LinearLayout
  xmlns:android=""http://schemas.android.com/apk/res/android""
  android:layout_width=""fill_parent""
  android:layout_height=""wrap_content""
/>"
			});
			app.Sources.Remove (app.GetItem ("MainActivity.cs"));

			using (var libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName)))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				SetTargetFrameworkAndManifest (app, appBuilder);
				Assert.IsTrue (libBuilder.Build (lib), "library build should have succeeded.");
				Assert.True (appBuilder.Install (app), "app should have installed.");
				ClearAdbLogcat ();
				appBuilder.BuildLogFile = "run.log";
				if (CommercialBuildAvailable)
					Assert.True (appBuilder.RunTarget (app, "_Run"), "Project should have run.");
				else
					AdbStartActivity ($"{app.PackageName}/{app.JavaPackageName}.MainActivity");

				Assert.True (WaitForActivityToStart (app.PackageName, "MainActivity",
					Path.Combine (Root, appBuilder.ProjectDirectory, "logcat.log"), 30), "Activity should have started.");
			}
		}

#pragma warning disable 414
		static object [] DebuggerCustomAppTestCases = new object [] {
			new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies",
				/* activityStarts */     true,
			},
			new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies",
				/* activityStarts */     true,
			},
			new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies:Dexes",
				/* activityStarts */     true,
			},
			new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies:Dexes",
				/* activityStarts */     false,
			},
		};
#pragma warning restore 414

		[Test, Category ("Debugger"), Category ("Node-4")]
		[TestCaseSource (nameof (DebuggerCustomAppTestCases))]
		[Retry(5)]
		public void CustomApplicationRunsWithDebuggerAndBreaks (bool embedAssemblies, string fastDevType, bool activityStarts)
		{
			AssertCommercialBuild ();
			AssertHasDevices ();
			SwitchUser ();

			var path = Path.Combine (Root, "temp", TestName);
			if (Directory.Exists (path)) {
				TestContext.Out.WriteLine ($"Deleting previous run at '{path}'");
				Directory.Delete (path,recursive:true);
			}

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = false,
				AndroidFastDeploymentType = fastDevType,
			};
			proj.SetAndroidSupportedAbis ("armeabi-v7a", "x86", "x86_64");
			proj.SetProperty ("EmbedAssembliesIntoApk", embedAssemblies.ToString ());
			proj.SetDefaultTargetDevice ();
			proj.Sources.Add (new BuildItem.Source ("MyApplication.cs") {
				TextContent = () => proj.ProcessSourceTemplate (@"using System;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Widget;

namespace ${ROOT_NAMESPACE} {
	[Application]
	public class MyApplication : Application {
		public MyApplication (IntPtr handle, JniHandleOwnership jniHandle)
			: base (handle, jniHandle)
		{
		}

		public override void OnCreate ()
		{
			base.OnCreate ();
	 	}
	}
}
"),
			});
			using (var b = CreateApkBuilder (path)) {
				SetTargetFrameworkAndManifest (proj, b);
				Assert.True (b.Install (proj), "Project should have installed.");

				int breakcountHitCount = 0;
				ManualResetEvent resetEvent = new ManualResetEvent (false);
				var sw = new Stopwatch ();
				// setup the debugger
				var session = new SoftDebuggerSession ();
				session.Breakpoints = new BreakpointStore ();
				string file =  Path.Combine (Root, b.ProjectDirectory, "MainActivity.cs");
				int line = FindTextInFile (file, "base.OnCreate (bundle);");
				session.Breakpoints.Add (file, line);
				file =  Path.Combine (Root, b.ProjectDirectory, "MyApplication.cs");
				line = FindTextInFile (file, "base.OnCreate ();");
				session.Breakpoints.Add (file, line);
				session.TargetHitBreakpoint += (sender, e) => {
					TestContext.WriteLine ($"BREAK {e.Type}, {e.Backtrace.GetFrame (0)}");
					breakcountHitCount++;
					session.Continue ();
				};
				var rnd = new Random ();
				int port = rnd.Next (10000, 20000);
				TestContext.Out.WriteLine ($"{port}");
				var args = new SoftDebuggerConnectArgs ("", IPAddress.Loopback, port) {
					MaxConnectionAttempts = 2000, // we need a long delay here to get a reliable connection
				};
				var startInfo = new SoftDebuggerStartInfo (args) {
					WorkingDirectory = Path.Combine (b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets"),
				};
				var options = new DebuggerSessionOptions () {
					EvaluationOptions = EvaluationOptions.DefaultOptions,
				};
				options.EvaluationOptions.UseExternalTypeResolver = true;
				ClearAdbLogcat ();
				b.BuildLogFile = "run.log";
				Assert.True (b.RunTarget (proj, "_Run", doNotCleanupOnUpdate: true, parameters: new string [] {
					$"AndroidSdbTargetPort={port}",
					$"AndroidSdbHostPort={port}",
					"AndroidAttachDebugger=True",
				}), "Project should have run.");

				session.LogWriter += (isStderr, text) => { Console.WriteLine (text); };
				session.OutputWriter += (isStderr, text) => { Console.WriteLine (text); };
				session.DebugWriter += (level, category, message) => { Console.WriteLine (message); };
				// do we expect the app to start?
				Assert.AreEqual (activityStarts, WaitForDebuggerToStart (Path.Combine (Root, b.ProjectDirectory, "logcat.log")), "Debugger should have started");
				if (!activityStarts)
					return;
				Assert.False (session.HasExited, "Target should not have exited.");
				session.Run (startInfo, options);
				var expectedTime = TimeSpan.FromSeconds (1);
				var actualTime = ProfileFor (() => session.IsConnected);
				Assert.True (session.IsConnected, "Debugger should have connected but it did not.");
				TestContext.Out.WriteLine ($"Debugger connected in {actualTime}");
				Assert.LessOrEqual (actualTime, expectedTime, $"Debugger should have connected within {expectedTime} but it took {actualTime}.");
				// we need to wait here for a while to allow the breakpoints to hit
				// but we need to timeout
				TimeSpan timeout = TimeSpan.FromSeconds (60);
				while (session.IsConnected && breakcountHitCount < 2 && timeout >= TimeSpan.Zero) {
					Thread.Sleep (10);
					timeout = timeout.Subtract (TimeSpan.FromMilliseconds (10));
				}
				WaitFor (2000);
				int expected = 2;
				Assert.AreEqual (expected, breakcountHitCount, $"Should have hit {expected} breakpoints. Only hit {breakcountHitCount}");
				b.BuildLogFile = "uninstall.log";
				Assert.True (b.Uninstall (proj), "Project should have uninstalled.");
				session.Exit ();
			}
		}

#pragma warning disable 414
		static object [] DebuggerTestCases = new object [] {
			new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies",
				/* allowDeltaInstall */  false,
				/* user */		 null,
				/* debugType */          "",
			},
			new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies",
				/* allowDeltaInstall */  false,
				/* user */		 null,
				/* debugType */          "full",
			},
			new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies",
				/* allowDeltaInstall */  false,
				/* user */		 null,
				/* debugType */          "",
			},
			new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies",
				/* allowDeltaInstall */  true,
				/* user */		 null,
				/* debugType */          "",
			},
			new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies:Dexes",
				/* allowDeltaInstall */  false,
				/* user */		 null,
				/* debugType */          "",
			},
			new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies:Dexes",
				/* allowDeltaInstall */  true,
				/* user */		 null,
				/* debugType */          "",
			},
			new object[] {
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies",
				/* allowDeltaInstall */  false,
				/* user */		 DeviceTest.GuestUserName,
				/* debugType */          "",
			},
			new object[] {
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies",
				/* allowDeltaInstall */  false,
				/* user */		 DeviceTest.GuestUserName,
				/* debugType */          "",
			},
		};
#pragma warning restore 414

		[Test, Category ("SmokeTests"), Category ("Debugger"), Category ("Node-4")]
		[TestCaseSource (nameof(DebuggerTestCases))]
		[Retry (5)]
		public void ApplicationRunsWithDebuggerAndBreaks (bool embedAssemblies, string fastDevType, bool allowDeltaInstall, string username, string debugType)
		{
			AssertCommercialBuild ();
			AssertHasDevices ();
			SwitchUser ();
			WaitFor (5000);

			var path = Path.Combine (Root, "temp", TestName);
			if (Directory.Exists (path)) {
				TestContext.Out.WriteLine ($"Deleting previous run at '{path}'");
				Directory.Delete (path,recursive:true);
			}

			int userId = GetUserId (username);
			List<string> parameters = new List<string> ();
			if (userId >= 0)
				parameters.Add ($"AndroidDeviceUserId={userId}");
			if (SwitchUser (username)) {
				WaitFor (5000);
				ClearBlockingDialogs ();
				ClickButton ("", "android:id/button1", "Yes continue");
			}

			var lib = new XamarinAndroidLibraryProject {
				ProjectName = "Library1",
				Sources = {
					new BuildItem.Source ("Foo.cs") {
						TextContent = () =>
@"public class Foo
{
	public Foo ()
	{
	}
}"
					},
				},
			};

			var app = new XamarinFormsAndroidApplicationProject {
				ProjectName = "App",
				IsRelease = false,
				EmbedAssembliesIntoApk = embedAssemblies,
				AndroidFastDeploymentType = fastDevType
			};
			app.MainPage = app.MainPage.Replace ("InitializeComponent ();", "InitializeComponent (); new Foo ();");
			app.AddReference (lib);
			app.SetAndroidSupportedAbis ("armeabi-v7a", "x86", "x86_64");
			app.SetProperty (KnownProperties._AndroidAllowDeltaInstall, allowDeltaInstall.ToString ());
			if (!string.IsNullOrEmpty (debugType)) {
				app.SetProperty ("DebugType", debugType);
			}
			app.SetDefaultTargetDevice ();
			using (var libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName)))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				Assert.True (libBuilder.Build (lib), "Library should have built.");

				SetTargetFrameworkAndManifest (app, appBuilder);
				Assert.True (appBuilder.Install (app, parameters: parameters.ToArray ()), "App should have installed.");

				if (!embedAssemblies) {
					// Check that we deployed app and framework .pdb files
					StringAssertEx.ContainsRegex ($@"NotifySync CopyFile.+{app.ProjectName}\.pdb", appBuilder.LastBuildOutput,
						$"{app.ProjectName}.pdb should be deployed!");
					StringAssertEx.ContainsRegex ($@"NotifySync CopyFile.+{lib.ProjectName}\.pdb", appBuilder.LastBuildOutput,
						$"{lib.ProjectName}.pdb should be deployed!");
					StringAssertEx.ContainsRegex ($@"NotifySync CopyFile.+Mono.Android\.pdb", appBuilder.LastBuildOutput,
						$"Mono.Android.pdb should be deployed!");
				}

				int breakcountHitCount = 0;
				ManualResetEvent resetEvent = new ManualResetEvent (false);
				var sw = new Stopwatch ();
				// setup the debugger
				var session = new SoftDebuggerSession ();

				session.Breakpoints = new BreakpointStore ();
				string file =  Path.Combine (Root, appBuilder.ProjectDirectory, "MainActivity.cs");
				int line = FindTextInFile (file, "base.OnCreate (savedInstanceState);");
				session.Breakpoints.Add (file, line);

				file =  Path.Combine (Root, appBuilder.ProjectDirectory, "MainPage.xaml.cs");
				line = FindTextInFile (file, "InitializeComponent ();");
				session.Breakpoints.Add (file, line);

				file =  Path.Combine (Root, appBuilder.ProjectDirectory, "MainPage.xaml.cs");
				line = FindTextInFile (file, "Console.WriteLine (");
				session.Breakpoints.Add (file, line);

				file =  Path.Combine (Root, appBuilder.ProjectDirectory, "App.xaml.cs");
				line = FindTextInFile (file, "InitializeComponent ();");
				session.Breakpoints.Add (file, line);

				file =  Path.Combine (Root, libBuilder.ProjectDirectory, "Foo.cs");
				line = FindTextInFile (file, "public Foo ()");
				// Add one to the line so we get the '{' under the constructor
				session.Breakpoints.Add (file, line++);

				session.TargetHitBreakpoint += (sender, e) => {
					TestContext.WriteLine ($"BREAK {e.Type}, {e.Backtrace.GetFrame (0)}");
					breakcountHitCount++;
					session.Continue ();
				};
				var rnd = new Random ();
				int port = rnd.Next (10000, 20000);
				TestContext.Out.WriteLine ($"{port}");
				var args = new SoftDebuggerConnectArgs ("", IPAddress.Loopback, port) {
					MaxConnectionAttempts = 2000,
				};
				var startInfo = new SoftDebuggerStartInfo (args) {
					WorkingDirectory = Path.Combine (appBuilder.ProjectDirectory, app.IntermediateOutputPath, "android", "assets"),
				};
				var options = new DebuggerSessionOptions () {
					EvaluationOptions = EvaluationOptions.DefaultOptions,
				};
				options.EvaluationOptions.UseExternalTypeResolver = true;
				ClearAdbLogcat ();
				appBuilder.BuildLogFile = "run.log";

				parameters.Add ($"AndroidSdbTargetPort={port}");
				parameters.Add ($"AndroidSdbHostPort={port}");
				parameters.Add ("AndroidAttachDebugger=True");

				Assert.True (appBuilder.RunTarget (app, "_Run", doNotCleanupOnUpdate: true,
					parameters: parameters.ToArray ()), "Project should have run.");

				session.LogWriter += (isStderr, text) => {
					TestContext.Out.WriteLine (text);
				};
				session.OutputWriter += (isStderr, text) => {
					TestContext.Out.WriteLine (text);
				};
				session.DebugWriter += (level, category, message) => {
					TestContext.Out.WriteLine (message);
				};
				Assert.IsTrue (WaitForDebuggerToStart (Path.Combine (Root, appBuilder.ProjectDirectory, "logcat.log")), "Debugger should have started");
				session.Run (startInfo, options);
				TestContext.Out.WriteLine ($"Detected debugger startup in log");
				Assert.False (session.HasExited, "Target should not have exited.");
				WaitFor (TimeSpan.FromSeconds (30), () => session.IsConnected );
				Assert.True (session.IsConnected, "Debugger should have connected but it did not.");
				// we need to wait here for a while to allow the breakpoints to hit
				// but we need to timeout
				TestContext.Out.WriteLine ($"Debugger connected.");
				TimeSpan timeout = TimeSpan.FromSeconds (60);
				int expected = 4;
				while (session.IsConnected && breakcountHitCount < 3 && timeout >= TimeSpan.Zero) {
					Thread.Sleep (10);
					timeout = timeout.Subtract (TimeSpan.FromMilliseconds (10));
				}
				WaitFor (2000);
				Assert.AreEqual (expected, breakcountHitCount, $"Should have hit {expected} breakpoints. Only hit {breakcountHitCount}");
				breakcountHitCount = 0;
				ClearAdbLogcat ();
				ClearBlockingDialogs ();
				Assert.True (ClickButton (app.PackageName, "myXFButton", "CLICK ME"), "Button should have been clicked!");
				while (session.IsConnected && breakcountHitCount < 1 && timeout >= TimeSpan.Zero) {
					Thread.Sleep (10);
					timeout = timeout.Subtract (TimeSpan.FromMilliseconds (10));
				}
				expected = 1;
				Assert.AreEqual (expected, breakcountHitCount, $"Should have hit {expected} breakpoints. Only hit {breakcountHitCount}");
				appBuilder.BuildLogFile = "uninstall.log";
				Assert.True (appBuilder.Uninstall (app), "Project should have uninstalled.");
				session.Exit ();
			}
		}
	}
}
