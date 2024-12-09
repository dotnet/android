#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Tools.Zip;

// TODO: Need to handle BuildBaseAppBundle.FixupArchive somewhere.

namespace Xamarin.Android.Tasks;

/// <summary>
/// Takes a list of files and adds them to an APK archive. If the APK archive already
/// exists, files are only added if they were changed.
/// </summary>
public class BuildApkArchive : AndroidTask
{
	public override string TaskPrefix => "BAA";

	[Required]
	public string Abi { get;set; } = null!; // NRT enforced by [Required]

	public string? ApkInputPath { get; set; }

	[Required]
	public ITaskItem [] ApkOutputPaths { get; set; } = null!; // NRT enforced by [Required]

	[Required]
	public ITaskItem [] FilesToAddToApk { get; set; } = null!; // NRT enforced by [Required]

	public string? UncompressedFileExtensions { get; set; }

	public string? ZipFlushFilesLimit { get; set; }

	public string? ZipFlushSizeLimit { get; set; }

	readonly HashSet<string> uncompressedFileExtensions;

	// TODO: Make a property?
	protected virtual CompressionMethod UncompressedMethod => CompressionMethod.Store;

	public BuildApkArchive ()
	{
		uncompressedFileExtensions = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

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
	}

	public override bool RunTask ()
	{
		var refresh = true;

		// Find the output apk filename
		var apk_output_path = ApkOutputPaths.Single (i => i.GetMetadataOrDefault ("Abi", string.Empty) == Abi).ItemSpec;

		// If we have an input apk but no output apk, copy it to the output
		// so we don't modify the original.
		if (ApkInputPath is not null && File.Exists (ApkInputPath) && !File.Exists (apk_output_path)) {
			Log.LogDebugMessage ($"Copying {ApkInputPath} to {apk_output_path}");
			File.Copy (ApkInputPath, apk_output_path, overwrite: true);
			refresh = false;
		}

		using var apk = new ZipArchiveEx (apk_output_path, FileMode.Open);

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
			for (var i = 0; i < apk.Archive.EntryCount; i++) {
				var entry = apk.Archive.ReadEntry ((ulong) i);
				Log.LogDebugMessage ($"Registering item {entry.FullName}");
				existingEntries.Add (entry.FullName);
			}
		}

		// If we're modifying an existing APK we need to update any out
		// of date entries in the output APK from the input APK.
		if (ApkInputPath is not null && File.Exists (ApkInputPath) && refresh) {

			var lastWriteOutput = File.Exists (apk_output_path) ? File.GetLastWriteTimeUtc (apk_output_path) : DateTime.MinValue;
			var lastWriteInput = File.GetLastWriteTimeUtc (ApkInputPath);

			using (var packaged = new ZipArchiveEx (ApkInputPath, FileMode.Open)) {
				foreach (var entry in packaged.Archive) {

					// NOTE: aapt2 is creating zip entries on Windows such as `assets\subfolder/asset2.txt`
					var entryName = entry.FullName;

					if (entryName.Contains ("\\")) {
						entryName = entryName.Replace ('\\', '/');
						Log.LogDebugMessage ($"Fixing up malformed entry `{entry.FullName}` -> `{entryName}`");
					}

					Log.LogDebugMessage ($"Deregistering item {entryName}");
					existingEntries.Remove (entryName);

					if (lastWriteInput <= lastWriteOutput) {
						Log.LogDebugMessage ($"Skipping to next item. {lastWriteInput} <= {lastWriteOutput}.");
						continue;
					}

					if (apk.Archive.ContainsEntry (entryName)) {
						ZipEntry e = apk.Archive.ReadEntry (entryName);
						// check the CRC values as the ModifiedDate is always 01/01/1980 in the aapt generated file.
						if (entry.CRC == e.CRC && entry.CompressedSize == e.CompressedSize) {
							Log.LogDebugMessage ($"Skipping {entryName} from {ApkInputPath} as its up to date.");
							continue;
						}
					}

					var ms = new MemoryStream ();
					entry.Extract (ms);
					Log.LogDebugMessage ($"Refreshing {entryName} from {ApkInputPath}");
					apk.Archive.AddStream (ms, entryName, compressionMethod: entry.CompressionMethod);
				}
			}
		}

		apk.FixupWindowsPathSeparators ((a, b) => Log.LogDebugMessage ($"Fixing up malformed entry `{a}` -> `{b}`"));

		// Add the files to the apk
		foreach (var file in FilesToAddToApk) {
			var disk_path = file.ItemSpec;
			var apk_path = file.GetRequiredMetadata ("FilesToAddToApk", "ApkPath", Log);

			// An error will already be logged
			if (apk_path is null) {
				return !Log.HasLoggedErrors;
			}

			// This is a temporary hack for adding files directly from inside a .jar/.aar
			// into the APK. Eventually another task should be writing them to disk and just
			// passing us a filename like everything else.
			var jar_entry_name = file.GetMetadataOrDefault ("JavaArchiveEntry", string.Empty);

			if (jar_entry_name.HasValue ()) {
				// ItemSpec for these will be "<jarfile>#<entrypath>
				// eg: "obj/myjar.jar#myfile.txt"
				var jar_file_path = disk_path.Substring (0, disk_path.Length - (jar_entry_name.Length + 1));

				if (apk.Archive.Any (ze => ze.FullName == apk_path)) {
					Log.LogDebugMessage ("Failed to add jar entry {0} from {1}: the same file already exists in the apk", jar_entry_name, Path.GetFileName (jar_file_path));
					continue;
				}

				using (var stream = File.OpenRead (jar_file_path))
				using (var jar = ZipArchive.Open (stream)) {
					var jar_item = jar.ReadEntry (jar_entry_name);

					byte [] data;

					using (var d = new MemoryStream ()) {
						jar_item.Extract (d);
						data = d.ToArray ();
					}

					Log.LogDebugMessage ($"Adding {jar_entry_name} from {jar_file_path} as the archive file is out of date.");
					apk.AddEntryAndFlush (data, apk_path);
				}

				continue;
			}

			AddFileToArchiveIfNewer (apk, disk_path, apk_path, existingEntries);
		}

		// Clean up Removed files.
		foreach (var entry in existingEntries) {
			// Never remove an AndroidManifest. It may be renamed when using aab.
			if (string.Compare (Path.GetFileName (entry), "AndroidManifest.xml", StringComparison.OrdinalIgnoreCase) == 0)
				continue;

			Log.LogDebugMessage ($"Removing {entry} as it is not longer required.");
			apk.Archive.DeleteEntry (entry);
		}

		return !Log.HasLoggedErrors;
	}

	bool AddFileToArchiveIfNewer (ZipArchiveEx apk, string file, string inArchivePath, List<string> existingEntries)
	{
		var compressionMethod = GetCompressionMethod (file);
		existingEntries.Remove (inArchivePath.Replace (Path.DirectorySeparatorChar, '/'));

		if (apk.SkipExistingFile (file, inArchivePath, compressionMethod)) {
			Log.LogDebugMessage ($"Skipping {file} as the archive file is up to date.");
			return false;
		}

		Log.LogDebugMessage ($"Adding {file} as the archive file is out of date.");
		apk.AddFileAndFlush (file, inArchivePath, compressionMethod);

		return true;
	}

	CompressionMethod GetCompressionMethod (string fileName)
	{
		return uncompressedFileExtensions.Contains (Path.GetExtension (fileName)) ? UncompressedMethod : CompressionMethod.Default;
	}
}
