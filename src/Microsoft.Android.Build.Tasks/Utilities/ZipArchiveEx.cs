#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Microsoft.Android.Tasks;

public sealed class ZipArchiveEx : IDisposable
{
	public ZipArchive Archive { get; }

	public ZipArchiveEx (string archive) : this (archive, FileMode.CreateNew)
	{
	}

	public ZipArchiveEx (string archive, FileMode mode)
	{
		var stream = new FileStream (archive, mode, FileAccess.ReadWrite);
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
			DeleteEntry (archivePath);
			Archive.CreateEntryFromFile (filename, archivePath, method);
		}
	}

	public bool SkipExistingFile (string filename, string archivePath, CompressionLevel compression)
	{
		var entry = Archive.GetEntry (archivePath);
		var method = compression == CompressionLevel.NoCompression ? ZipCompressionMethod.Stored : ZipCompressionMethod.Deflate;
		if (entry is null || entry.CompressionMethod != method)
			return false;

		var file = new FileInfo (filename);
		if (entry.Length != file.Length)
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

	public bool AddFileIfChanged (TaskLoggingHelper log, string filename, string archivePath, CompressionLevel compression)
	{
		if (SkipExistingFile (filename, archivePath, compression)) {
			log.LogDebugMessage ($"Skipping {filename} as the archive file is up to date.");
			return false;
		}

		DeleteEntry (archivePath);
		Archive.CreateEntryFromFile (filename, archivePath, compression);
		log.LogDebugMessage ($"Adding {filename} as the archive file is out of date.");
		return true;
	}

	public void AddEntry (byte [] data, string archivePath)
	{
		using var stream = new MemoryStream (data, writable: false);
		AddEntry (stream, archivePath, CompressionLevel.Optimal);
	}

	public void AddEntry (Stream stream, string archivePath, CompressionLevel compression)
	{
		DeleteEntry (archivePath);
		var entry = Archive.CreateEntry (archivePath, compression);
		using var destination = entry.Open ();
		stream.CopyTo (destination);
	}

	public bool ContainsEntry (string archivePath) => Archive.GetEntry (archivePath) is not null;

	public ZipArchiveEntry GetEntry (string archivePath) =>
		Archive.GetEntry (archivePath) ?? throw new ArgumentOutOfRangeException (nameof (archivePath));

	public IEnumerable<string> GetAllEntryNames () => Archive.Entries.Select (entry => entry.FullName);

	public void DeleteEntry (string archivePath)
	{
		while (Archive.GetEntry (archivePath) is ZipArchiveEntry entry)
			entry.Delete ();
	}

	public bool MoveEntry (string oldPath, string newPath)
	{
		var entry = Archive.GetEntry (oldPath);
		if (entry is null)
			return false;
		if (oldPath == newPath)
			return true;

		var compression = GetCompressionLevel (entry);
		var lastWriteTime = entry.LastWriteTime;
		var attributes = entry.ExternalAttributes;
		DeleteEntry (newPath);
		var replacement = Archive.CreateEntry (newPath, compression);
		replacement.LastWriteTime = lastWriteTime;
		replacement.ExternalAttributes = attributes;
		using (var source = entry.Open ())
		using (var destination = replacement.Open ())
			source.CopyTo (destination);
		entry.Delete ();
		return true;
	}

	public void FixupWindowsPathSeparators (Action<string, string> onRename)
	{
		foreach (var entry in Archive.Entries.Where (entry => entry.FullName.Contains ('\\')).ToArray ()) {
			var name = entry.FullName.Replace ('\\', '/');
			onRename (entry.FullName, name);
			MoveEntry (entry.FullName, name);
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
