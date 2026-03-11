using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[Parallelizable (ParallelScope.Children)]
public partial class BuildTest3 : BaseTest
{
	const int ExpectedJniPreloadIndexStride = 4;
	const string JniPreloadSourceLibraryName = "libtest-jni-library.so";

	[Test]
	public void NativeLibraryJniPreload_NoDuplicates ([Values] AndroidRuntime runtime)
	{
		const string MyLibKeep1 = "libMyStuffKeep.so";
		const string MyLibKeep2 = "libMyStuffKeep.so";

		List<EnvironmentHelper.JniPreloads>? allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				NativeLibraryJniPreload_AddNativeLibraries (proj, supportedArches, MyLibKeep1, MyLibKeep2);
			}
		);
		if (allPreloads == null) {
			return;
		}

		NativeLibraryJniPreload_VerifyLibs (allPreloads, new List<string> { MyLibKeep1 });
	}

	[Test]
	public void NativeLibraryJniPreload_IncludeCustomLibraries ([Values] AndroidRuntime runtime)
	{
		const string MyLib = "libMyStuff.so";

		List<EnvironmentHelper.JniPreloads>? allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				NativeLibraryJniPreload_AddNativeLibraries (proj, supportedArches, MyLib);
			}
		);
		if (allPreloads == null) {
			return;
		}

		NativeLibraryJniPreload_VerifyLibs (allPreloads, new List<string> { MyLib });
	}

	[Test]
	public void NativeLibraryJniPreload_ExcludeSomeCustomLibraries ([Values] AndroidRuntime runtime)
	{
		const string MyLibKeep = "libMyStuffKeep.so";
		const string MyLibExempt = "libMyStuffExempt.so";

		List<EnvironmentHelper.JniPreloads>? allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				NativeLibraryJniPreload_AddNativeLibraries (proj, supportedArches, MyLibKeep, MyLibExempt);
				proj.OtherBuildItems.Add (
					new AndroidItem.AndroidNativeLibraryNoJniPreload (MyLibExempt)
				);
			}
		);
		if (allPreloads == null) {
			return;
		}

		NativeLibraryJniPreload_VerifyLibs (allPreloads, new List<string> { MyLibKeep });
	}

	[Test]
	public void NativeLibraryJniPreload_ExcludeAllCustomLibraries ([Values] AndroidRuntime runtime)
	{
		const string MyLibExempt1 = "libMyStuffExempt1.so";
		const string MyLibExempt2 = "libMyStuffExempt2.so";

		List<EnvironmentHelper.JniPreloads>? allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				NativeLibraryJniPreload_AddNativeLibraries (proj, supportedArches, MyLibExempt1, MyLibExempt2);
				proj.OtherBuildItems.Add (
					new AndroidItem.AndroidNativeLibraryNoJniPreload (MyLibExempt1)
				);
				proj.OtherBuildItems.Add (
					new AndroidItem.AndroidNativeLibraryNoJniPreload (MyLibExempt2)
				);
			}
		);
		if (allPreloads == null) {
			return;
		}

		NativeLibraryJniPreload_VerifyDefaults (allPreloads);
	}

	[Test]
	public void NativeLibraryJniPreload_AddSomeCustomLibrariesAndIgnoreAll ([Values] AndroidRuntime runtime)
	{
		List<EnvironmentHelper.JniPreloads>? allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				NativeLibraryJniPreload_AddNativeLibraries (proj, supportedArches, "libMyStuffOne.so", "libMyStuffTwo.so");
				proj.SetProperty ("AndroidIgnoreAllJniPreload", "true");
			}
		);
		if (allPreloads == null) {
			return;
		}

		// With `$(AndroidIgnoreAllJniPreload)=true` we still must have the defaults in the generated code.
		NativeLibraryJniPreload_VerifyDefaults (allPreloads);
	}

	[Test]
	public void NativeLibraryJniPreload_AddSomeCustomLibrariesAndIgnoreAllByName ([Values] AndroidRuntime runtime)
	{
		const string MyLibExemptOne = "libMyStuffExemptOne.so";
		const string MyLibExemptTwo = "libMyStuffExemptTwo.so";

		List<EnvironmentHelper.JniPreloads>? allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				NativeLibraryJniPreload_AddNativeLibraries (proj, supportedArches, MyLibExemptOne, MyLibExemptTwo);
				proj.OtherBuildItems.Add (
					new AndroidItem.AndroidNativeLibraryNoJniPreload (MyLibExemptOne)
				);
				proj.OtherBuildItems.Add (
					new AndroidItem.AndroidNativeLibraryNoJniPreload (MyLibExemptTwo)
				);
			}
		);
		if (allPreloads == null) {
			return;
		}

		// With all custom libraries ignored, we still must have the defaults in the generated code.
		NativeLibraryJniPreload_VerifyDefaults (allPreloads);
	}

	void NativeLibraryJniPreload_AddNativeLibraries (XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches, string libName, params string[]? moreLibNames)
	{
		var libNames = new List<string> {
			libName,
		};
		if (moreLibNames != null && moreLibNames.Length > 0) {
			libNames.AddRange (moreLibNames);
		}

		foreach (AndroidTargetArch arch in supportedArches) {
			string libPath = Path.Combine (XABuildPaths.TestAssemblyOutputDirectory, MonoAndroidHelper.ArchToRid (arch), JniPreloadSourceLibraryName);
			Assert.IsTrue (File.Exists (libPath), $"Native library '{libPath}' does not exist.");

			foreach (string lib in libNames) {
				string abi = MonoAndroidHelper.ArchToAbi (arch);
				proj.OtherBuildItems.Add (
					new AndroidItem.AndroidNativeLibrary ($"native/{abi}/{lib}") {
						BinaryContent = () => File.ReadAllBytes (libPath),
						MetadataValues = $"Link={abi}/{lib}",
					}
				);
			}
		}
	}

	[Test]
	public void NativeLibraryJniPreload_IgnoreAll_PreservesRequired ([Values] AndroidRuntime runtime)
	{
		List<EnvironmentHelper.JniPreloads>? allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				proj.SetProperty ("AndroidIgnoreAllJniPreload", "true");
			}
		);

		// With `$(AndroidIgnoreAllJniPreload)=true` we still must have the defaults in the generated code.
		NativeLibraryJniPreload_VerifyDefaults (allPreloads);
	}

	[Test]
	public void NativeLibraryJniPreload_DefaultsWork ([Values] AndroidRuntime runtime)
	{
		List<EnvironmentHelper.JniPreloads>? allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (runtime);
		NativeLibraryJniPreload_VerifyDefaults (allPreloads);
	}

	void NativeLibraryJniPreload_VerifyDefaults (List<EnvironmentHelper.JniPreloads>? allPreloads)
	{
		NativeLibraryJniPreload_VerifyLibs (allPreloads, additionalLibs: null);
	}

	void NativeLibraryJniPreload_VerifyLibs (List<EnvironmentHelper.JniPreloads>? allPreloads, List<string>? additionalLibs)
	{
		if (allPreloads == null) {
			return;
		}

		int numberOfLibs = 1;
		if (additionalLibs != null) {
			numberOfLibs += additionalLibs.Count;
		}

		int ExpectedEntryCount = ExpectedJniPreloadIndexStride * numberOfLibs;
		foreach (EnvironmentHelper.JniPreloads preloads in allPreloads) {
			Assert.IsTrue (preloads.IndexStride == (uint)ExpectedJniPreloadIndexStride, $"JNI preloads index stride should be {ExpectedJniPreloadIndexStride}, was {preloads.IndexStride} instead. Source file: {preloads.SourceFile}");
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

			if (additionalLibs != null) {
				foreach (string extraLib in additionalLibs) {
					expectedLibNames.Add (extraLib, 0);
				}
			}

			for (int i = 0; i < preloads.Entries.Count; i++) {
				EnvironmentHelper.JniPreloadsEntry entry = preloads.Entries[i];
				Assert.IsFalse (entry.LibraryName == "libmonodroid.so", $"JNI preloads entry at index {i} refers to the .NET for Android native runtime. It must never be preloaded. Source file: {preloads.SourceFile}");
				Assert.IsTrue (expectedLibNames.ContainsKey (entry.LibraryName), $"JNI preloads entry at index {i}, referring to library at DSO cache index {entry.Index} has unexpected name '{entry.LibraryName}';  Source file: {preloads.SourceFile}");
				expectedLibNames[entry.LibraryName]++;
			}

			foreach (var kvp in expectedLibNames) {
				Assert.IsTrue (kvp.Value == ExpectedJniPreloadIndexStride, $"JNI preloads entry '{kvp.Key}' should have {ExpectedJniPreloadIndexStride} instances, it had {kvp.Value} instead. Source file: {preloads.SourceFile}");
			}
		}
	}

	List<EnvironmentHelper.JniPreloads>? NativeLibraryJniPreload_CommonInitAndGetPreloads (AndroidRuntime runtime, Action<XamarinAndroidApplicationProject, AndroidTargetArch[]>? configureProject = null)
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
		configureProject?.Invoke (proj, supportedArches);

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
