using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[NonParallelizable]
	[Category ("UsesDevices"), Category ("DotNetIgnore")] // These don't need to run under `--params dotnet=true`
	public class XASdkDeployTests : DeviceTest
	{
		[Test]
		public void DotNetInstallAndRun ([Values (false, true)] bool isRelease, [Values (false, true)] bool xamarinForms)
		{
			AssertHasDevices ();

			XASdkProject proj;
			if (xamarinForms) {
				proj = new XamarinFormsXASdkProject {
					IsRelease = isRelease
				};
			} else {
				proj = new XASdkProject {
					IsRelease = isRelease
				};
			}
			proj.SetRuntimeIdentifier (DeviceAbi);

			var relativeProjDir = Path.Combine ("temp", TestName);
			var fullProjDir     = Path.Combine (Root, relativeProjDir);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = fullProjDir;
			var files = proj.Save ();
			proj.Populate (relativeProjDir, files);
			var dotnet = new DotNetCLI (proj, Path.Combine (fullProjDir, proj.ProjectFilePath));

			Assert.IsTrue (dotnet.Run (), "`dotnet run` should succeed");
			bool didLaunch = WaitForActivityToStart (proj.PackageName, "MainActivity",
				Path.Combine (fullProjDir, "logcat.log"), 30);
			RunAdbCommand ($"uninstall {proj.PackageName}");
			Assert.IsTrue(didLaunch, "Activity should have started.");
		}

		[Test]
		public void DotNetDebug ()
		{
			AssertCommercialBuild ();
			AssertHasDevices ();

			XASdkProject proj;
			proj = new XASdkProject {
				//TODO: targetSdkVersion="30" causes a crash on startup in .NET 5
				MinSdkVersion = null,
				TargetSdkVersion = null,
			};
			proj.SetRuntimeIdentifier (DeviceAbi);

			var relativeProjDir = Path.Combine ("temp", TestName);
			var fullProjDir = Path.Combine (Root, relativeProjDir);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = fullProjDir;
			var files = proj.Save ();
			proj.Populate (relativeProjDir, files);
			var dotnet = new DotNetCLI (proj, Path.Combine (fullProjDir, proj.ProjectFilePath));
			Assert.IsTrue (dotnet.Build ("Install"), "`dotnet build` should succeed");

			bool breakpointHit = false;
			ManualResetEvent resetEvent = new ManualResetEvent (false);
			var sw = new Stopwatch ();
			// setup the debugger
			var session = new SoftDebuggerSession ();
			session.Breakpoints = new BreakpointStore {
				{ Path.Combine (Root, dotnet.ProjectDirectory, "MainActivity.cs"),  19 },
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
				WorkingDirectory = Path.Combine (dotnet.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets"),
			};
			var options = new DebuggerSessionOptions () {
				EvaluationOptions = EvaluationOptions.DefaultOptions,
			};
			options.EvaluationOptions.UseExternalTypeResolver = true;
			ClearAdbLogcat ();
			Assert.True (dotnet.Build ("Run", new string [] {
				$"AndroidSdbTargetPort={port}",
				$"AndroidSdbHostPort={port}",
				"AndroidAttachDebugger=True",
			}), "Project should have run.");

			Assert.IsTrue (WaitForDebuggerToStart (Path.Combine (Root, dotnet.ProjectDirectory, "logcat.log")), "Activity should have started");
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
		}
	}
}
