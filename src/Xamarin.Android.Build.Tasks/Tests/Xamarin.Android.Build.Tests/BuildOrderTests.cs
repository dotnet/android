using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
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

		[TestCase ("selected-serial", "true", false)]
		[TestCase ("selected-serial", "", true)]
		[TestCase ("", "", false)]
		public void DeviceBootIsLimitedToOuterBuilds (string device, string innerBuild, bool bootExpected)
		{
			var application = XDocument.Load (Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.Application.targets"));
			var boot = new XElement (application.Descendants ("Target").Single (target => (string) target.Attribute ("Name") == "_EnsureDeviceBooted"));

			// Replace device work with a trace; retain the shipped condition and hook.
			boot.RemoveNodes ();
			boot.Add (
				new XElement ("PropertyGroup", new XElement ("AdbTarget", "-s resolved-serial")),
				new XElement ("ItemGroup", new XElement ("DeviceBootTrace", new XAttribute ("Include", "Boot=$(Device)"))));

			var project = new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("Device", device),
					new XElement ("AdbTarget", "-s selected-serial"),
					new XElement ("RuntimeIdentifier", "android-arm64"),
					new XElement ("_ComputeFilesToPublishForRuntimeIdentifiers", innerBuild)),
				boot,
				new XElement ("Target", new XAttribute ("Name", "_GetPrimaryCpuAbi"),
					new XElement ("ItemGroup", new XElement ("DeviceBootTrace", new XAttribute ("Include", "ABI=$(AdbTarget)")))),
				new XElement ("Target", new XAttribute ("Name", "Build"),
					new XAttribute ("DependsOnTargets", "_GetPrimaryCpuAbi;_EnsureDeviceBooted")));

			using var reader = project.CreateReader ();
			using var collection = new ProjectCollection ();
			var root = ProjectRootElement.Create (reader, collection);
			root.FullPath = Path.Combine (Root, "DeviceBoot.proj");
			var instance = new ProjectInstance (root, globalProperties: null, toolsVersion: null, collection);
			Assert.IsTrue (instance.Build ("Build", []));
			string [] expected = bootExpected ? ["Boot=" + device, "ABI=-s resolved-serial"] : ["ABI=-s selected-serial"];
			CollectionAssert.AreEqual (expected, instance.GetItems ("DeviceBootTrace").Select (item => item.EvaluatedInclude),
				"Only outer builds with Device should check it, once, before the ABI query.");
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
