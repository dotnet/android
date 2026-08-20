#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

	public string? ArchiveRootDirectory { get; set; }

	public string? UncompressedFileExtensions { get; set; }

	public string? ZipFlushFilesLimit { get; set; }

	public string? ZipFlushSizeLimit { get; set; }

	HashSet<string>? uncompressedFileExtensions;

	HashSet<string> UncompressedFileExtensionsSet => uncompressedFileExtensions ??= ParseUncompressedFileExtensions ();

	CompressionLevel uncompressedFileCompression = CompressionLevel.NoCompression;

	const int DefaultFlushFilesLimit = 512;
	const long DefaultFlushSizeLimit = 100 * 1024 * 1024;

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

		using var apk = new ArchiveUpdateSession (
			ApkOutputPath,
			ParseFlushLimit (ZipFlushFilesLimit, DefaultFlushFilesLimit),
			ParseFlushLimit (ZipFlushSizeLimit, DefaultFlushSizeLimit)
		);
		var existingEntries = new List<string> ();

		if (refreshExistingOutput) {
			foreach (var entry in apk.Archive.Entries) {
				Log.LogDebugMessage ($"Registering item {entry.FullName}");
				existingEntries.Add (entry.FullName);
			}
		}

		if (!string.IsNullOrEmpty (ApkInputPath) && File.Exists (ApkInputPath) && refreshExistingOutput) {
			RefreshEntriesFromInputArchive (apk, existingEntries, isAab);
		}

		bool fixedPathSeparators = false;
		apk.Archive.FixupWindowsPathSeparators (
			entry => ToCompressionLevel (entry.CompressionMethod),
			(source, destination) => {
				fixedPathSeparators = true;
				Log.LogDebugMessage ($"Fixing up malformed entry `{source}` -> `{destination}`");
				existingEntries.Remove (source);
				existingEntries.Add (destination);
			}
		);
		if (fixedPathSeparators)
			apk.Commit ();

		foreach (var file in FilesToAddToArchive) {
			if (!AddItemToArchive (apk, file, existingEntries))
				return false;
		}

		foreach (var entry in existingEntries) {
			if (string.Equals (Path.GetFileName (entry), "AndroidManifest.xml", StringComparison.OrdinalIgnoreCase))
				continue;

			Log.LogDebugMessage ($"Removing {entry} as it is no longer required.");
			apk.Archive.ReadEntry (entry, StringComparison.Ordinal)?.Delete ();
		}

		if (isAab) {
			FixupBundleManifest (apk.Archive);
		}

		return !Log.HasLoggedErrors;
	}

	void RefreshEntriesFromInputArchive (ArchiveUpdateSession apk, List<string> existingEntries, bool isAab)
	{
		if (ApkInputPath == null)
			throw new InvalidOperationException ("ApkInputPath must not be null when refreshing the output archive.");

		DateTime lastWriteOutput = File.Exists (ApkOutputPath) ? File.GetLastWriteTimeUtc (ApkOutputPath) : DateTime.MinValue;
		DateTime lastWriteInput = File.GetLastWriteTimeUtc (ApkInputPath);

		using var packaged = ZipArchiveExtensions.OpenZipRead (ApkInputPath);
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

			var currentEntry = apk.Archive.ReadEntry (entryName, StringComparison.Ordinal);
			if (currentEntry != null && entry.Crc32 == currentEntry.Crc32 && entry.CompressedLength == currentEntry.CompressedLength) {
				Log.LogDebugMessage ($"Skipping {entryName} from {ApkInputPath} as its up to date.");
				continue;
			}

			if (currentEntry != null) {
				currentEntry.Delete ();
			}

			Log.LogDebugMessage ($"Refreshing {entryName} from {ApkInputPath}");
			CopyEntryToArchive (apk.Archive, entryName, entry, ToCompressionLevel (entry.CompressionMethod));
			apk.RecordWrite (entry.Length);
		}
	}

	bool AddItemToArchive (ArchiveUpdateSession apk, ITaskItem item, List<string> existingEntries)
	{
		string diskPath = item.ItemSpec;
		string archivePath = item.GetMetadata ("ArchivePath") ?? "";
		if (string.IsNullOrWhiteSpace (archivePath)) {
			if (!string.IsNullOrEmpty (ArchiveRootDirectory)) {
				archivePath = Path.GetRelativePath (ArchiveRootDirectory, diskPath);
			} else if (!item.TryGetRequiredMetadata ("FilesToAddToArchive", "ArchivePath", Log, out archivePath)) {
				return false;
			}
		}

		archivePath = archivePath.Replace ('\\', '/');

		string jarEntryName = GetMetadataOrDefault (item, "JavaArchiveEntry", string.Empty);
		if (!string.IsNullOrEmpty (jarEntryName)) {
			AddJarEntryToArchive (apk, diskPath, archivePath, jarEntryName, existingEntries);
			return !Log.HasLoggedErrors;
		}

		AddFileToArchiveIfNewer (apk, diskPath, archivePath, item, existingEntries);
		return !Log.HasLoggedErrors;
	}

	void AddJarEntryToArchive (ArchiveUpdateSession apk, string diskPath, string archivePath, string jarEntryName, List<string> existingEntries)
	{
		string jarFilePath = diskPath.Substring (0, diskPath.Length - (jarEntryName.Length + 1));
		bool wasExistingOutputEntry = existingEntries.Remove (archivePath);
		var currentEntry = apk.Archive.ReadEntry (archivePath, StringComparison.Ordinal);

		if (currentEntry != null && !wasExistingOutputEntry) {
			Log.LogDebugMessage ("Failed to add jar entry {0} from {1}: the same file already exists in the apk", jarEntryName, Path.GetFileName (jarFilePath));
			return;
		}

		using var jar = ZipArchiveExtensions.OpenZipRead (jarFilePath);
		var jarEntry = jar.ReadEntry (jarEntryName, StringComparison.Ordinal);
		if (jarEntry == null) {
			Log.LogDebugMessage ("Failed to add jar entry {0} from {1}: entry not found in jar.", jarEntryName, jarFilePath);
			if (wasExistingOutputEntry)
				existingEntries.Add (archivePath);
			return;
		}

		if (currentEntry != null && currentEntry.Crc32 == jarEntry.Crc32) {
			Log.LogDebugMessage ("Skipping {0} from {1} as it is up to date.", jarEntryName, jarFilePath);
			return;
		}

		currentEntry?.Delete ();

		using var buffer = MemoryStreamPool.Shared.Rent ();
		jarEntry.Extract (buffer);
		buffer.Position = 0;
		Log.LogDebugMessage ($"Adding {jarEntryName} from {jarFilePath} as the archive file is out of date.");
		apk.Archive.AddStream (buffer, archivePath);
		apk.RecordWrite (jarEntry.Length);
	}

	bool AddFileToArchiveIfNewer (ArchiveUpdateSession apk, string file, string archivePath, ITaskItem item, List<string> existingEntries)
	{
		ZipCompressionMethod compressionMethod = GetCompressionMethod (item);
		existingEntries.Remove (archivePath);

		var entry = apk.Archive.ReadEntry (archivePath, StringComparison.Ordinal);
		if (entry == null) {
			apk.Archive.AddFile (file, archivePath, ToCompressionLevel (compressionMethod));
			apk.RecordWrite (new FileInfo (file).Length);
			Log.LogDebugMessage ($"Adding {file} as it doesn't already exist.");
			return true;
		}

		if (GetExistingCompressionMethod (entry) != compressionMethod) {
			Log.LogDebugMessage ($"Updating {file} as the compression level changed.");
			entry.Delete ();
			apk.Archive.AddFile (file, archivePath, ToCompressionLevel (compressionMethod));
			apk.RecordWrite (new FileInfo (file).Length);
			return true;
		}

		uint existingDosTime = DateTimeToDosTime (entry.LastWriteTime.UtcDateTime);
		uint fileDosTime = DateTimeToDosTime (File.GetLastWriteTimeUtc (file));
		if (existingDosTime < fileDosTime) {
			Log.LogDebugMessage ($"Updating {file} as the file write time is newer: file in zip - '{existingDosTime}', file on disk - '{fileDosTime}'.");
			entry.Delete ();
			apk.Archive.AddFile (file, archivePath, ToCompressionLevel (compressionMethod));
			apk.RecordWrite (new FileInfo (file).Length);
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

	static string GetMetadataOrDefault (ITaskItem item, string metadataName, string defaultValue)
	{
		string metadataValue = item.GetMetadata (metadataName) ?? "";
		if (string.IsNullOrEmpty (metadataValue))
			return defaultValue;

		return metadataValue;
	}

	static long ParseFlushLimit (string? value, long defaultValue)
	{
		if (long.TryParse (value, out long parsedValue) && parsedValue > 0)
			return parsedValue;

		return defaultValue;
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

	sealed class ArchiveUpdateSession : IDisposable
	{
		readonly string archivePath;
		readonly long flushFilesLimit;
		readonly long flushSizeLimit;
		long filesWritten;
		long bytesWritten;
		ZipArchive archive;

		public ZipArchive Archive => archive;

		public ArchiveUpdateSession (string archivePath, long flushFilesLimit, long flushSizeLimit)
		{
			this.archivePath = archivePath;
			this.flushFilesLimit = flushFilesLimit;
			this.flushSizeLimit = flushSizeLimit;
			archive = ZipArchiveExtensions.OpenZipUpdate (archivePath);
		}

		public void RecordWrite (long size)
		{
			filesWritten++;
			bytesWritten += size;
			if (filesWritten >= flushFilesLimit || bytesWritten >= flushSizeLimit)
				Commit ();
		}

		public void Commit ()
		{
			archive.Dispose ();
			archive = ZipArchiveExtensions.OpenZipUpdate (archivePath, FileMode.Open);
			filesWritten = 0;
			bytesWritten = 0;
		}

		public void Dispose ()
		{
			archive.Dispose ();
		}
	}
}
