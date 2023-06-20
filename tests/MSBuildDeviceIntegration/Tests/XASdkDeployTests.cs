using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using NUnit.Framework;
using Xamarin.ProjectTools;

#if !NET472
namespace Xamarin.Android.Build.Tests
{
	[Obsolete ("De-dupe and migrate these tests to InstallAndRunTests.cs")]
	[TestFixture]
	[Category ("UsesDevice"), Category ("WearOS")]
	public class XASdkDeployTests : DeviceTest
	{
		static object [] DotNetInstallAndRunSource = new object [] {
			new object[] {
				/* isRelease */      false,
				/* xamarinForms */   false,
				/* targetFramework*/ "net8.0-android",
			},
			new object[] {
				/* isRelease */      true,
				/* xamarinForms */   false,
				/* targetFramework*/ "net8.0-android",
			},
			new object[] {
				/* isRelease */      false,
				/* xamarinForms */   true,
				/* targetFramework*/ "net8.0-android",
			},
			new object[] {
				/* isRelease */      true,
				/* xamarinForms */   true,
				/* targetFramework*/ "net8.0-android",
			},
			new object[] {
				/* isRelease */      true,
				/* xamarinForms */   false,
				/* targetFramework*/ "net7.0-android",
			},
			new object[] {
				/* isRelease */      false,
				/* xamarinForms */   true,
				/* targetFramework*/ "net7.0-android",
			},
			new object[] {
				/* isRelease */      true,
				/* xamarinForms */   true,
				/* targetFramework*/ "net7.0-android",
			},
		};

		[Test]
		[TestCaseSource (nameof (DotNetInstallAndRunSource))]
		public void DotNetInstallAndRun (bool isRelease, bool xamarinForms, string targetFramework)
		{
			XamarinAndroidApplicationProject proj;
			if (xamarinForms) {
				proj = new XamarinFormsAndroidApplicationProject {
					IsRelease = isRelease,
					EnableDefaultItems = true,
				};
			} else {
				proj = new XamarinAndroidApplicationProject {
					IsRelease = isRelease,
					EnableDefaultItems = true,
				};
			}

			if (targetFramework == "net7.0-android")
				proj.TargetSdkVersion = "33";

			proj.TargetFramework = targetFramework;
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
		[Category ("Debugger")]
		[Retry(5)]
		public void DotNetDebug ([Values("net7.0-android", "net8.0-android")] string targetFramework)
		{
			AssertCommercialBuild ();

			var proj = new XamarinAndroidApplicationProject () {
				EnableDefaultItems = true,
				TargetFramework = targetFramework,
			};
			proj.SetRuntimeIdentifier (DeviceAbi);
			string runtimeId = proj.GetProperty (KnownProperties.RuntimeIdentifier);

			var builder = CreateApkBuilder ();

			Assert.IsTrue (builder.Install (proj), "Install should succeed.");

			bool breakpointHit = false;
			ManualResetEvent resetEvent = new ManualResetEvent (false);
			var sw = new Stopwatch ();
			// setup the debugger
			var session = new SoftDebuggerSession ();
			try {
				session.Breakpoints = new BreakpointStore {
					{ Path.Combine (Root, builder.ProjectDirectory, "MainActivity.cs"), 10 },
				};
				session.TargetHitBreakpoint += (sender, e) => {
					Console.WriteLine ($"BREAK {e.Type}");
					breakpointHit = true;
					session.Continue ();
				};
				var rnd = new Random ();
				int port = rnd.Next (10000, 20000);
				TestContext.Out.WriteLine ($"{port}");
				var args = new SoftDebuggerConnectArgs ("", IPAddress.Loopback, port) {
					MaxConnectionAttempts = 10,
				};
				var startInfo = new SoftDebuggerStartInfo (args) {
					WorkingDirectory = Path.Combine (builder.ProjectDirectory, proj.IntermediateOutputPath, runtimeId, "android", "assets"),
				};
				var options = new DebuggerSessionOptions () {
					EvaluationOptions = EvaluationOptions.DefaultOptions,
				};
				options.EvaluationOptions.UseExternalTypeResolver = true;
				builder.BuildLogFile = Path.Combine (Root, builder.ProjectDirectory, "run.log");
				Assert.True (builder.RunTarget (proj, "Run", parameters: new [] {
					$"AndroidSdbTargetPort={port}",
					$"AndroidSdbHostPort={port}",
					"AndroidAttachDebugger=True",
				}), "Project should have run.");
				WaitForPermissionActivity (Path.Combine (Root, builder.ProjectDirectory, "permission-logcat.log"));
				Assert.IsTrue (WaitForDebuggerToStart (Path.Combine (Root, builder.ProjectDirectory, "logcat.log"), 120), "Activity should have started");
				// we need to give a bit of time for the debug server to start up.
				WaitFor (2000);
				session.LogWriter += (isStderr, text) => { Console.WriteLine (text); };
				session.OutputWriter += (isStderr, text) => { Console.WriteLine (text); };
				session.DebugWriter += (level, category, message) => { Console.WriteLine (message); };
				session.Run (startInfo, options);
				WaitFor (TimeSpan.FromSeconds (30), () => session.IsConnected);
				Assert.True (session.IsConnected, "Debugger should have connected but it did not.");
				// we need to wait here for a while to allow the breakpoints to hit
				// but we need to timeout
				TimeSpan timeout = TimeSpan.FromSeconds (60);
				while (session.IsConnected && !breakpointHit && timeout >= TimeSpan.Zero) {
					Thread.Sleep (10);
					timeout = timeout.Subtract (TimeSpan.FromMilliseconds (10));
				}
				WaitFor (2000);
				Assert.IsTrue (breakpointHit, "Should have a breakpoint");
			} catch (Exception ex) {
				Assert.Fail($"Exception occurred {ex}");
			} finally {
				session.Exit ();
			}
		}
	}
}
#endif
