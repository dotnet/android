using System;

using Microsoft.Build.Framework;

using NUnit.Framework;

using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class BuildOrderTests : BaseTest
	{	
		[Test]
		public void APFBDependsOn ([Values (false, true)] bool isAppProject)
		{
			var setupTargets = new Import (() => "SetupBuild.targets") {
				TextContent = () =>@"
<Project>
  <PropertyGroup>
    <AndroidPrepareForBuildDependsOn>MyPrepareTarget;</AndroidPrepareForBuildDependsOn>
  </PropertyGroup>

  <Target Name=""MyPrepareTarget"" >
    <Message Text=""Running target: 'MyPrepareTarget'"" Importance=""high"" />
  </Target>
</Project>
"
			};

			XamarinAndroidCommonProject proj = isAppProject ?
				new XamarinAndroidApplicationProject {
					Imports = { setupTargets }
				}
				: new XamarinAndroidLibraryProject {
					Imports = { setupTargets }
				};

			using var builder = isAppProject ?
				CreateApkBuilder ()
				: CreateDllBuilder ();

			builder.Verbosity = LoggerVerbosity.Detailed;
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
			Assert.IsTrue (!builder.Output.IsTargetSkipped ("MyPrepareTarget"), "The 'MyPrepareTarget' target should run");
			StringAssertEx.Contains ("Running target: 'MyPrepareTarget'", builder.LastBuildOutput);
		}

		[Test]
		public void ComputeRunArgumentsIncludesDebuggerPortForwarding ()
		{
			var setupTargets = new Import (() => "SetupRun.targets") {
				TextContent = () => """
<Project>
  <PropertyGroup>
    <_AndroidComputeRunArgumentsDependsOn>NoOp</_AndroidComputeRunArgumentsDependsOn>
  </PropertyGroup>

  <Target Name="NoOp" />
  <Target Name="ReportRunArguments" AfterTargets="ComputeRunArguments">
    <Message Text="RunCommand=$(RunCommand)" Importance="high" />
    <Message Text="RunArguments=$(RunArguments)" Importance="high" />
  </Target>
</Project>
"""
			};
			var proj = new XamarinAndroidApplicationProject {
				Imports = { setupTargets },
			};
			proj.SetProperty ("AndroidLaunchActivity", "com.example.MainActivity");
			proj.SetProperty ("_AndroidPackage", "com.example");
			proj.SetProperty ("_AdbToolPath", "adb");
			proj.SetProperty ("AdbTarget", "-s emulator-5554");
			proj.SetProperty ("AndroidAttachDebugger", "true");
			proj.SetProperty ("AndroidSdbTargetPort", "12345");
			proj.SetProperty ("AndroidSdbHostPort", "54321");

			using var builder = CreateApkBuilder ();
			builder.Target = "ComputeRunArguments";
			builder.Verbosity = LoggerVerbosity.Detailed;
			Assert.IsTrue (builder.Build (proj), "ComputeRunArguments should succeed.");
			StringAssertEx.Contains ("RunCommand=dotnet", builder.LastBuildOutput);
			StringAssertEx.Contains ("--activity \"com.example.MainActivity\"", builder.LastBuildOutput);
			StringAssertEx.Contains ("--adb-target \"-s emulator-5554\"", builder.LastBuildOutput);
			StringAssertEx.Contains ("--forward-port \"12345:54321\"", builder.LastBuildOutput);
			Assert.IsFalse (builder.LastBuildOutput.ContainsText ("--debugger-"), "RunArguments should use generic port forwarding.");

			proj.SetProperty ("WaitForExit", "false");
			Assert.IsTrue (builder.Build (proj), "ComputeRunArguments should succeed without waiting.");
			StringAssertEx.Contains ("RunCommand=dotnet", builder.LastBuildOutput);
			StringAssertEx.Contains ("--no-wait", builder.LastBuildOutput);
			StringAssertEx.Contains ("--no-wake-device", builder.LastBuildOutput);
			StringAssertEx.Contains ("--forward-port \"12345:54321\"", builder.LastBuildOutput);

			proj.SetProperty ("WaitForExit", "true");
			proj.SetProperty ("_AndroidRunTarget", "true");
			Assert.IsTrue (builder.Build (proj), "ComputeRunArguments should succeed for the Run target.");
			StringAssertEx.Contains ("--no-wait", builder.LastBuildOutput);
			Assert.IsFalse (builder.LastBuildOutput.ContainsText ("--no-wake-device"), "The Run target should wake the device.");
			StringAssertEx.Contains ("--forward-port \"12345:54321\"", builder.LastBuildOutput);

			proj.SetProperty ("AndroidDebuggerServer", "false");
			Assert.IsTrue (builder.Build (proj), "ComputeRunArguments should succeed for a debugger client.");
			StringAssertEx.Contains ("RunCommand=dotnet", builder.LastBuildOutput);
			Assert.IsFalse (builder.LastBuildOutput.ContainsText ("--forward-port"), "A debugger client should connect without adb forwarding.");
		}

		[Test]
		public void RunWithLoggingEnablesManagedTimingLogger ()
		{
			var setupTargets = new Import (() => "SetupRunWithLogging.targets") {
				TextContent = () => """
<Project>
  <Target Name="ReportRunWithLoggingProperties" DependsOnTargets="_PrepareRunWithLogging">
    <Message Text="RuntimeLogProperty=$(_AndroidRuntimeLogProperty)" Importance="high" />
    <Message Text="RuntimeLog=$(_AndroidRuntimeLog)" Importance="high" />
  </Target>
</Project>
"""
			};
			var proj = new XamarinAndroidApplicationProject {
				Imports = { setupTargets },
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);

			using var builder = CreateApkBuilder ();
			builder.Target = "ReportRunWithLoggingProperties";
			builder.Verbosity = LoggerVerbosity.Detailed;
			Assert.IsTrue (builder.Build (proj), "ReportRunWithLoggingProperties should succeed.");
			StringAssertEx.Contains ("RuntimeLogProperty=debug.dotnet.log", builder.LastBuildOutput);
			StringAssertEx.Contains ("RuntimeLog=default,assembly,timing", builder.LastBuildOutput);
		}

	}
}
