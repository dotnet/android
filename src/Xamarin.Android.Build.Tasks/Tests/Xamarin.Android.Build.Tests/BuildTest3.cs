using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[Parallelizable (ParallelScope.Children)]
public partial class BuildTest3 : BaseTest
{
	// Keep in sync with the flags written by GenerateJavaApplicationConfig.WriteSource into
	// AppBootstrapConfig.java's `NativeLibraryFlags` array.
	const byte NativeLibraryFlagIsJniLibrary = 1;
	const byte NativeLibraryFlagPreload = 2;

	const string CryptoNativeLibraryName = "libSystem.Security.Cryptography.Native.Android.so";
	const string MonodroidNativeLibraryName = "libmonodroid.so";

	const string JniPreloadSourceLibraryName = "libtest-jni-library.so";

	[Test]
	public void NativeLibraryJniPreload_NoDuplicates ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
	{
		const string MyLibKeep1 = "libMyStuffKeep.so";
		const string MyLibKeep2 = "libMyStuffKeep.so";

		Dictionary<string, byte>? flags = NativeLibraryJniPreload_CommonInitAndGetFlags (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				NativeLibraryJniPreload_AddNativeLibraries (proj, supportedArches, MyLibKeep1, MyLibKeep2);
			}
		);

		NativeLibraryJniPreload_VerifyLibs (flags, preloadedLibs: new List<string> { MyLibKeep1 }, exemptedLibs: null);
	}

	[Test]
	public void NativeLibraryJniPreload_IncludeCustomLibraries ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
	{
		const string MyLib = "libMyStuff.so";

		Dictionary<string, byte>? flags = NativeLibraryJniPreload_CommonInitAndGetFlags (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				NativeLibraryJniPreload_AddNativeLibraries (proj, supportedArches, MyLib);
			}
		);

		NativeLibraryJniPreload_VerifyLibs (flags, preloadedLibs: new List<string> { MyLib }, exemptedLibs: null);
	}

	[Test]
	public void NativeLibraryJniPreload_ExcludeSomeCustomLibraries ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
	{
		const string MyLibKeep = "libMyStuffKeep.so";
		const string MyLibExempt = "libMyStuffExempt.so";

		Dictionary<string, byte>? flags = NativeLibraryJniPreload_CommonInitAndGetFlags (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				NativeLibraryJniPreload_AddNativeLibraries (proj, supportedArches, MyLibKeep, MyLibExempt);
				proj.OtherBuildItems.Add (
					new AndroidItem.AndroidNativeLibraryNoJniPreload (MyLibExempt)
				);
			}
		);

		NativeLibraryJniPreload_VerifyLibs (flags, preloadedLibs: new List<string> { MyLibKeep }, exemptedLibs: new List<string> { MyLibExempt });
	}

	[Test]
	public void NativeLibraryJniPreload_ExcludeAllCustomLibraries ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
	{
		const string MyLibExempt1 = "libMyStuffExempt1.so";
		const string MyLibExempt2 = "libMyStuffExempt2.so";

		Dictionary<string, byte>? flags = NativeLibraryJniPreload_CommonInitAndGetFlags (
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

		NativeLibraryJniPreload_VerifyLibs (flags, preloadedLibs: null, exemptedLibs: new List<string> { MyLibExempt1, MyLibExempt2 });
	}

	[Test]
	public void NativeLibraryJniPreload_AddSomeCustomLibrariesAndIgnoreAll ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
	{
		const string MyLibOne = "libMyStuffOne.so";
		const string MyLibTwo = "libMyStuffTwo.so";

		Dictionary<string, byte>? flags = NativeLibraryJniPreload_CommonInitAndGetFlags (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				NativeLibraryJniPreload_AddNativeLibraries (proj, supportedArches, MyLibOne, MyLibTwo);
				proj.SetProperty ("AndroidIgnoreAllJniPreload", "true");
			}
		);

		// With `$(AndroidIgnoreAllJniPreload)=true` we still must have the defaults in the generated
		// code, while both custom libraries must not be preloaded.
		NativeLibraryJniPreload_VerifyLibs (flags, preloadedLibs: null, exemptedLibs: new List<string> { MyLibOne, MyLibTwo });
	}

	[Test]
	public void NativeLibraryJniPreload_AddSomeCustomLibrariesAndIgnoreAllByName ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
	{
		const string MyLibExemptOne = "libMyStuffExemptOne.so";
		const string MyLibExemptTwo = "libMyStuffExemptTwo.so";

		Dictionary<string, byte>? flags = NativeLibraryJniPreload_CommonInitAndGetFlags (
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
		NativeLibraryJniPreload_VerifyLibs (flags, preloadedLibs: null, exemptedLibs: new List<string> { MyLibExemptOne, MyLibExemptTwo });
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
	public void NativeLibraryJniPreload_IgnoreAll_PreservesRequired ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
	{
		Dictionary<string, byte>? flags = NativeLibraryJniPreload_CommonInitAndGetFlags (
			runtime,
			(XamarinAndroidApplicationProject proj, AndroidTargetArch[] supportedArches) => {
				proj.SetProperty ("AndroidIgnoreAllJniPreload", "true");
			}
		);

		// With `$(AndroidIgnoreAllJniPreload)=true` we still must have the defaults in the generated code.
		NativeLibraryJniPreload_VerifyDefaults (flags);
	}

	[Test]
	public void NativeLibraryJniPreload_DefaultsWork ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
	{
		Dictionary<string, byte>? flags = NativeLibraryJniPreload_CommonInitAndGetFlags (runtime);
		NativeLibraryJniPreload_VerifyDefaults (flags);
	}

	void NativeLibraryJniPreload_VerifyDefaults (Dictionary<string, byte>? flags)
	{
		NativeLibraryJniPreload_VerifyLibs (flags, preloadedLibs: null, exemptedLibs: null);
	}

	// Verifies the `NativeLibraries`/`NativeLibraryFlags` arrays emitted into the generated
	// AppBootstrapConfig.java: the crypto native library must always be preloaded, the .NET for
	// Android native runtime library (libmonodroid.so) must never be preloaded, every library in
	// `preloadedLibs` must have its preload bit set, and every library in `exemptedLibs` must not.
	void NativeLibraryJniPreload_VerifyLibs (Dictionary<string, byte>? flags, List<string>? preloadedLibs, List<string>? exemptedLibs)
	{
		if (flags == null) {
			return;
		}

		Assert.IsTrue (flags.TryGetValue (CryptoNativeLibraryName, out byte cryptoFlags), $"'{CryptoNativeLibraryName}' should always be present in the generated NativeLibraries array.");
		Assert.AreEqual (NativeLibraryFlagIsJniLibrary | NativeLibraryFlagPreload, cryptoFlags, $"'{CryptoNativeLibraryName}' should always be a preloaded JNI library, its flags were {cryptoFlags}.");

		if (flags.TryGetValue (MonodroidNativeLibraryName, out byte monodroidFlags)) {
			Assert.AreEqual (0, monodroidFlags & NativeLibraryFlagPreload, $"'{MonodroidNativeLibraryName}' (.NET for Android native runtime) must never be preloaded, its flags were {monodroidFlags}.");
		}

		foreach (string lib in preloadedLibs ?? Enumerable.Empty<string> ()) {
			Assert.IsTrue (flags.TryGetValue (lib, out byte libFlags), $"'{lib}' should be present in the generated NativeLibraries array.");
			Assert.AreEqual (NativeLibraryFlagIsJniLibrary | NativeLibraryFlagPreload, libFlags, $"'{lib}' should be a preloaded JNI library, its flags were {libFlags}.");
		}

		foreach (string lib in exemptedLibs ?? Enumerable.Empty<string> ()) {
			Assert.IsTrue (flags.TryGetValue (lib, out byte libFlags), $"'{lib}' should be present in the generated NativeLibraries array.");
			Assert.AreEqual (0, libFlags & NativeLibraryFlagPreload, $"'{lib}' should not be preloaded, its flags were {libFlags}.");
		}
	}

	Dictionary<string, byte>? NativeLibraryJniPreload_CommonInitAndGetFlags (AndroidRuntime runtime, Action<XamarinAndroidApplicationProject, AndroidTargetArch[]>? configureProject = null)
	{
		const bool isRelease = true;
		if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
			return null;
		}

		if (runtime == AndroidRuntime.NativeAOT) {
			Assert.Ignore ("NativeAOT doesn't preload JNI native libraries yet");
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

		string androidIntermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath, "android");
		string[] javaConfigFiles = Directory.GetFiles (androidIntermediate, "AppBootstrapConfig.java", SearchOption.AllDirectories);
		Assert.AreEqual (1, javaConfigFiles.Length, $"Exactly one AppBootstrapConfig.java should have been generated under '{androidIntermediate}'.");

		return JavaBootstrapConfig_ReadNativeLibraryFlags (javaConfigFiles[0]);
	}

	static Dictionary<string, byte> JavaBootstrapConfig_ReadNativeLibraryFlags (string javaConfigPath)
	{
		string source = File.ReadAllText (javaConfigPath);
		List<string> names = JavaBootstrapConfig_ExtractStringArray (source, "NativeLibraries", javaConfigPath);
		List<byte> flags = JavaBootstrapConfig_ExtractByteArray (source, "NativeLibraryFlags", javaConfigPath);

		Assert.AreEqual (names.Count, flags.Count, $"'NativeLibraries' and 'NativeLibraryFlags' arrays must have the same number of entries in '{javaConfigPath}'.");

		// Use Add (rather than the indexer) so a library name emitted more than once throws
		// instead of silently overwriting its flags, which would otherwise hide a dedup
		// regression in the generator.
		var result = new Dictionary<string, byte> (StringComparer.Ordinal);
		for (int i = 0; i < names.Count; i++) {
			result.Add (names[i], flags[i]);
		}

		Assert.AreEqual (names.Count, result.Count, $"The generated NativeLibraries array in '{javaConfigPath}' must not contain duplicate library names.");

		return result;
	}

	static string JavaBootstrapConfig_ExtractArrayLiteral (string source, string fieldName, string javaTypeName, string javaConfigPath)
	{
		string marker = $"{javaTypeName} {fieldName} = new {javaTypeName} {{";
		int start = source.IndexOf (marker, StringComparison.Ordinal);
		Assert.IsTrue (start >= 0, $"Field '{fieldName}' was not found in '{javaConfigPath}'.");

		int contentStart = start + marker.Length;
		int end = source.IndexOf ("};", contentStart, StringComparison.Ordinal);
		Assert.IsTrue (end >= 0, $"The array literal for field '{fieldName}' was not properly closed in '{javaConfigPath}'.");

		return source.Substring (contentStart, end - contentStart);
	}

	static List<string> JavaBootstrapConfig_ExtractStringArray (string source, string fieldName, string javaConfigPath)
	{
		string arrayLiteral = JavaBootstrapConfig_ExtractArrayLiteral (source, fieldName, "String[]", javaConfigPath);
		var values = new List<string> ();
		foreach (Match match in Regex.Matches (arrayLiteral, "\"((?:[^\"\\\\]|\\\\.)*)\"")) {
			values.Add (JavaBootstrapConfig_UnescapeJavaString (match.Groups[1].Value));
		}

		return values;
	}

	static List<byte> JavaBootstrapConfig_ExtractByteArray (string source, string fieldName, string javaConfigPath)
	{
		string arrayLiteral = JavaBootstrapConfig_ExtractArrayLiteral (source, fieldName, "byte[]", javaConfigPath);
		var values = new List<byte> ();
		foreach (Match match in Regex.Matches (arrayLiteral, @"(\d+)\s*,")) {
			values.Add (byte.Parse (match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture));
		}

		return values;
	}

	static string JavaBootstrapConfig_UnescapeJavaString (string javaString)
	{
		var sb = new StringBuilder (javaString.Length);
		for (int i = 0; i < javaString.Length; i++) {
			char c = javaString[i];
			if (c != '\\' || i + 1 >= javaString.Length) {
				sb.Append (c);
				continue;
			}

			i++;
			switch (javaString[i]) {
				case 'n': sb.Append ('\n'); break;
				case 'r': sb.Append ('\r'); break;
				case 't': sb.Append ('\t'); break;
				case 'b': sb.Append ('\b'); break;
				case 'f': sb.Append ('\f'); break;
				case '"': sb.Append ('"'); break;
				case '\\': sb.Append ('\\'); break;
				default: sb.Append (javaString[i]); break;
			}
		}

		return sb.ToString ();
	}
}
