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
			new object[] {
				/* isRelease */      false,
				/* xamarinForms */   true,
				/* targetFramework*/ "net6.0-android",
			},
			new object[] {
				/* isRelease */      true,
				/* xamarinForms */   true,
				/* targetFramework*/ "net6.0-android",
			},
		};

		[Test]
		[TestCaseSource (nameof (DotNetInstallAndRunSource))]
		public void DotNetInstallAndRun (bool isRelease, bool xamarinForms, string targetFramework)
		{
			//var id = DateTime.Now.ToString ("yyyyMMddHHmmssfff");

			XASdkProject proj;
			if (xamarinForms) {
				proj = new XamarinFormsXASdkProject {
					ProjectName = $"DotNetInstallAndRun",
					//PackageName = $"com.xamarin.dotnetinstallandrun{id}",
					IsRelease = isRelease
				};
			} else {
				proj = new XASdkProject {
					ProjectName = $"DotNetInstallAndRun",
					//PackageName = $"com.xamarin.dotnetinstallandrun{id}",
					IsRelease = isRelease
				};
			}
			proj.TargetFramework = targetFramework;
			proj.AddNuGetSourcesForOlderTargetFrameworks ();
			proj.SetRuntimeIdentifier (DeviceAbi);

			var relativeProjDir = Path.Combine ("temp", TestName);
			var fullProjDir     = Path.Combine (Root, relativeProjDir);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = fullProjDir;
			TestPackageNames[proj.PackageName] = proj.PackageName;
			var files = proj.Save ();
			proj.Populate (relativeProjDir, files);
			proj.CopyNuGetConfig (relativeProjDir);
			var dotnet = new DotNetCLI (proj, Path.Combine (fullProjDir, proj.ProjectFilePath));

			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");
			Assert.IsTrue (dotnet.Run (), "`dotnet run` should succeed");
			WaitForPermissionActivity (Path.Combine (Root, dotnet.ProjectDirectory, "permission-logcat.log"));
			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (fullProjDir, "logcat.log"), 30);
			RunAdbCommand ($"uninstall {proj.PackageName}");
			Assert.IsTrue(didLaunch, "Activity should have started.");
		}

		[Test]
		public void TypeAndMemberRemapping ([Values (false, true)] bool isRelease)
		{
			//var id = DateTime.Now.ToString ("yyyyMMddHHmmssfff");

			var proj = new XASdkProject () {
				ProjectName = $"TypeAndMemberRemapping",
				//PackageName = $"com.xamarin.typeandmemberremapping{id}",
				IsRelease = isRelease,
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
			proj.SetRuntimeIdentifier (DeviceAbi);
			var relativeProjDir = Path.Combine ("temp", TestName);
			var fullProjDir     = Path.Combine (Root, relativeProjDir);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = fullProjDir;
			TestPackageNames[proj.PackageName] = proj.PackageName;
			var files = proj.Save ();
			proj.Populate (relativeProjDir, files);
			proj.CopyNuGetConfig (relativeProjDir);
			var dotnet = new DotNetCLI (proj, Path.Combine (fullProjDir, proj.ProjectFilePath));

			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");
			Assert.IsTrue (dotnet.Run (), "`dotnet run` should succeed");

			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (fullProjDir, "logcat.log"));
			Assert.IsTrue (didLaunch, "MainActivity should have launched!");
			var logcatOutput = File.ReadAllText (Path.Combine (fullProjDir, "logcat.log"));

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
			AssertHasDevices ();
			if (!Builder.UseDotNet) {
				Assert.Ignore ("Skipping. Test not relevant under Classic.");
			}

			var proj = new XASdkProject () {
				IsRelease = true,
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
			proj.SetProperty (proj.ReleaseProperties, "SupportedOSPlatformVersion", "21");

			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", @"
		Console.WriteLine ($""# jonp static interface default method invocation; IStaticMethodsInterface.Value={Example.IStaticMethodsInterface.Value}"");
");
			proj.SetRuntimeIdentifier (DeviceAbi);
			var relativeProjDir = Path.Combine ("temp", TestName);
			var fullProjDir     = Path.Combine (Root, relativeProjDir);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = fullProjDir;
			var files = proj.Save ();
			proj.Populate (relativeProjDir, files);
			proj.CopyNuGetConfig (relativeProjDir);
			var dotnet = new DotNetCLI (proj, Path.Combine (fullProjDir, proj.ProjectFilePath));

			Assert.IsTrue (dotnet.Build (), "`dotnet build` should succeed");
			Assert.IsTrue (dotnet.Run (), "`dotnet run` should succeed");

			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (fullProjDir, "logcat.log"));
			Assert.IsTrue (didLaunch, "MainActivity should have launched!");
			var logcatOutput = File.ReadAllText (Path.Combine (fullProjDir, "logcat.log"));

			StringAssert.Contains (
					"IStaticMethodsInterface.Value=3",
					logcatOutput,
					"Was IStaticMethodsInterface.Value executed?"
			);
		}

		[Test]
		[Category ("Debugger")]
		[Retry(5)]
		public void DotNetDebug ([Values("net6.0-android", "net7.0-android")] string targetFramework)
		{
			AssertCommercialBuild ();

			var proj = new XASdkProject ();
			proj.TargetFramework = targetFramework;
			proj.AddNuGetSourcesForOlderTargetFrameworks ();
			proj.SetRuntimeIdentifier (DeviceAbi);
			string runtimeId = proj.GetProperty (KnownProperties.RuntimeIdentifier);

			var relativeProjDir = Path.Combine ("temp", TestName);
			var fullProjDir = Path.Combine (Root, relativeProjDir);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = fullProjDir;
			TestPackageNames[proj.PackageName] = proj.PackageName;
			var files = proj.Save ();
			proj.Populate (relativeProjDir, files);
			proj.CopyNuGetConfig (relativeProjDir);
			var dotnet = new DotNetCLI (proj, Path.Combine (fullProjDir, proj.ProjectFilePath));
			Assert.IsTrue (dotnet.Build ("Install"), "`dotnet build` should succeed");

			bool breakpointHit = false;
			ManualResetEvent resetEvent = new ManualResetEvent (false);
			var sw = new Stopwatch ();
			// setup the debugger
			var session = new SoftDebuggerSession ();
			try {
				session.Breakpoints = new BreakpointStore {
					{ Path.Combine (Root, dotnet.ProjectDirectory, "MainActivity.cs"), 10 },
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
					WorkingDirectory = Path.Combine (dotnet.ProjectDirectory, proj.IntermediateOutputPath, runtimeId, "android", "assets"),
				};
				var options = new DebuggerSessionOptions () {
					EvaluationOptions = EvaluationOptions.DefaultOptions,
				};
				options.EvaluationOptions.UseExternalTypeResolver = true;
				dotnet.BuildLogFile = Path.Combine (Root, dotnet.ProjectDirectory, "run.log");
				Assert.True (dotnet.Build ("Run", parameters: new [] {
					$"AndroidSdbTargetPort={port}",
					$"AndroidSdbHostPort={port}",
					"AndroidAttachDebugger=True",
				}), "Project should have run.");
				WaitForPermissionActivity (Path.Combine (Root, dotnet.ProjectDirectory, "permission-logcat.log"));
				Assert.IsTrue (WaitForDebuggerToStart (Path.Combine (Root, dotnet.ProjectDirectory, "logcat.log"), 120), "Activity should have started");
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
