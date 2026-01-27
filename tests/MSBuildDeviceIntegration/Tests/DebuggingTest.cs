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
using Microsoft.Build.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class DebuggingTest : DeviceTest
	{
		const int DEBUGGER_MAX_CONNECTIONS = 100;
		const int DEBUGGER_CONNECTION_TIMEOUT = 3000;

		[TearDown]
		public void ClearDebugProperties ()
		{
			ClearDebugProperty ();
		}

		void SetTargetFrameworkAndManifest(XamarinAndroidApplicationProject proj, Builder builder, int? apiLevelOverride)
		{
			builder.LatestTargetFrameworkVersion (out string apiLevel);
			proj.SupportedOSPlatformVersion = "24";
			proj.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""{proj.PackageName}"">
	<uses-sdk android:targetSdkVersion=""{apiLevelOverride?.ToString () ?? apiLevel}"" />
	<application android:label=""${{PROJECT_NAME}}"">
	</application >
</manifest>";
		}

		int FindTextInFile (string file, string text)
		{
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
		public void ApplicationRunsWithoutDebugger ([Values] bool isRelease, [Values] bool extractNativeLibs, [Values] bool useEmbeddedDex, [Values] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			// TODO: NativeAOT fails with the following exception:
			//
			// FATAL UNHANDLED EXCEPTION: System.InvalidCastException: Unable to convert instance of type 'AndroidX.AppCompat.Widget.AppCompatImageButton' to type 'AndroidX.AppCompat.Widget.Toolbar'.
			//    at Java.Interop.JavaObjectExtensions._JavaCast[TResult](IJavaObject) + 0x190
			//    at Android.Runtime.Extensions.JavaCast[TResult](IJavaObject) + 0x18
			//    at Xamarin.Forms.Platform.Android.FormsAppCompatActivity.OnCreate(Bundle, ActivationFlags) + 0x5bc
			//    at UnnamedProject.MainActivity.OnCreate(Bundle savedInstanceState) + 0x4c
			//    at Android.App.Activity.n_OnCreate_Landroid_os_Bundle_(IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState) + 0x7c
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("NativeAOT currently crashes with an exception.");
			}

			SwitchUser ();

			var proj = new XamarinFormsAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			if (isRelease || !TestEnvironment.CommercialBuildAvailable) {
				proj.SetAndroidSupportedAbis (DeviceAbi);
			}
			proj.SetDefaultTargetDevice ();
			if (isRelease) {
				// bundle tool does NOT support embeddedDex files it seems.
				useEmbeddedDex = false;
			}
			using (var b = CreateApkBuilder ()) {
				SetTargetFrameworkAndManifest (proj, b, null);
				proj.AndroidManifest = proj.AndroidManifest.Replace ("<application ", $"<application android:extractNativeLibs=\"{extractNativeLibs.ToString ().ToLowerInvariant ()}\" android:useEmbeddedDex=\"{useEmbeddedDex.ToString ().ToLowerInvariant ()}\" ");
				Assert.True (b.Install (proj), "Project should have installed.");
				var manifest = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "AndroidManifest.xml");
				AssertExtractNativeLibs (manifest, extractNativeLibs);
				RunProjectAndAssert (proj, b);
				Assert.True (WaitForActivityToStart (proj.PackageName, "MainActivity",
					Path.Combine (Root, b.ProjectDirectory, "logcat.log"), 30), "Activity should have started.");
				b.BuildLogFile = "uninstall.log";
				Assert.True (b.Uninstall (proj), "Project should have uninstalled.");
			}
		}

		[Test]
		public void ClassLibraryMainLauncherRuns ([Values] bool preloadAssemblies, [Values] AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			// TODO: NativeAOT currently dies with a Java android.os.DeadObjectException exception (GC issue?):
			//
			// Exception thrown during dispatchAppVisibility Window{74689fa u0 com.xamarin.classlibrarymainlauncherruns/com.xamarin.classlibrarymainlauncherruns.MainActivity EXITING}
			// android.os.DeadObjectException
			//         at android.os.BinderProxy.transactNative(Native Method)
			//         at android.os.BinderProxy.transact(BinderProxy.java:592)
			//         at android.view.IWindow$Stub$Proxy.dispatchAppVisibility(IWindow.java:538)
			//         at com.android.server.wm.WindowState.sendAppVisibilityToClients(WindowState.java:3183)
			//         at com.android.server.wm.WindowContainer.sendAppVisibilityToClients(WindowContainer.java:1233)
			//         at com.android.server.wm.WindowToken.setClientVisible(WindowToken.java:394)
			//         at com.android.server.wm.ActivityRecord.commitVisibility(ActivityRecord.java:5546)
			//         at com.android.server.wm.Transition.finishTransition(Transition.java:1485)
			//         at com.android.server.wm.TransitionController.finishTransition(TransitionController.java:1048)
			//         at com.android.server.wm.WindowOrganizerController.finishTransition(WindowOrganizerController.java:514)
			//         at android.window.IWindowOrganizerController$Stub.onTransact(IWindowOrganizerController.java:270)
			//         at com.android.server.wm.WindowOrganizerController.onTransact(WindowOrganizerController.java:230)
			//         at android.os.Binder.execTransactInternal(Binder.java:1446)
			//         at android.os.Binder.execTransact(Binder.java:1385)
			if (runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("NativeAOT currently dies at startup with a Java android.os.DeadObjectException exception");
			}

			SwitchUser ();

			var path = Path.Combine ("temp", TestName);

			var app = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				ProjectName = "MyApp",
			};
			app.SetRuntime (runtime);
			if (!TestEnvironment.CommercialBuildAvailable) {
				app.SetAndroidSupportedAbis (DeviceAbi);
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
				SetTargetFrameworkAndManifest (app, appBuilder, null);
				Assert.IsTrue (libBuilder.Build (lib), "library build should have succeeded.");
				Assert.True (appBuilder.Install (app), "app should have installed.");
				RunProjectAndAssert (app, appBuilder);
				Assert.True (WaitForActivityToStart (app.PackageName, "MainActivity",
					Path.Combine (Root, appBuilder.ProjectDirectory, "logcat.log"), 30), "Activity should have started.");
			}
		}

		static IEnumerable<object[]> Get_CustomApplicationRunsWithDebuggerAndBreaks_Data ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in Enum.GetValues (typeof (AndroidRuntime))) {
				// TODO: once CoreCLR debugging works, this needs to be adjusted accordingly
				if (runtime != AndroidRuntime.MonoVM) {
					continue;
				}

				AddTestData (
					embedAssemblies: true,
					activityStarts:  true,
					packageFormat:   "apk",
					runtime:         runtime
				);

				AddTestData (
					embedAssemblies: false,
					activityStarts:  true,
					packageFormat:   "apk",
					runtime:         runtime
				);

				AddTestData (
					embedAssemblies: true,
					activityStarts:  true,
					packageFormat:   "aab",
					runtime:         runtime
				);

				AddTestData (
					embedAssemblies: false,
					activityStarts:  true,
					packageFormat:   "aab",
					runtime:         runtime
				);
			}

			return ret;

			void AddTestData (bool embedAssemblies, bool activityStarts, string packageFormat, AndroidRuntime runtime)
			{
				ret.Add (new object[] {
					embedAssemblies,
					activityStarts,
					packageFormat,
					runtime,
				});
			}
		}

		// MonoVM-only test for the moment.
		[Test, Category ("Debugger")]
		[TestCaseSource (nameof (Get_CustomApplicationRunsWithDebuggerAndBreaks_Data))]
		[Retry(5)]
		public void CustomApplicationRunsWithDebuggerAndBreaks (bool embedAssemblies, bool activityStarts, string packageFormat, AndroidRuntime runtime)
		{
			const bool isRelease = false;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			AssertCommercialBuild ();
			SwitchUser ();

			var path = Path.Combine (Root, "temp", TestName);
			if (Directory.Exists (path)) {
				TestContext.Out.WriteLine ($"Deleting previous run at '{path}'");
				Directory.Delete (path,recursive:true);
			}

			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
			};

			proj.SetRuntime (runtime);
			proj.SetAndroidSupportedAbis (DeviceAbi);
			proj.SetProperty ("EmbedAssembliesIntoApk", embedAssemblies.ToString ());
			proj.SetProperty ("AndroidPackageFormat", packageFormat);
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
				SetTargetFrameworkAndManifest (proj, b, null);
				Assert.True (b.Install (proj), "Project should have installed.");

				int breakcountHitCount = 0;
				ManualResetEvent resetEvent = new ManualResetEvent (false);
				var sw = new Stopwatch ();
				// setup the debugger
				var session = new SoftDebuggerSession ();
				try {
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
						MaxConnectionAttempts = DEBUGGER_MAX_CONNECTIONS, // we need a long delay here to get a reliable connection
						TimeBetweenConnectionAttempts = DEBUGGER_CONNECTION_TIMEOUT,
					};
					var startInfo = new SoftDebuggerStartInfo (args) {
						WorkingDirectory = Path.Combine (b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets"),
					};
					var options = new DebuggerSessionOptions () {
						EvaluationOptions = EvaluationOptions.DefaultOptions,
					};
					options.EvaluationOptions.UseExternalTypeResolver = true;
					RunProjectAndAssert (proj, b, doNotCleanupOnUpdate: true, parameters: new string [] {
						$"AndroidSdbTargetPort={port}",
						$"AndroidSdbHostPort={port}",
						"AndroidAttachDebugger=True",
					});

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
				} catch (Exception ex) {
					Assert.Fail ($"Exception occurred {ex}");
				} finally {
					session.Exit ();
				}
			}
		}

		static IEnumerable<object[]> Get_ApplicationRunsWithDebuggerAndBreaks_Data ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in Enum.GetValues (typeof (AndroidRuntime))) {
				// TODO: once CoreCLR debugging works, this needs to be adjusted accordingly
				if (runtime != AndroidRuntime.MonoVM) {
					continue;
				}

				AddTestData (
					embedAssemblies: true,
					username:	 null,
					packageFormat:   "apk",
					useLatestSdk:    true,
					runtime:         runtime
				);

				// https://github.com/dotnet/android/issues/10722
				// AddTestData (
				// 	embedAssemblies: true,
				// 	username:	 null,
				// 	packageFormat:   "apk",
				// 	useLatestSdk:    false,
				// 	runtime:         runtime
				// );

				AddTestData (
					embedAssemblies: false,
					username:	 null,
					packageFormat:   "apk",
					useLatestSdk:    true,
					runtime:         runtime
				);

				AddTestData (
					embedAssemblies: true,
					username:	 DeviceTest.GuestUserName,
					packageFormat:   "apk",
					useLatestSdk:    true,
					runtime:         runtime
				);

				AddTestData (
					embedAssemblies: false,
					username:	 DeviceTest.GuestUserName,
					packageFormat:   "apk",
					useLatestSdk:    true,
					runtime:         runtime
				);

				AddTestData (
					embedAssemblies: true,
					username:	 null,
					packageFormat:   "aab",
					useLatestSdk:    true,
					runtime:         runtime
				);

				AddTestData (
					embedAssemblies: false,
					username:	 null,
					packageFormat:   "aab",
					useLatestSdk:    true,
					runtime:         runtime
				);

				AddTestData (
					embedAssemblies: true,
					username:	 DeviceTest.GuestUserName,
					packageFormat:   "aab",
					useLatestSdk:    true,
					runtime:         runtime
				);

				AddTestData (
					embedAssemblies: false,
					username:	 DeviceTest.GuestUserName,
					packageFormat:   "aab",
					useLatestSdk:    true,
					runtime:         runtime
				);
			}

			return ret;

			void AddTestData (bool embedAssemblies, string username, string packageFormat, bool useLatestSdk, AndroidRuntime runtime)
			{
				ret.Add (new object[] {
					embedAssemblies,
					username,
					packageFormat,
					useLatestSdk,
					runtime,
				});
			}
		}

		// MonoVM-only test for the moment.
		[Test, Category ("Debugger"), Category ("WearOS")]
		[TestCaseSource (nameof(Get_ApplicationRunsWithDebuggerAndBreaks_Data))]
		[Retry (5)]
		public void ApplicationRunsWithDebuggerAndBreaks (bool embedAssemblies, string username, string packageFormat, bool useLatestSdk, AndroidRuntime runtime)
		{
			const bool isRelease = false;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			AssertCommercialBuild ();
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
				IsRelease = isRelease,
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
				IsRelease = isRelease,
				EmbedAssembliesIntoApk = embedAssemblies,
			};

			app.SetRuntime (runtime);
			if (!useLatestSdk) {
				lib.TargetFramework = $"{XABuildConfig.PreviousDotNetTargetFramework}-android";
				app.TargetFramework = $"{XABuildConfig.PreviousDotNetTargetFramework}-android";
			}

			app.SetProperty ("AndroidPackageFormat", packageFormat);
			app.MainPage = app.MainPage
				.Replace ("InitializeComponent ();", "InitializeComponent (); new Foo ();")
				// NOTE: can trigger deadlock/loop on startup:
				// 08-25 09:40:36.759 32259 32293 D monodroid-assembly: monodroid_dlopen: hash match found, DSO name is 'libSystem.Security.Cryptography.Native.Android.so'
				// 08-25 09:40:36.759 32259 32293 D monodroid-assembly: Trying to load loading shared JNI library /data/user/0/com.companyname.testgrendel/files/.__override__/arm64-v8a/libSystem.Security.Cryptography.Native.Android.so with System.loadLibrary
				// 08-25 09:40:36.759 32259 32293 D monodroid-assembly: Running DSO loader on thread 32293, dispatching to main thread
				.Replace ("//${AFTER_MAINACTIVITY}", """
					static MainActivity()
					{
						try
						{
							var text = new HttpClient().GetStringAsync("https://www.google.com").GetAwaiter().GetResult();
							Console.WriteLine("Web request:" + text);
						}
						catch (Exception ex)
						{
							// Doesn't actually matter if succeeds
							Console.WriteLine("Web request failed:" + ex);
						}
					}
				""");
			app.AddReference (lib);
			var abis = new [] { DeviceAbi };
			app.SetRuntimeIdentifiers (abis);
			app.SetDefaultTargetDevice ();
			using (var libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName)))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				appBuilder.Verbosity = LoggerVerbosity.Detailed;
				Assert.True (libBuilder.Build (lib), "Library should have built.");

				SetTargetFrameworkAndManifest (app, appBuilder, app.TargetFramework == $"{XABuildConfig.PreviousDotNetTargetFramework}-android" ? 36 : null);
				Assert.True (appBuilder.Install (app, parameters: parameters.ToArray ()), "App should have installed.");

				if (!embedAssemblies) {
					// Check that we deployed app and framework .pdb files
					StringAssertEx.ContainsRegex ($@"NotifySync CopyFile.+{app.ProjectName}\.pdb", appBuilder.LastBuildOutput,
						$"{app.ProjectName}.pdb should be deployed!");
					StringAssertEx.ContainsRegex ($@"NotifySync CopyFile.+{lib.ProjectName}\.pdb", appBuilder.LastBuildOutput,
						$"{lib.ProjectName}.pdb should be deployed!");
				}

				int breakcountHitCount = 0;
				ManualResetEvent resetEvent = new ManualResetEvent (false);
				var sw = new Stopwatch ();
				// setup the debugger
				var session = new SoftDebuggerSession ();
				try {
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
						MaxConnectionAttempts = DEBUGGER_MAX_CONNECTIONS,
						TimeBetweenConnectionAttempts = DEBUGGER_CONNECTION_TIMEOUT,
					};
					var startInfo = new SoftDebuggerStartInfo (args) {
						WorkingDirectory = Path.Combine (appBuilder.ProjectDirectory, app.IntermediateOutputPath, "android", "assets"),
					};
					var options = new DebuggerSessionOptions () {
						EvaluationOptions = EvaluationOptions.DefaultOptions,
					};
					options.EvaluationOptions.UseExternalTypeResolver = true;

					parameters.Add ($"AndroidSdbTargetPort={port}");
					parameters.Add ($"AndroidSdbHostPort={port}");
					parameters.Add ("AndroidAttachDebugger=True");

					RunProjectAndAssert (app, appBuilder, doNotCleanupOnUpdate: true, parameters: parameters.ToArray ());

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
				} catch (Exception ex) {
					Assert.Fail($"Exception occurred {ex}");
				} finally {
					session.Exit ();
				}
			}
		}
	}
}
