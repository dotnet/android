using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[Parallelizable (ParallelScope.Children)]
public partial class BuildTest3 : BaseTest
{
	const string JniPreloadSourceLibraryName = "libtest-jni-library.so";

	[Test]
	public void NativeLibraryJniPreload_NoDuplicates ([Values (AndroidRuntime.CoreCLR)] AndroidRuntime runtime)
	{
		const string MyLibKeep1 = "libMyStuffKeep.so";
		const string MyLibKeep2 = "libMyStuffKeep.so";

		List<string> allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				NativeLibraryJniPreload_AddNativeLibraries (proj, supportedArches, MyLibKeep1, MyLibKeep2);
			}
		);
		NativeLibraryJniPreload_VerifyLibs (allPreloads, new List<string> { MyLibKeep1 });
	}

	[Test]
	public void NativeLibraryJniPreload_IncludeCustomLibraries ([Values (AndroidRuntime.CoreCLR)] AndroidRuntime runtime)
	{
		const string MyLib = "libMyStuff.so";

		List<string> allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				NativeLibraryJniPreload_AddNativeLibraries (proj, supportedArches, MyLib);
			}
		);
		NativeLibraryJniPreload_VerifyLibs (allPreloads, new List<string> { MyLib });
	}

	[Test]
	public void NativeLibraryJniPreload_ExcludeSomeCustomLibraries ([Values (AndroidRuntime.CoreCLR)] AndroidRuntime runtime)
	{
		const string MyLibKeep = "libMyStuffKeep.so";
		const string MyLibExempt = "libMyStuffExempt.so";

		List<string> allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				NativeLibraryJniPreload_AddNativeLibraries (proj, supportedArches, MyLibKeep, MyLibExempt);
				proj.OtherBuildItems.Add (
					new AndroidItem.AndroidNativeLibraryNoJniPreload (MyLibExempt)
				);
			}
		);
		NativeLibraryJniPreload_VerifyLibs (allPreloads, new List<string> { MyLibKeep });
	}

	[Test]
	public void NativeLibraryJniPreload_ExcludeAllCustomLibraries ([Values (AndroidRuntime.CoreCLR)] AndroidRuntime runtime)
	{
		const string MyLibExempt1 = "libMyStuffExempt1.so";
		const string MyLibExempt2 = "libMyStuffExempt2.so";

		List<string> allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (
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
		NativeLibraryJniPreload_VerifyDefaults (allPreloads);
	}

	[Test]
	public void NativeLibraryJniPreload_AddSomeCustomLibrariesAndIgnoreAll ([Values (AndroidRuntime.CoreCLR)] AndroidRuntime runtime)
	{
		List<string> allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				NativeLibraryJniPreload_AddNativeLibraries (proj, supportedArches, "libMyStuffOne.so", "libMyStuffTwo.so");
				proj.SetProperty ("AndroidIgnoreAllJniPreload", "true");
			}
		);
		// With `$(AndroidIgnoreAllJniPreload)=true` we still must have the defaults in the generated code.
		NativeLibraryJniPreload_VerifyDefaults (allPreloads);
	}

	[Test]
	public void NativeLibraryJniPreload_AddSomeCustomLibrariesAndIgnoreAllByName ([Values (AndroidRuntime.CoreCLR)] AndroidRuntime runtime)
	{
		const string MyLibExemptOne = "libMyStuffExemptOne.so";
		const string MyLibExemptTwo = "libMyStuffExemptTwo.so";

		List<string> allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (
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
	public void NativeLibraryJniPreload_IgnoreAll_PreservesRequired ([Values (AndroidRuntime.CoreCLR)] AndroidRuntime runtime)
	{
		List<string> allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				proj.SetProperty ("AndroidIgnoreAllJniPreload", "true");
			}
		);

		// With `$(AndroidIgnoreAllJniPreload)=true` we still must have the defaults in the generated code.
		NativeLibraryJniPreload_VerifyDefaults (allPreloads);
	}

	[Test]
	public void NativeLibraryJniPreload_DefaultsWork ([Values (AndroidRuntime.CoreCLR)] AndroidRuntime runtime)
	{
		List<string> allPreloads = NativeLibraryJniPreload_CommonInitAndGetPreloads (runtime);
		NativeLibraryJniPreload_VerifyDefaults (allPreloads);
	}

	void NativeLibraryJniPreload_VerifyDefaults (List<string> allPreloads)
	{
		NativeLibraryJniPreload_VerifyLibs (allPreloads, additionalLibs: null);
	}

	void NativeLibraryJniPreload_VerifyLibs (List<string> allPreloads, List<string>? additionalLibs)
	{
		var expected = new List<string> { "libSystem.Security.Cryptography.Native.Android.so" };
		expected.AddRange (additionalLibs ?? []);
		Assert.That (allPreloads, Is.EquivalentTo (expected), "Each JNI library should be preloaded once.");
		Assert.That (allPreloads, Does.Not.Contain ("libmonodroid.so"), "The Android runtime is already loaded by Java.");
	}

	List<string> NativeLibraryJniPreload_CommonInitAndGetPreloads (AndroidRuntime runtime, Action<XamarinAndroidApplicationProject, AndroidTargetArch[]>? configureProject = null)
	{
		const bool isRelease = true;
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
		string source = File.ReadAllText (Path.Combine (objDirPath, "android", "src", "net", "dot", "android", "AppBootstrapConfig.java"));
		Match names = Regex.Match (source, @"NativeLibraries = new String\[\] \{(?<values>.*?)\};", RegexOptions.Singleline);
		Match flags = Regex.Match (source, @"NativeLibraryFlags = new byte\[\] \{(?<values>.*?)\};", RegexOptions.Singleline);
		Assert.That (names.Success && flags.Success, Is.True, "Java bootstrap must contain both native library arrays.");
		var libraries = Regex.Matches (names.Groups ["values"].Value, "\"([^\"]+)\"").Select (match => match.Groups [1].Value).ToArray ();
		var values = Regex.Matches (flags.Groups ["values"].Value, @"\d+").Select (match => byte.Parse (match.Value)).ToArray ();
		Assert.That (libraries.Length, Is.EqualTo (values.Length));
		return libraries.Where ((_, index) => (values [index] & 2) != 0).ToList ();
	}
}
