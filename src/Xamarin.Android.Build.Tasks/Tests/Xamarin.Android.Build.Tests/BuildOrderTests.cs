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
			// Use MSBuild property functions for validation since the property value is multi-line.
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
