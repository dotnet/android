#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Takes a list of files and adds them to an APK archive. If the APK archive already
/// exists, files are only added if they were changed. Note *ALL* files to be in the final
/// APK must be passed in via @(FilesToAddToArchive). This task will determine any unchanged files
/// and skip them, as well as remove any existing files in the APK that are no longer required.
/// </summary>
public class BuildArchive : AndroidTask
{
	public override string TaskPrefix => "BAA";

	public string? AndroidPackageFormat { get; set; }

	public string? ApkInputPath { get; set; }

	[Required]
	public string ApkOutputPath { get; set; } = "";

	[Required]
	public ITaskItem [] FilesToAddToArchive { get; set; } = [];

	public string? UncompressedFileExtensions { get; set; }

	public bool UseLibZipSharp { get; set; }

	public string? ZipFlushFilesLimit { get; set; }

	public string? ZipFlushSizeLimit { get; set; }

	HashSet<string>? uncompressedFileExtensions;
	HashSet<string> UncompressedFileExtensionsSet => uncompressedFileExtensions ??= ParseUncompressedFileExtensions ();

	CompressionMethod uncompressedMethod = CompressionMethod.Store;

	public override bool RunTask ()
	{
		var is_aab = string.Compare (AndroidPackageFormat, "aab", true) == 0;

		// Nothing needs to be compressed with app bundles. BundleConfig.json specifies the final compression mode.
		if (is_aab)
			uncompressedMethod = CompressionMethod.Default;

		var refresh = true;

		// If we have an input apk but no output apk, copy it to the output
		// so we don't modify the original.
		if (ApkInputPath is not null && File.Exists (ApkInputPath) && !File.Exists (ApkOutputPath)) {
			Log.LogDebugMessage ($"Copying {ApkInputPath} to {ApkOutputPath}");
			File.Copy (ApkInputPath, ApkOutputPath, overwrite: true);
			refresh = false;
		}

		using var apk = ZipArchiveDotNet.Create (Log, ApkOutputPath, System.IO.Compression.ZipArchiveMode.Update, UseLibZipSharp);

		// Set up AutoFlush
		if (int.TryParse (ZipFlushFilesLimit, out int flushFilesLimit)) {
			apk.ZipFlushFilesLimit = flushFilesLimit;
		}

		if (int.TryParse (ZipFlushSizeLimit, out int flushSizeLimit)) {
			apk.ZipFlushSizeLimit = flushSizeLimit;
		}

		// If we're modifying an existing APK we need to track what entries we started
		// with so we can remove any existing entries that are no longer used.
		var existingEntries = new List<string> ();

		if (refresh) {
			foreach (var entry in apk.GetAllEntryNames ()) {
				Log.LogDebugMessage ($"Registering item {entry}");
				existingEntries.Add (entry);
			}
		}

		// If we're modifying an existing APK we need to update any out
		// of date entries in the output APK from the input APK.
		if (ApkInputPath is not null && File.Exists (ApkInputPath) && refresh) {

			var lastWriteOutput = File.Exists (ApkOutputPath) ? File.GetLastWriteTimeUtc (ApkOutputPath) : DateTime.MinValue;
			var lastWriteInput = File.GetLastWriteTimeUtc (ApkInputPath);

			using (var packaged = new ZipArchiveEx (ApkInputPath, FileMode.Open)) {
				foreach (var entry in packaged.Archive) {

					// NOTE: aapt2 is creating zip entries on Windows such as `assets\subfolder/asset2.txt`
					var entryName = entry.FullName;

					if (entryName.Contains ("\\")) {
						entryName = entryName.Replace ('\\', '/');
						Log.LogDebugMessage ($"Fixing up malformed entry `{entry.FullName}` -> `{entryName}`");
					}

					if (entryName == "AndroidManifest.xml" && is_aab) {
						Log.LogDebugMessage ("Renaming AndroidManifest.xml to manifest/AndroidManifest.xml");
						entryName = "manifest/AndroidManifest.xml";
					}

					Log.LogDebugMessage ($"Deregistering item {entryName}");
					existingEntries.Remove (entryName);

					if (lastWriteInput <= lastWriteOutput) {
						Log.LogDebugMessage ($"Skipping to next item. {lastWriteInput} <= {lastWriteOutput}.");
						continue;
					}

					if (apk.ContainsEntry (entryName)) {
						var e = apk.GetEntry (entryName);
						// check the CRC values as the ModifiedDate is always 01/01/1980 in the aapt generated file.
						if (entry.CRC == e.CRC && entry.CompressedSize == e.CompressedSize) {
							Log.LogDebugMessage ($"Skipping {entryName} from {ApkInputPath} as its up to date.");
							continue;
						}

						// Delete the existing entry so we can replace it with the new one.
						apk.DeleteEntry (entryName);
					}

					var ms = new MemoryStream ();
					entry.Extract (ms);
					ms.Position = 0;
					Log.LogDebugMessage ($"Refreshing {entryName} from {ApkInputPath}");
					apk.AddEntry (ms, entryName, entry.CompressionMethod.ToCompressionLevel ());
				}
			}
		}

		apk.FixupWindowsPathSeparators (Log);

		// Add the files to the apk
		foreach (var file in FilesToAddToArchive) {
			var disk_path = file.ItemSpec;
			var apk_path = file.GetRequiredMetadata ("FilesToAddToArchive", "ArchivePath", Log);

			// An error will already be logged
			if (apk_path is null) {
				return !Log.HasLoggedErrors;
			}

			apk_path = apk_path.Replace ('\\', '/');

			// This is a temporary hack for adding files directly from inside a .jar/.aar
			// into the APK. Eventually another task should be writing them to disk and just
			// passing us a filename like everything else.
			var jar_entry_name = file.GetMetadataOrDefault ("JavaArchiveEntry", string.Empty);

			if (jar_entry_name.HasValue ()) {
				// ItemSpec for these will be "<jarfile>#<entrypath>
				// eg: "obj/myjar.jar#myfile.txt"
				var jar_file_path = disk_path.Substring (0, disk_path.Length - (jar_entry_name.Length + 1));

				if (apk.ContainsEntry (apk_path)) {
					Log.LogDebugMessage ("Failed to add jar entry {0} from {1}: the same file already exists in the apk", jar_entry_name, Path.GetFileName (jar_file_path));
					continue;
				}

				using (var stream = File.OpenRead (jar_file_path))
				using (var jar = ZipArchive.Open (stream)) {
					var jar_item = jar.ReadEntry (jar_entry_name);

					byte [] data;
					var d = MemoryStreamPool.Shared.Rent ();

					try {
						jar_item.Extract (d);
						data = d.ToArray ();
					} finally {
						MemoryStreamPool.Shared.Return (d);
					}

					Log.LogDebugMessage ($"Adding {jar_entry_name} from {jar_file_path} as the archive file is out of date.");
					apk.AddEntry (data, apk_path);
				}

				continue;
			}

			AddFileToArchiveIfNewer (apk, disk_path, apk_path, file, existingEntries);
		}

		// Clean up Removed files.
		foreach (var entry in existingEntries) {
			// Never remove an AndroidManifest. It may be renamed when using aab.
			if (string.Compare (Path.GetFileName (entry), "AndroidManifest.xml", StringComparison.OrdinalIgnoreCase) == 0)
				continue;

			Log.LogDebugMessage ($"Removing {entry} as it is not longer required.");
			apk.DeleteEntry (entry);
		}

		if (is_aab)
			FixupArchive (apk);

		return !Log.HasLoggedErrors;
	}

	bool AddFileToArchiveIfNewer (IZipArchive apk, string file, string inArchivePath, ITaskItem item, List<string> existingEntries)
	{
		var compressionMethod = GetCompressionLevel (item);
		existingEntries.Remove (inArchivePath.Replace (Path.DirectorySeparatorChar, '/'));

		return apk.AddFileIfChanged (Log, file, inArchivePath, compressionMethod);
	}

	/// <summary>
	/// aapt2 is putting AndroidManifest.xml in the root of the archive instead of at manifest/AndroidManifest.xml that bundletool expects.
	/// I see no way to change this behavior, so we can move the file for now:
	/// https://github.com/aosp-mirror/platform_frameworks_base/blob/e80b45506501815061b079dcb10bf87443bd385d/tools/aapt2/LoadedApk.h#L34
	/// </summary>
	void FixupArchive (IZipArchive zip)
	{
		if (!zip.ContainsEntry ("AndroidManifest.xml")) {
			Log.LogDebugMessage ($"No AndroidManifest.xml. Skipping Fixup");
			return;
		}

		Log.LogDebugMessage ($"Fixing up AndroidManifest.xml to be manifest/AndroidManifest.xml.");

		if (zip.ContainsEntry ("manifest/AndroidManifest.xml"))
			zip.DeleteEntry ("manifest/AndroidManifest.xml");

		zip.MoveEntry ("AndroidManifest.xml", "manifest/AndroidManifest.xml");
	}

	System.IO.Compression.CompressionLevel GetCompressionLevel (ITaskItem item)
	{
		return (UncompressedFileExtensionsSet.Contains (Path.GetExtension (item.ItemSpec)) ? uncompressedMethod : CompressionMethod.Default).ToCompressionLevel ();
	}

	HashSet<string> ParseUncompressedFileExtensions ()
	{
		var uncompressedFileExtensions = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		foreach (var extension in UncompressedFileExtensions?.Split ([';', ','], StringSplitOptions.RemoveEmptyEntries) ?? []) {
			var ext = extension.Trim ();

			if (string.IsNullOrEmpty (ext)) {
				continue;
			}

			if (ext [0] != '.') {
				ext = $".{ext}";
			}

			uncompressedFileExtensions.Add (ext);
		}

		return uncompressedFileExtensions;
	}
}
