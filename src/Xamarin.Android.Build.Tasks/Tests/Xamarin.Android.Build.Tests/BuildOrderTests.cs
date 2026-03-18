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

			string dependsOn = null;
			foreach (var line in builder.LastBuildOutput) {
				if (line.Contains ("DeployToDeviceDependsOnTargets=")) {
					dependsOn = line;
					break;
				}
			}

			Assert.IsNotNull (dependsOn, "DeployToDeviceDependsOnTargets property should be logged");
			StringAssert.Contains ("_EnsureDeviceBooted", dependsOn,
				"DeployToDeviceDependsOnTargets must contain _EnsureDeviceBooted");

			// _EnsureDeviceBooted must appear before _DeployApk to set AdbTarget first
			int bootIndex = dependsOn.IndexOf ("_EnsureDeviceBooted", StringComparison.Ordinal);
			int deployIndex = dependsOn.IndexOf ("_DeployApk", StringComparison.Ordinal);
			if (deployIndex >= 0) {
				Assert.Less (bootIndex, deployIndex,
					"_EnsureDeviceBooted must appear before _DeployApk in DeployToDeviceDependsOnTargets");
			}
		}

	}
}
