#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Microsoft.Android.Tasks;

sealed class ZipArchiveEx : IDisposable
{
	const long BufferedSizeLimit = 100 * 1024 * 1024;
	const int BufferedFilesLimit = 512;

	readonly string archivePath;
	long bufferedSize;
	int bufferedFiles;
	bool modified;

	public ZipArchive Archive { get; private set; }

	public ZipArchiveEx (string archive, FileMode mode)
	{
		archivePath = Path.GetFullPath (archive);
		var stream = new FileStream (archivePath, mode, FileAccess.ReadWrite);
		var ownsStream = false;
		try {
			Archive = new ZipArchive (stream, ZipArchiveMode.Update);
			ownsStream = true;
		} finally {
			if (!ownsStream)
				stream.Dispose ();
		}
	}

	public void AddDirectory (string folder, string folderInArchive, CompressionLevel method = CompressionLevel.Optimal)
	{
		foreach (var (filename, relativePath) in Files.EnumerateArchiveFiles (folder)) {
			var archivePath = Path.Combine (folderInArchive, relativePath).Replace ('\\', '/').TrimStart ('/');
			if (SkipExistingFile (filename, archivePath, method))
				continue;
			AddFile (filename, archivePath, method);
		}
	}

	void PrepareWrite (long? size)
	{
		if (bufferedFiles > 0 && (size is null || size.Value > BufferedSizeLimit - bufferedSize))
			Flush ();
	}

	void RecordWrite (long size)
	{
		modified = true;
		bufferedSize += size;
		bufferedFiles++;
		if (bufferedSize >= BufferedSizeLimit || bufferedFiles >= BufferedFilesLimit)
			Flush ();
	}

	void Flush ()
	{
		if (!modified)
			return;

		// Update mode retains changed uncompressed payloads until the archive is disposed.
		Archive.Dispose ();
		Archive = ZipFile.Open (archivePath, ZipArchiveMode.Update);
		bufferedSize = 0;
		bufferedFiles = 0;
		modified = false;
	}

	void AddFile (string filename, string archivePath, CompressionLevel compression)
	{
		var size = new FileInfo (filename).Length;
		PrepareWrite (size);
		DeleteEntry (archivePath);
		Archive.CreateEntryFromFile (filename, archivePath, compression);
		RecordWrite (size);
	}

	public bool SkipExistingFile (string filename, string archivePath, CompressionLevel compression)
	{
		var entry = Archive.GetEntry (archivePath);
		var method = compression == CompressionLevel.NoCompression ? ZipCompressionMethod.Stored : ZipCompressionMethod.Deflate;
		if (entry is null || entry.CompressionMethod != method)
			return false;

		var file = new FileInfo (filename);
		if (!TryGetEntryLength (entry, out var length) || length != file.Length)
			return false;

		var fileTime = ToDosTime (file.LastWriteTimeUtc);
		var entryTime = ToDosTime (entry.LastWriteTime.UtcDateTime);
		if (fileTime > entryTime)
			return false;
		if (fileTime < entryTime)
			return true;

		// CRC also distinguishes edits within ZIP's two-second timestamp granularity.
		using var stream = file.OpenRead ();
		var crc = new System.IO.Hashing.Crc32 ();
		crc.Append (stream);
		return crc.GetCurrentHashAsUInt32 () == entry.Crc32;
	}

	internal static bool TryGetEntryLength (ZipArchiveEntry entry, out long length)
	{
		try {
			length = entry.Length;
			return true;
		} catch (InvalidOperationException) {
			// Entries already written during this update no longer expose their stored length.
			length = 0;
			return false;
		}
	}

	public bool AddFileIfChanged (TaskLoggingHelper log, string filename, string archivePath, CompressionLevel compression)
	{
		if (SkipExistingFile (filename, archivePath, compression)) {
			log.LogDebugMessage ($"Skipping {filename} as the archive file is up to date.");
			return false;
		}

		AddFile (filename, archivePath, compression);
		log.LogDebugMessage ($"Adding {filename} as the archive file is out of date.");
		return true;
	}

	public void AddEntry (Stream stream, string archivePath, CompressionLevel compression, long? uncompressedSize = null)
	{
		if (uncompressedSize < 0)
			throw new ArgumentOutOfRangeException (nameof (uncompressedSize));
		var size = uncompressedSize ?? (stream.CanSeek ? Math.Max (0, stream.Length - stream.Position) : (long?) null);
		PrepareWrite (size);
		DeleteEntry (archivePath);
		var entry = Archive.CreateEntry (archivePath, compression);
		long written;
		using (var destination = entry.Open ()) {
			stream.CopyTo (destination);
			written = destination.Length;
		}
		RecordWrite (written);
	}

	public bool ContainsEntry (string archivePath) => Archive.GetEntry (archivePath) is not null;

	public ZipArchiveEntry GetEntry (string archivePath) =>
		Archive.GetEntry (archivePath) ?? throw new ArgumentOutOfRangeException (nameof (archivePath));

	public IEnumerable<string> GetAllEntryNames () => Archive.Entries.Select (entry => entry.FullName).Distinct (StringComparer.Ordinal).ToArray ();

	public void DeleteEntry (string archivePath)
	{
		if (Archive.GetEntry (archivePath) is null)
			return;
		foreach (var entry in Archive.Entries.Where (item => item.FullName == archivePath).ToArray ()) {
			entry.Delete ();
			modified = true;
		}
	}

	public bool MoveEntry (string oldPath, string newPath)
	{
		var entry = Archive.GetEntry (oldPath);
		if (entry is null)
			return false;
		if (oldPath == newPath)
			return true;

		if (!TryGetEntryLength (entry, out var size)) {
			Flush ();
			entry = GetEntry (oldPath);
			size = entry.Length;
		}
		var compression = GetCompressionLevel (entry);
		var lastWriteTime = entry.LastWriteTime;
		var attributes = entry.ExternalAttributes;
		// Moving an entry buffers both its old and new uncompressed contents.
		var bufferedBytes = checked (size * 2);
		PrepareWrite (bufferedBytes);
		entry = GetEntry (oldPath);
		DeleteEntry (newPath);
		var replacement = Archive.CreateEntry (newPath, compression);
		replacement.LastWriteTime = lastWriteTime;
		replacement.ExternalAttributes = attributes;
		using (var source = entry.Open ())
		using (var destination = replacement.Open ())
			source.CopyTo (destination);
		DeleteEntry (oldPath);
		RecordWrite (bufferedBytes);
		return true;
	}

	public void FixupWindowsPathSeparators (Action<string, string> onRename)
	{
		var malformedNames = Archive.Entries.Select (entry => entry.FullName).Where (name => name.Contains ('\\')).ToArray ();
		foreach (var oldName in malformedNames) {
			var name = oldName.Replace ('\\', '/');
			onRename (oldName, name);
			MoveEntry (oldName, name);
		}
	}

	public void FixupWindowsPathSeparators (TaskLoggingHelper log) =>
		FixupWindowsPathSeparators ((oldPath, newPath) => log.LogDebugMessage ($"Fixing up malformed entry `{oldPath}` -> `{newPath}`"));

	internal static CompressionLevel GetCompressionLevel (ZipArchiveEntry entry) => entry.CompressionMethod switch {
		ZipCompressionMethod.Stored => CompressionLevel.NoCompression,
		ZipCompressionMethod.Deflate => CompressionLevel.Optimal,
		_ => throw new NotSupportedException ($"Unsupported ZIP compression method: {entry.CompressionMethod}"),
	};

	static DateTime ToDosTime (DateTime time) =>
		new DateTime (time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second / 2 * 2, DateTimeKind.Utc);

	public void Dispose () => Archive.Dispose ();
}
