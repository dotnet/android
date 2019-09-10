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

namespace Xamarin.Android.Build.Tests
{
	public class DebuggingTest : DeviceTest {
		[TearDown]
		public void ClearDebugProperties ()
		{
			ClearDebugProperty ();
		}

		void SetTargetFrameworkAndManifest(XamarinAndroidApplicationProject proj, Builder builder)
		{
			string apiLevel;
			proj.TargetFrameworkVersion = builder.LatestTargetFrameworkVersion (out apiLevel);
			proj.AndroidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" android:versionCode=""1"" android:versionName=""1.0"" package=""UnnamedProject.UnnamedProject"">
	<uses-sdk android:minSdkVersion=""24"" android:targetSdkVersion=""{apiLevel}"" />
	<application android:label=""${{PROJECT_NAME}}"">
	</application >
</manifest>";
		}

		[Test]
		[Retry (1)]
		public void ApplicationRunsWithoutDebugger ([Values (false, true)] bool isRelease)
		{
			if (!HasDevices) {
				Assert.Ignore ("Test needs a device attached.");
				return;
			}

			var proj = new XamarinFormsAndroidApplicationProject () {
				IsRelease = isRelease,
			};
			if (isRelease || !CommercialBuildAvailable) {
				var abis = new string [] { "armeabi-v7a", "x86" };
				proj.SetProperty (KnownProperties.AndroidSupportedAbis, string.Join (";", abis));
			}
			proj.SetDefaultTargetDevice ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				SetTargetFrameworkAndManifest (proj, b);
				b.BuildLogFile = "install.log";
				Assert.True (b.Install (proj), "Project should have installed.");
				ClearAdbLogcat ();
				if (CommercialBuildAvailable) {
					b.BuildLogFile = "_run.log";
					Assert.True (b.RunTarget (proj, "_Run"), "Project should have run.");
				} else
					AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity");

				Assert.True (WaitForActivityToStart (proj.PackageName, "MainActivity",
					Path.Combine (Root, b.ProjectDirectory, "logcat.log"), 30), "Activity should have started.");
				Assert.True (b.Uninstall (proj), "Project should have uninstalled.");
			}
		}

		[Test]
		public void ClassLibraryMainLauncherRuns ()
		{
			if (!HasDevices) {
				Assert.Ignore ("Test needs a device attached.");
				return;
			}

			var path = Path.Combine ("temp", TestName);

			var app = new XamarinAndroidApplicationProject {
				ProjectName = "MyApp",
			};
			if (!CommercialBuildAvailable) {
				var abis = new string [] { "armeabi-v7a", "x86" };
				app.SetProperty (KnownProperties.AndroidSupportedAbis, string.Join (";", abis));
			}
			app.SetDefaultTargetDevice ();

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
				TextContent = () => "<?xml version=\"1.0\" encoding=\"utf-8\" ?><LinearLayout xmlns:android=\"http://schemas.android.com/apk/res/android\" />"
			});
			app.Sources.Remove (app.GetItem ("MainActivity.cs"));

			using (var libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName)))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				SetTargetFrameworkAndManifest (app, appBuilder);
				Assert.IsTrue (libBuilder.Build (lib), "library build should have succeeded.");
				Assert.True (appBuilder.Install (app), "app should have installed.");
				ClearAdbLogcat ();
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
				/* useSharedRuntime */   false,
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies",
				/* activityStarts */     true,
			},
			new object[] {
				/* useSharedRuntime */   false,
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies",
				/* activityStarts */     true,
			},
			new object[] {
				/* useSharedRuntime */   true,
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies",
				/* activityStarts */     true,
			},
			new object[] {
				/* useSharedRuntime */   true,
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies",
				/* activityStarts */     true,
			},
			new object[] {
				/* useSharedRuntime */   true,
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies:Dexes",
				/* activityStarts */     true,
			},
			new object[] {
				/* useSharedRuntime */   true,
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies:Dexes",
				/* activityStarts */     false,
			},
		};
#pragma warning restore 414

		[Test]
		[TestCaseSource (nameof (DebuggerCustomAppTestCases))]
		[Retry (1)]
		public void CustomApplicationRunsWithDebuggerAndBreaks (bool useSharedRuntime, bool embedAssemblies, string fastDevType, bool activityStarts)
		{
			if (!CommercialBuildAvailable) {
				Assert.Ignore ("Test does not run on the Open Source Builds.");
				return;
			}
			if (!HasDevices) {
				Assert.Ignore ("Test needs a device attached.");
				return;
			}
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = false,
				AndroidFastDeploymentType = fastDevType,
			};
			var abis = new string [] { "armeabi-v7a", "x86" };
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, string.Join (";", abis));
			proj.SetProperty (KnownProperties.AndroidUseSharedRuntime, useSharedRuntime.ToString ());
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
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				SetTargetFrameworkAndManifest (proj, b);
				Assert.True (b.Install (proj), "Project should have installed.");

				int breakcountHitCount = 0;
				ManualResetEvent resetEvent = new ManualResetEvent (false);
				var sw = new Stopwatch ();
				// setup the debugger
				var session = new SoftDebuggerSession ();
				session.Breakpoints = new BreakpointStore {
					{ Path.Combine (Root, b.ProjectDirectory, "MainActivity.cs"),  19 },
					{ Path.Combine (Root, b.ProjectDirectory, "MyApplication.cs"),  17 },
				};
				session.TargetHitBreakpoint += (sender, e) => {
					Console.WriteLine ($"BREAK {e.Type}");
					breakcountHitCount++;
					session.Continue ();
				};
				var rnd = new Random ();
				int port = rnd.Next (10000, 20000);
				TestContext.Out.WriteLine ($"{port}");
				var args = new SoftDebuggerConnectArgs ("", IPAddress.Loopback, port) {
					MaxConnectionAttempts = 10,
				};
				var startInfo = new SoftDebuggerStartInfo (args) {
					WorkingDirectory = Path.Combine (b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets"),
				};
				var options = new DebuggerSessionOptions () {
					EvaluationOptions = EvaluationOptions.DefaultOptions,
				};
				options.EvaluationOptions.UseExternalTypeResolver = true;
				ClearAdbLogcat ();
				Assert.True (b.RunTarget (proj, "_Run", parameters: new string [] {
					$"AndroidSdbTargetPort={port}",
					$"AndroidSdbHostPort={port}",
					"AndroidAttachDebugger=True",
				}), "Project should have run.");

				// do we expect the app to start?
				Assert.AreEqual (activityStarts, WaitForDebuggerToStart (Path.Combine (Root, b.ProjectDirectory, "logcat.log")), "Activity should have started");
				if (!activityStarts)
					return;
				// we need to give a bit of time for the debug server to start up.
				WaitFor (2000);
				session.LogWriter += (isStderr, text) => { Console.WriteLine (text); };
				session.OutputWriter += (isStderr, text) => { Console.WriteLine (text); };
				session.DebugWriter += (level, category, message) => { Console.WriteLine (message); };
				session.Run (startInfo, options);
				var expectedTime = TimeSpan.FromSeconds (1);
				var actualTime = ProfileFor (() => session.IsConnected);
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
				Assert.True (b.Uninstall (proj), "Project should have uninstalled.");
				session.Exit ();
			}
		}

#pragma warning disable 414
		static object [] DebuggerTestCases = new object [] {
			new object[] {
				/* useSharedRuntime */   false,
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies",
			},
			new object[] {
				/* useSharedRuntime */   false,
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies",
			},
			new object[] {
				/* useSharedRuntime */   true,
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies",
			},
			new object[] {
				/* useSharedRuntime */   true,
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies",
			},
			new object[] {
				/* useSharedRuntime */   true,
				/* embedAssemblies */    true,
				/* fastDevType */        "Assemblies:Dexes",
			},
			new object[] {
				/* useSharedRuntime */   true,
				/* embedAssemblies */    false,
				/* fastDevType */        "Assemblies:Dexes",
			},
		};
#pragma warning restore 414

		SoftDebuggerSession CreateDebuggerSession (BreakpointStore breakPoints, EventHandler<TargetEventArgs> targetHitHandler)
		{
			var session = new SoftDebuggerSession ();
			session.Breakpoints = breakPoints;
			session.TargetHitBreakpoint += targetHitHandler;
			session.LogWriter += (isStderr, text) => { Console.WriteLine (text); };
			session.OutputWriter += (isStderr, text) => { Console.WriteLine (text); };
			session.DebugWriter += (level, category, message) => { Console.WriteLine (message); };
			return session;
		}

		[Test]
		[TestCaseSource (nameof(DebuggerTestCases))]
		[Retry (1)]
		public void ApplicationRunsWithDebuggerAndBreaks (bool useSharedRuntime, bool embedAssemblies, string fastDevType)
		{
			if (!CommercialBuildAvailable) {
				Assert.Ignore ("Test does not run on the Open Source Builds.");
				return;
			}
			if (!HasDevices) {
				Assert.Ignore ("Test needs a device attached.");
				return;
			}
			var proj = new XamarinFormsAndroidApplicationProject () {
				IsRelease = false,
				AndroidUseSharedRuntime = useSharedRuntime,
				EmbedAssembliesIntoApk = embedAssemblies,
				AndroidFastDeploymentType = fastDevType
			};
			var abis = new string [] { "armeabi-v7a", "x86" };
			proj.SetProperty (KnownProperties.AndroidSupportedAbis, string.Join (";", abis));
			proj.SetDefaultTargetDevice ();
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				SetTargetFrameworkAndManifest (proj, b);
				b.BuildLogFile = "install.log";
				Assert.True (b.Install (proj), "Project should have installed.");

				int breakcountHitCount = 0;
				ManualResetEvent resetEvent = new ManualResetEvent (false);
				var sw = new Stopwatch ();
				BreakpointStore breakPoints = new BreakpointStore {
					{ Path.Combine (Root, b.ProjectDirectory, "MainActivity.cs"),  19 },
					{ Path.Combine (Root, b.ProjectDirectory, "MainPage.xaml.cs"), 14 },
					{ Path.Combine (Root, b.ProjectDirectory, "MainPage.xaml.cs"), 19 },
					{ Path.Combine (Root, b.ProjectDirectory, "App.xaml.cs"), 12 },
				};
				SoftDebuggerSession session = null;
				// setup the debugger
				EventHandler<TargetEventArgs> targetHit = (sender, e) => {
					Console.WriteLine ($"BREAK {e.Type}");
					breakcountHitCount++;
					session.Continue ();
				};
				session = CreateDebuggerSession (breakPoints, targetHit);
				var rnd = new Random ();
				int port = rnd.Next (10000, 20000);
				TestContext.Out.WriteLine ($"{port}");
				var args = new SoftDebuggerConnectArgs ("", IPAddress.Loopback, port) {
					MaxConnectionAttempts = 10,
				};
				var startInfo = new SoftDebuggerStartInfo (args) {
					WorkingDirectory = Path.Combine (b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets"),
				};
				var options = new DebuggerSessionOptions () {
					EvaluationOptions = EvaluationOptions.DefaultOptions,
				};
				options.EvaluationOptions.UseExternalTypeResolver = true;
				ClearAdbLogcat ();
				b.BuildLogFile = "_run.log";
				Assert.True (b.RunTarget (proj, "_Run", doNotCleanupOnUpdate: true, parameters: new string [] {
					$"AndroidSdbTargetPort={port}",
					$"AndroidSdbHostPort={port}",
					"AndroidAttachDebugger=True",
				}), "Project should have run.");

				Assert.IsTrue (WaitForDebuggerToStart (Path.Combine (Root, b.ProjectDirectory, "logcat.log")), "Activity should have started");
				// we need to give a bit of time for the debug server to start up.
				WaitFor (2000);
				session.Run (startInfo, options);
				WaitFor (TimeSpan.FromSeconds (30), () => session.IsConnected);
				Assert.True (session.IsConnected, "Debugger should have connected but it did not.");
				// we need to wait here for a while to allow the breakpoints to hit
				// but we need to timeout
				TimeSpan timeout = TimeSpan.FromSeconds (60);
				int expected = 3;
				while (session.IsConnected && breakcountHitCount < 3 && timeout >= TimeSpan.Zero) {
					Thread.Sleep (10);
					timeout = timeout.Subtract (TimeSpan.FromMilliseconds (10));
				}
				WaitFor (2000);
				Assert.AreEqual (expected, breakcountHitCount, $"Should have hit {expected} breakpoints. Only hit {breakcountHitCount}");
				breakcountHitCount = 0;
				ClearAdbLogcat ();
				ClickButton (proj.PackageName, "myXFButton", "CLICK ME");
				while (session.IsConnected && breakcountHitCount < 1 && timeout >= TimeSpan.Zero) {
					Thread.Sleep (10);
					timeout = timeout.Subtract (TimeSpan.FromMilliseconds (10));
				}
				expected = 1;
				Assert.AreEqual (expected, breakcountHitCount, $"Should have hit {expected} breakpoints. Only hit {breakcountHitCount}");
				session.Exit ();
				WaitFor (2000);
				Assert.IsTrue (session.HasExited);

				breakcountHitCount = 0;
				session = CreateDebuggerSession (breakPoints, targetHit);

				string mainPage = Path.Combine (Root, b.ProjectDirectory, "MainPage.xaml");
				string mainPageText = File.ReadAllText (mainPage);
				File.WriteAllText (mainPage, mainPageText.Replace ("Click Me", "Click Me 1"));

				b.BuildLogFile = "build2.log";
				Assert.True (b.Build (proj, doNotCleanupOnUpdate: true), "Project should have built.");
				b.BuildLogFile = "install2.log";
				Assert.True (b.Install (proj, doNotCleanupOnUpdate: true), "Project should have installed.");

				ClearAdbLogcat ();
				b.BuildLogFile = "_run2.log";
				Assert.True (b.RunTarget (proj, "_Run", doNotCleanupOnUpdate: true, parameters: new string [] {
					$"AndroidSdbTargetPort={port}",
					$"AndroidSdbHostPort={port}",
					"AndroidAttachDebugger=True",
				}), "Project should have run.");

				Assert.IsTrue (WaitForDebuggerToStart (Path.Combine (Root, b.ProjectDirectory, "logcat2.log")), "Activity should have started");
				WaitFor (2000);
				session.Run (startInfo, options);
				WaitFor (TimeSpan.FromSeconds (30), () => session.IsConnected);
				Assert.True (session.IsConnected, "Debugger should have connected but it did not.");
				// we need to wait here for a while to allow the breakpoints to hit
				// but we need to timeout
				timeout = TimeSpan.FromSeconds (60);
				while (session.IsConnected && breakcountHitCount < 3) {
					Thread.Sleep (10);
					timeout = timeout.Subtract (TimeSpan.FromMilliseconds (10));
				}
				WaitFor (2000);
				ClearAdbLogcat ();
				ClickButton (proj.PackageName, "myXFButton", "CLICK ME 1");
				while (session.IsConnected && breakcountHitCount < 4) {
					Thread.Sleep (10);
					timeout = timeout.Subtract (TimeSpan.FromMilliseconds (10));
				}
				expected = 4;
				Assert.AreEqual (expected, breakcountHitCount, $"Should have hit {expected} breakpoints. Only hit {breakcountHitCount}");
				session.Exit ();
				WaitFor (2000);
				Assert.IsTrue (session.HasExited);

				Assert.True (b.Uninstall (proj), "Project should have uninstalled.");
			}
		}
	}
}
