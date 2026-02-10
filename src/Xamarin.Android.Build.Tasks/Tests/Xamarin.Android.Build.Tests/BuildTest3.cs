using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Build.Framework;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Build.Tests;

[Parallelizable (ParallelScope.Children)]
public partial class BuildTest3 : BaseTest
{
	[Test]
	public void NativeLibraryJniPreloadDefaultsWork ([Values] AndroidRuntime runtime)
	{
		const bool isRelease = true;
		if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
			return;
		}

		if (runtime == AndroidRuntime.NativeAOT) {
			Assert.Ignore ("NativeAOT doesn't use JNI preload");
		}

		AndroidTargetArch[] supportedArches = new [] {
			AndroidTargetArch.Arm64,
			AndroidTargetArch.X86_64,
		};

		var proj = new XamarinAndroidApplicationProject {
			IsRelease = isRelease,
		};
		proj.SetRuntime (runtime);
		proj.SetRuntimeIdentifiers (supportedArches);

		using var builder = CreateApkBuilder ();
		Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

		string objDirPath = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
		List<EnvironmentHelper.EnvironmentFile> envFiles = EnvironmentHelper.GatherEnvironmentFiles (
			objDirPath,
			String.Join (";", supportedArches.Select (arch => MonoAndroidHelper.ArchToAbi (arch))),
			true
		);

		EnvironmentHelper.IApplicationConfig app_config = EnvironmentHelper.ReadApplicationConfig (envFiles, runtime);
		uint numberOfDsoCacheEntries = runtime switch {
			AndroidRuntime.MonoVM  => ((EnvironmentHelper.ApplicationConfig_MonoVM)app_config).number_of_dso_cache_entries,
			AndroidRuntime.CoreCLR => ((EnvironmentHelper.ApplicationConfig_CoreCLR)app_config).number_of_dso_cache_entries,
			_                      => throw new NotSupportedException ($"Unsupported runtime '{runtime}'")
		};

		List<EnvironmentHelper.JniPreloads> preloads = EnvironmentHelper.ReadJniPreloads (envFiles, numberOfDsoCacheEntries, runtime);
	}
}
