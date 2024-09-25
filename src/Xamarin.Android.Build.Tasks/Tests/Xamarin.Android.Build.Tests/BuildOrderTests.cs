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

	}
}
