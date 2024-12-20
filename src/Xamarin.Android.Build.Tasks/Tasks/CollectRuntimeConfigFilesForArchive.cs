#nullable enable

using System;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Collects rc.bin to be added to the final archive.
/// </summary>
public class CollectRuntimeConfigFilesForArchive : AndroidTask
{
	const string ArchiveLibPath = "lib";

	public override string TaskPrefix => "CRF";

	[Required]
	public string AndroidBinUtilsDirectory { get; set; } = "";

	[Required]
	public string IntermediateOutputPath { get; set; } = "";

	public string RuntimeConfigBinFilePath { get; set; } = "";

	[Required]
	public string [] SupportedAbis { get; set; } = [];

	[Output]
	public ITaskItem [] FilesToAddToArchive { get; set; } = [];

	public override bool RunTask ()
	{
		var files = new PackageFileListBuilder ();
		var dsoWrapperConfig = DSOWrapperGenerator.GetConfig (Log, AndroidBinUtilsDirectory, IntermediateOutputPath);

		// We will place rc.bin in the `lib` directory next to the blob, to make startup slightly faster, as we will find the config file right after we encounter
		// our assembly store.  Not only that, but also we'll be able to skip scanning the `base.apk` archive when split configs are enabled (which they are in 99%
		// of cases these days, since AAB enforces that split).  `base.apk` contains only ABI-agnostic file, while one of the split config files contains only
		// ABI-specific data+code.
		if (!string.IsNullOrEmpty (RuntimeConfigBinFilePath) && File.Exists (RuntimeConfigBinFilePath)) {
			foreach (var abi in SupportedAbis) {
				// Prefix it with `a` because bundletool sorts entries alphabetically, and this will place it right next to `assemblies.*.blob.so`, which is what we
				// like since we can finish scanning the zip central directory earlier at startup.
				var inArchivePath = MakeArchiveLibPath (abi, "libarc.bin.so");
				var wrappedSourcePath = DSOWrapperGenerator.WrapIt (Log, dsoWrapperConfig, MonoAndroidHelper.AbiToTargetArch (abi), RuntimeConfigBinFilePath, Path.GetFileName (inArchivePath));
				files.AddItem (wrappedSourcePath, inArchivePath);
			}
		}

		FilesToAddToArchive = files.ToArray ();

		return !Log.HasLoggedErrors;
	}

	static string MakeArchiveLibPath (string abi, string fileName) => MonoAndroidHelper.MakeZipArchivePath (ArchiveLibPath, abi, fileName);
}
