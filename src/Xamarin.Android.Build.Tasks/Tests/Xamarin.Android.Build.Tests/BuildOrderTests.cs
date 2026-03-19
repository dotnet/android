using System;

using Microsoft.Build.Framework;

using NUnit.Framework;

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
		public void DeployToDeviceDependsOn_ContainsEnsureDeviceBooted ()
		{
			// _EnsureDeviceBooted must be in DeployToDeviceDependsOnTargets so that it fires
			// when the .NET SDK calls ProjectInstance.Build(["DeployToDevice"]) in-process,
			// where BeforeTargets hooks are not reliably triggered.
			var checkTargets = new Import (() => "CheckDeployOrder.targets") {
				TextContent = () => """
<Project>
  <Target Name="_CheckDeployOrder">
    <Message Text="DeployToDeviceDependsOnTargets=$(DeployToDeviceDependsOnTargets)" Importance="high" />
  </Target>
</Project>
"""
			};

			var proj = new XamarinAndroidApplicationProject {
				Imports = { checkTargets }
			};

			using var builder = CreateApkBuilder ();
			builder.Verbosity = LoggerVerbosity.Detailed;
			Assert.IsTrue (builder.RunTarget (proj, "_CheckDeployOrder"),
				"Build should have succeeded.");

			// The property is multi-line, so join all build output into a single string for matching
			string allOutput = string.Join ("\n", builder.LastBuildOutput);
			StringAssert.Contains ("_EnsureDeviceBooted", allOutput,
				"DeployToDeviceDependsOnTargets must contain _EnsureDeviceBooted");

			// _EnsureDeviceBooted must appear before _DeployApk to set AdbTarget first
			int bootIndex = allOutput.IndexOf ("_EnsureDeviceBooted", StringComparison.Ordinal);
			int deployIndex = allOutput.IndexOf ("_DeployApk", StringComparison.Ordinal);
			if (deployIndex >= 0) {
				Assert.Less (bootIndex, deployIndex,
					"_EnsureDeviceBooted must appear before _DeployApk in DeployToDeviceDependsOnTargets");
			}
		}

		[Test]
		public void DeployToDeviceDependsOn_MSBuildValidation ()
		{
			// Validates the same constraint using MSBuild property functions directly,
			// as a safety net in case log parsing has edge cases.
			var checkTargets = new Import (() => "CheckDeployOrder.targets") {
				TextContent = () => """
<Project>
  <Target Name="_CheckDeployOrder">
    <Error Text="DeployToDeviceDependsOnTargets does not contain _EnsureDeviceBooted"
           Condition="!$(DeployToDeviceDependsOnTargets.Contains('_EnsureDeviceBooted'))" />
    <PropertyGroup>
      <_BootIndex>$(DeployToDeviceDependsOnTargets.IndexOf('_EnsureDeviceBooted'))</_BootIndex>
      <_DeployIndex>$(DeployToDeviceDependsOnTargets.IndexOf('_DeployApk'))</_DeployIndex>
    </PropertyGroup>
    <Error Text="_EnsureDeviceBooted (at $(_BootIndex)) must appear before _DeployApk (at $(_DeployIndex))"
           Condition=" '$(_DeployIndex)' != '-1' And $(_BootIndex) &gt; $(_DeployIndex) " />
  </Target>
</Project>
"""
			};

			var proj = new XamarinAndroidApplicationProject {
				Imports = { checkTargets }
			};

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.RunTarget (proj, "_CheckDeployOrder"),
				"Build should have succeeded — _EnsureDeviceBooted must be in DeployToDeviceDependsOnTargets before _DeployApk.");
		}

	}
}
