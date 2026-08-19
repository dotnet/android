#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Android.Tasks;

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

	HashSet<string>? uncompressedFileExtensions;

	HashSet<string> UncompressedFileExtensionsSet => uncompressedFileExtensions ??= ParseUncompressedFileExtensions ();

	CompressionLevel uncompressedFileCompression = CompressionLevel.NoCompression;

	public override bool RunTask ()
	{
		bool isAab = string.Equals (AndroidPackageFormat, "aab", StringComparison.OrdinalIgnoreCase);
		if (isAab) {
			uncompressedFileCompression = CompressionLevel.Optimal;
		}

		Directory.CreateDirectory (Path.GetDirectoryName (ApkOutputPath) ?? ".");

		bool refreshExistingOutput = true;
		if (!string.IsNullOrEmpty (ApkInputPath) && File.Exists (ApkInputPath) && !File.Exists (ApkOutputPath)) {
			Log.LogDebugMessage ($"Copying {ApkInputPath} to {ApkOutputPath}");
			File.Copy (ApkInputPath, ApkOutputPath, overwrite: true);
			refreshExistingOutput = false;
		}

		using var apk = ZipArchiveExtensions.OpenZip (ApkOutputPath, FileMode.OpenOrCreate);
		var existingEntries = new List<string> ();

		if (refreshExistingOutput) {
			foreach (var entry in apk.Entries) {
				Log.LogDebugMessage ($"Registering item {entry.FullName}");
				existingEntries.Add (entry.FullName);
			}
		}

		if (!string.IsNullOrEmpty (ApkInputPath) && File.Exists (ApkInputPath) && refreshExistingOutput) {
			RefreshEntriesFromInputArchive (apk, existingEntries, isAab);
		}

		apk.FixupWindowsPathSeparators (
			entry => ToCompressionLevel (entry.CompressionMethod),
			(source, destination) => {
				Log.LogDebugMessage ($"Fixing up malformed entry `{source}` -> `{destination}`");
				existingEntries.Remove (source);
				existingEntries.Add (destination);
			}
		);

		foreach (var file in FilesToAddToArchive) {
			if (!AddItemToArchive (apk, file, existingEntries))
				return false;
		}

		foreach (var entry in existingEntries) {
			if (string.Equals (Path.GetFileName (entry), "AndroidManifest.xml", StringComparison.OrdinalIgnoreCase))
				continue;

			Log.LogDebugMessage ($"Removing {entry} as it is no longer required.");
			apk.ReadEntry (entry, StringComparison.Ordinal)?.Delete ();
		}

		if (isAab) {
			FixupBundleManifest (apk);
		}

		return !Log.HasLoggedErrors;
	}

	void RefreshEntriesFromInputArchive (ZipArchive apk, List<string> existingEntries, bool isAab)
	{
		if (ApkInputPath == null)
			throw new InvalidOperationException ("ApkInputPath must not be null when refreshing the output archive.");

		DateTime lastWriteOutput = File.Exists (ApkOutputPath) ? File.GetLastWriteTimeUtc (ApkOutputPath) : DateTime.MinValue;
		DateTime lastWriteInput = File.GetLastWriteTimeUtc (ApkInputPath);
		var inputMetadata = ZipArchiveMetadataReader.Read (ApkInputPath);

		using var packaged = ZipArchiveExtensions.OpenZip (ApkInputPath, FileMode.Open);
		foreach (var entry in packaged.Entries) {
			if (entry.IsDirectory ()) {
				continue;
			}

			string entryName = entry.FullName;
			if (entryName.Contains ("\\")) {
				entryName = entryName.Replace ('\\', '/');
				Log.LogDebugMessage ($"Fixing up malformed entry `{entry.FullName}` -> `{entryName}`");
			}

			if (entryName == "AndroidManifest.xml" && isAab) {
				Log.LogDebugMessage ("Renaming AndroidManifest.xml to manifest/AndroidManifest.xml");
				entryName = "manifest/AndroidManifest.xml";
			}

			Log.LogDebugMessage ($"Deregistering item {entryName}");
			existingEntries.Remove (entryName);

			if (lastWriteInput <= lastWriteOutput) {
				Log.LogDebugMessage ($"Skipping to next item. {lastWriteInput} <= {lastWriteOutput}.");
				continue;
			}

			if (!inputMetadata.TryGetValue (entry.FullName, out ZipEntryMetadata metadata)) {
				throw new InvalidDataException ($"Unable to read ZIP metadata for '{entry.FullName}' in '{ApkInputPath}'.");
			}

			var currentEntry = apk.ReadEntry (entryName, StringComparison.Ordinal);
			if (currentEntry != null && metadata.Crc32 == GetEntryCrc32 (currentEntry) && metadata.CompressedSize == currentEntry.CompressedLength) {
				Log.LogDebugMessage ($"Skipping {entryName} from {ApkInputPath} as its up to date.");
				continue;
			}

			if (currentEntry != null) {
				currentEntry.Delete ();
			}

			Log.LogDebugMessage ($"Refreshing {entryName} from {ApkInputPath}");
			CopyEntryToArchive (apk, entryName, entry, metadata.CompressionMethod.ToCompressionLevel ());
		}
	}

	bool AddItemToArchive (ZipArchive apk, ITaskItem item, List<string> existingEntries)
	{
		string diskPath = item.ItemSpec;
		string? archivePath = GetRequiredMetadata (item, "FilesToAddToArchive", "ArchivePath");
		if (archivePath == null)
			return false;

		archivePath = archivePath.Replace ('\\', '/');

		string jarEntryName = GetMetadataOrDefault (item, "JavaArchiveEntry", string.Empty);
		if (!string.IsNullOrEmpty (jarEntryName)) {
			AddJarEntryToArchive (apk, diskPath, archivePath, jarEntryName, existingEntries);
			return !Log.HasLoggedErrors;
		}

		AddFileToArchiveIfNewer (apk, diskPath, archivePath, item, existingEntries);
		return !Log.HasLoggedErrors;
	}

	void AddJarEntryToArchive (ZipArchive apk, string diskPath, string archivePath, string jarEntryName, List<string> existingEntries)
	{
		string jarFilePath = diskPath.Substring (0, diskPath.Length - (jarEntryName.Length + 1));
		bool wasExistingOutputEntry = existingEntries.Remove (archivePath);
		var currentEntry = apk.ReadEntry (archivePath, StringComparison.Ordinal);

		if (currentEntry != null && !wasExistingOutputEntry) {
			Log.LogDebugMessage ("Failed to add jar entry {0} from {1}: the same file already exists in the apk", jarEntryName, Path.GetFileName (jarFilePath));
			return;
		}

		using var jar = ZipArchiveExtensions.OpenZip (jarFilePath, FileMode.Open);
		var jarEntry = jar.ReadEntry (jarEntryName, StringComparison.Ordinal);
		if (jarEntry == null) {
			Log.LogDebugMessage ("Failed to add jar entry {0} from {1}: entry not found in jar.", jarEntryName, jarFilePath);
			if (wasExistingOutputEntry)
				existingEntries.Add (archivePath);
			return;
		}

		if (currentEntry != null && GetEntryCrc32 (currentEntry) == GetEntryCrc32 (jarEntry)) {
			Log.LogDebugMessage ("Skipping {0} from {1} as it is up to date.", jarEntryName, jarFilePath);
			return;
		}

		currentEntry?.Delete ();

		using var buffer = MemoryStreamPool.Shared.Rent ();
		jarEntry.Extract (buffer);
		buffer.Position = 0;
		Log.LogDebugMessage ($"Adding {jarEntryName} from {jarFilePath} as the archive file is out of date.");
		apk.AddStream (buffer, archivePath);
	}

	bool AddFileToArchiveIfNewer (ZipArchive apk, string file, string archivePath, ITaskItem item, List<string> existingEntries)
	{
		ZipCompressionMethod compressionMethod = GetCompressionMethod (item);
		existingEntries.Remove (archivePath);

		var entry = apk.ReadEntry (archivePath, StringComparison.Ordinal);
		if (entry == null) {
			apk.AddFile (file, archivePath, ToCompressionLevel (compressionMethod));
			Log.LogDebugMessage ($"Adding {file} as it doesn't already exist.");
			return true;
		}

		if (GetExistingCompressionMethod (entry) != compressionMethod) {
			Log.LogDebugMessage ($"Updating {file} as the compression level changed.");
			entry.Delete ();
			apk.AddFile (file, archivePath, ToCompressionLevel (compressionMethod));
			return true;
		}

		uint existingDosTime = DateTimeToDosTime (entry.LastWriteTime.UtcDateTime);
		uint fileDosTime = DateTimeToDosTime (File.GetLastWriteTimeUtc (file));
		if (existingDosTime < fileDosTime) {
			Log.LogDebugMessage ($"Updating {file} as the file write time is newer: file in zip - '{existingDosTime}', file on disk - '{fileDosTime}'.");
			entry.Delete ();
			apk.AddFile (file, archivePath, ToCompressionLevel (compressionMethod));
			return true;
		}

		Log.LogDebugMessage ($"Skipping {file} as the archive file is up to date.");
		return false;
	}

	void FixupBundleManifest (ZipArchive apk)
	{
		var manifest = apk.ReadEntry ("AndroidManifest.xml", StringComparison.Ordinal);
		if (manifest == null) {
			Log.LogDebugMessage ("No AndroidManifest.xml. Skipping Fixup");
			return;
		}

		Log.LogDebugMessage ("Fixing up AndroidManifest.xml to be manifest/AndroidManifest.xml.");
		apk.MoveEntry ("AndroidManifest.xml", "manifest/AndroidManifest.xml", ToCompressionLevel (manifest.CompressionMethod));
	}

	void CopyEntryToArchive (ZipArchive archive, string destinationEntryName, ZipArchiveEntry sourceEntry, CompressionLevel compressionLevel)
	{
		var destinationEntry = archive.CreateEntry (destinationEntryName, compressionLevel);
		destinationEntry.LastWriteTime = sourceEntry.LastWriteTime;
		using var source = sourceEntry.Open ();
		using var destination = destinationEntry.Open ();
		source.CopyTo (destination);
	}

	ZipCompressionMethod GetCompressionMethod (ITaskItem item)
	{
		if (UncompressedFileExtensionsSet.Contains (Path.GetExtension (item.ItemSpec))) {
			return uncompressedFileCompression == CompressionLevel.NoCompression ? ZipCompressionMethod.Stored : ZipCompressionMethod.Deflate;
		}

		return ZipCompressionMethod.Deflate;
	}

	static CompressionLevel ToCompressionLevel (ZipCompressionMethod compressionMethod)
	{
		return compressionMethod switch {
			ZipCompressionMethod.Stored => CompressionLevel.NoCompression,
			ZipCompressionMethod.Deflate => CompressionLevel.Optimal,
			_ => throw new NotSupportedException ($"Unsupported ZIP compression method: {compressionMethod}"),
		};
	}

	static ZipCompressionMethod GetExistingCompressionMethod (ZipArchiveEntry entry)
	{
		return entry.CompressionMethod switch {
			ZipCompressionMethod.Stored => ZipCompressionMethod.Stored,
			ZipCompressionMethod.Deflate => ZipCompressionMethod.Deflate,
			_ => throw new NotSupportedException ($"Unsupported ZIP compression method: {entry.CompressionMethod}"),
		};
	}

	static uint GetEntryCrc32 (ZipArchiveEntry entry)
	{
		using var buffer = MemoryStreamPool.Shared.Rent ();
		entry.Extract (buffer);
		if (buffer.TryGetBuffer (out ArraySegment<byte> segment) && segment.Array != null) {
			return Crc32.HashToUInt32 (new ReadOnlySpan<byte> (segment.Array, segment.Offset, (int) buffer.Length));
		}

		return Crc32.HashToUInt32 (buffer.ToArray ());
	}

	HashSet<string> ParseUncompressedFileExtensions ()
	{
		var parsedExtensions = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		foreach (var extension in UncompressedFileExtensions?.Split ([';', ','], StringSplitOptions.RemoveEmptyEntries) ?? []) {
			var normalized = extension.Trim ();
			if (string.IsNullOrEmpty (normalized)) {
				continue;
			}

			if (normalized [0] != '.') {
				normalized = $".{normalized}";
			}

			parsedExtensions.Add (normalized);
		}

		return parsedExtensions;
	}

	string? GetRequiredMetadata (ITaskItem item, string itemName, string metadataName)
	{
		string metadataValue = item.GetMetadata (metadataName) ?? "";
		if (!string.IsNullOrWhiteSpace (metadataValue))
			return metadataValue;

		Log.LogError ($"The '{metadataName}' metadata on '{itemName}' is required for '{item.ItemSpec}'.");
		return null;
	}

	static string GetMetadataOrDefault (ITaskItem item, string metadataName, string defaultValue)
	{
		string metadataValue = item.GetMetadata (metadataName) ?? "";
		if (string.IsNullOrEmpty (metadataValue))
			return defaultValue;

		return metadataValue;
	}

	const int ValidZipDate_YearMin = 1980;

	static uint DateTimeToDosTime (DateTime dateTime)
	{
		int ret = ((dateTime.Year - ValidZipDate_YearMin) & 0x7F);
		ret = (ret << 4) + dateTime.Month;
		ret = (ret << 5) + dateTime.Day;
		ret = (ret << 5) + dateTime.Hour;
		ret = (ret << 6) + dateTime.Minute;
		ret = (ret << 5) + (dateTime.Second / 2);
		return (uint) ret;
	}
}
