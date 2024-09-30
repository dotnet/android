using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// <para>
/// Puts passed files inside a real ELF shared library so that they
/// pass scrutiny when examined.  The payload is placed inside its own
/// section of a file, so the entire file is a 100% valid ELF image.
/// </para>
///
/// <para>
/// The generated files have their payload section positioned at the offset of
/// 16k (0x4000) from the beginning of file.  It's done this way because it not
/// only gives us enough room for the stub part of the ELF image to precede that
/// offset, but it also complies with Google policy of aligning to 16k **and**
/// is still nicely aligned to a 4k boundary on 32-bit systems.  This helps mmapping
/// the section on both 64-bit and 32-bit systems.
/// </para>
/// </summary>
/// <remarks>
/// The generated file **MUST NOT** be stripped with `llvm-strip` etc,
/// as it will remove the payload together with other sections it deems
/// unnecessary.
/// </remarks>
class DSOWrapperGenerator
{
	internal const string RegisteredConfigKey = ".:!DSOWrapperGeneratorConfig!:.";

	internal class Config
	{
		public Dictionary<AndroidTargetArch, ITaskItem> DSOStubPaths { get; }
		public string AndroidBinUtilsDirectory                       { get; }
		public string BaseOutputDirectory                            { get; }

		public Config (Dictionary<AndroidTargetArch, ITaskItem> stubPaths, string androidBinUtilsDirectory, string baseOutputDirectory)
		{
			DSOStubPaths = stubPaths;
			AndroidBinUtilsDirectory = androidBinUtilsDirectory;
			BaseOutputDirectory = baseOutputDirectory;
		}
	};

	//
	// Must be the same avalue as ARCHIVE_DSO_STUB_PAYLOAD_SECTION_ALIGNMENT in src/native/CMakeLists.txt
	//
	const ulong PayloadSectionAlignment = 0x4000;

	public static Config GetConfig (TaskLoggingHelper log, ITaskItem[] nativeLibraries, string androidBinUtilsDirectory, string baseOutputDirectory)
	{
		var stubPaths = new Dictionary<AndroidTargetArch, ITaskItem> ();

		foreach (ITaskItem stubItem in nativeLibraries) {
			if (Path.GetFileName (stubItem.ItemSpec) != "libarchive-dso-stub.so") {
				continue;
			}

			string rid = stubItem.GetRequiredMetadata ("ArchiveDSOStub", "RuntimeIdentifier", log);
			AndroidTargetArch arch = MonoAndroidHelper.RidToArch (rid);
			if (stubPaths.ContainsKey (arch)) {
				throw new InvalidOperationException ($"Internal error: duplicate archive DSO stub architecture '{arch}' (RID: '{rid}')");
			}

			if (!File.Exists (stubItem.ItemSpec)) {
				throw new InvalidOperationException ($"Internal error: archive DSO stub file '{stubItem.ItemSpec}' does not exist");
			}

			stubPaths.Add (arch, stubItem);
		}

		return new Config (stubPaths, androidBinUtilsDirectory, baseOutputDirectory);
	}

	static string GetArchOutputPath (AndroidTargetArch targetArch, Config config)
	{
		return Path.Combine (config.BaseOutputDirectory, MonoAndroidHelper.ArchToRid (targetArch), "wrapped");
	}

	/// <summary>
	/// Puts the indicated file (<paramref name="payloadFilePath"/>) inside an ELF shared library and returns
	/// path to the wrapped file.
	/// </summary>
	public static string WrapIt (TaskLoggingHelper log, Config config, AndroidTargetArch targetArch, string payloadFilePath, string outputFileName)
	{
		log.LogDebugMessage ($"[{targetArch}] Putting '{payloadFilePath}' inside ELF shared library '{outputFileName}'");
		string outputDir = GetArchOutputPath (targetArch, config);
		Directory.CreateDirectory (outputDir);

		string outputFile = Path.Combine (outputDir, outputFileName);
		log.LogDebugMessage ($"  output file path: {outputFile}");

		if (!config.DSOStubPaths.TryGetValue (targetArch, out ITaskItem? stubItem)) {
			throw new InvalidOperationException ($"Internal error: archive DSO stub location not known for architecture '{targetArch}'");
		}

		File.Copy (stubItem.ItemSpec, outputFile, overwrite: true);

		string quotedOutputFile = MonoAndroidHelper.QuoteFileNameArgument (outputFile);
		string objcopy = Path.Combine (config.AndroidBinUtilsDirectory, MonoAndroidHelper.GetExecutablePath (config.AndroidBinUtilsDirectory, "llvm-objcopy"));
		var args = new List<string> {
			"--add-section",
			$"payload={MonoAndroidHelper.QuoteFileNameArgument (payloadFilePath)}",
			quotedOutputFile,
		};

		int ret = MonoAndroidHelper.RunProcess (objcopy, String.Join (" ", args), log);
		if (ret != 0) {
			return outputFile;
		}

		args.Clear ();
		args.Add ("--set-section-flags");
		args.Add ("payload=readonly,data");
		args.Add ($"--set-section-alignment payload={PayloadSectionAlignment}");
		args.Add (quotedOutputFile);
		ret = MonoAndroidHelper.RunProcess (objcopy, String.Join (" ", args), log);

		return outputFile;
	}

	/// <summary>
	/// Call when all packaging is done.  The method will remove all the wrapper shared libraries that were previously
	/// created by this class.  The reason to do so is to ensure that we don't package any "stale" content and those
	/// wrapper files aren't part of any dependency chain so it's hard to check their up to date state.
	/// </summary>
	public static void CleanUp (Config config)
	{
		foreach (var kvp in config.DSOStubPaths) {
			string outputDir = GetArchOutputPath (kvp.Key, config);
			if (!Directory.Exists (outputDir)) {
				continue;
			}

			Directory.Delete (outputDir, recursive: true);
		}
	}
}
