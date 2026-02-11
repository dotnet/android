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
	const uint ExpectedJniPreloadIndexStride = 4;

	[Test]
	public void NativeLibraryJniPreload_IgnoreAllJniPreload_PreserveRequired ([Values] AndroidRuntime runtime)
	{
		const int ExpectedEntryCount = 4; // stride * number_of_libs

		List<EnvironmentHelper.JniPreloads>? allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (runtime);
		if (allPreloads == null) {
			return;
		}
	}

	[Test]
	public void NativeLibraryJniPreload_DefaultsWork ([Values] AndroidRuntime runtime)
	{
		const int ExpectedEntryCount = 4; // stride * number_of_libs

		List<EnvironmentHelper.JniPreloads>? allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (runtime);
		if (allPreloads == null) {
			return;
		}

		foreach (EnvironmentHelper.JniPreloads preloads in allPreloads) {
			Assert.IsTrue (preloads.IndexStride == ExpectedJniPreloadIndexStride, $"JNI preloads index stride should be {ExpectedJniPreloadIndexStride}, was {preloads.IndexStride} instead. Source file: {preloads.SourceFile}");
			Assert.IsTrue (preloads.Entries.Count == ExpectedEntryCount, $"JNI preloads index entry count should be {ExpectedEntryCount}, was {preloads.Entries.Count} instead. Source file: {preloads.SourceFile}");

			// DSO cache entries are sorted based on their **mutated name's** 64-bit xxHash, which
			// won't change but builds may add/remove libraries and, thus, change the indexes after
			// sorting. For that reason we don't verify the index values and use them just for reporting.
			//
			// Also, all the entries will point to the same library name. Name variations aren't
			// stored directly in the DSO cache, just their hashes which are used for lookup at run time.
			//
			// We use a Dictionary<> here because there might be more libraries to preload in the future.
			var expectedLibNames = new Dictionary<string, uint> (StringComparer.Ordinal) {
				{ "libSystem.Security.Cryptography.Native.Android.so", 0 },
			};

			for (int i = 0; i < preloads.Entries.Count; i++) {
				EnvironmentHelper.JniPreloadsEntry entry = preloads.Entries[i];
				Assert.IsTrue (expectedLibNames.ContainsKey (entry.LibraryName), $"JNI preloads entry at index {i}, referring to library at DSO cache index {entry.Index} has unexpected name '{entry.LibraryName}';  Source file: {preloads.SourceFile}");
				expectedLibNames[entry.LibraryName]++;
			}

			foreach (var kvp in expectedLibNames) {
				Assert.IsTrue (kvp.Value == ExpectedJniPreloadIndexStride, $"JNI preloads entry '{kvp.Key}' should have {ExpectedJniPreloadIndexStride} instances, it had {kvp.Value} instead. Source file: {preloads.SourceFile}");
			}
		}
	}

	List<EnvironmentHelper.JniPreloads>? NativeLibraryJniPreload_CommonInitAndGetPreloads (AndroidRuntime runtime)
	{
		const bool isRelease = true;
		if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
			return null;
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

		return EnvironmentHelper.ReadJniPreloads (envFiles, numberOfDsoCacheEntries, runtime);
	}
}
