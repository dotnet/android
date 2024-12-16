using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

interface IZipArchive : IDisposable
{
	int ZipFlushFilesLimit { get; set; }
	int ZipFlushSizeLimit { get; set; }

	void AddEntry (byte [] data, string apkPath);
	void AddEntry (Stream stream, string apkPath, CompressionLevel compression);
	bool AddFileIfChanged (TaskLoggingHelper log, string filename, string archiveFileName, CompressionLevel compression);
	bool ContainsEntry (string entryPath);
	void DeleteEntry (string entry);
	void FixupWindowsPathSeparators (TaskLoggingHelper log);
	IEnumerable<string> GetAllEntryNames ();
	IZipArchiveEntry GetEntry (string entryName);
	void MoveEntry (string oldEntry, string newEntry);
}

interface IZipArchiveEntry
{
	uint CRC { get; }
	ulong CompressedSize { get; }
}

class ZipArchiveDotNet : IZipArchive
{
	const int DEFAULT_FLUSH_SIZE_LIMIT = 100 * 1024 * 1024;
	const int DEFAULT_FLUSH_FILES_LIMIT = 512;

	// ZipFile seems to be performant enough that we have not currently implemented a flush mechanism.
	public int ZipFlushSizeLimit { get; set; } = DEFAULT_FLUSH_SIZE_LIMIT;
	public int ZipFlushFilesLimit { get; set; } = DEFAULT_FLUSH_FILES_LIMIT;

	public ZipArchive Archive { get; }

	static readonly FieldInfo? crc_field;
	static readonly FieldInfo? comp_field;

	// This is private to ensure the only way to create an instance is through the Create method.
	ZipArchiveDotNet (string archive, ZipArchiveMode mode)
	{
		Archive = ZipFile.Open (archive, mode);
	}

	static ZipArchiveDotNet ()
	{
		// netstandard2.0 does not provide a way to access a ZipArchiveEntry's CRC or compresion level.
		// We need to use reflection to access the private fields.

		// These private fields exist on both .NET Framework 4.7.2 and .NET 9.0. If they are not found,
		// we will return a ZipArchiveEx which uses libZipSharp instead.
		crc_field = typeof (ZipArchiveEntry).GetField ("_crc32", BindingFlags.NonPublic | BindingFlags.Instance);
		comp_field = typeof (ZipArchiveEntry).GetField ("_storedCompressionMethod", BindingFlags.NonPublic | BindingFlags.Instance);
	}

	public static IZipArchive Create (TaskLoggingHelper log, string archive, ZipArchiveMode mode)
	{
		if (crc_field is null) {
			log.LogDebugMessage ($"ZipArchiveDotNet: Could not find private CRC field, falling back to libZipSharp.");
			return new ZipArchiveEx (archive, mode.ToFileMode ());
		}

		if (comp_field is null) {
			log.LogDebugMessage ($"ZipArchiveDotNet: Could not find private CompressionMethod field, falling back to libZipSharp.");
			return new ZipArchiveEx (archive, mode.ToFileMode ());
		}

		return new ZipArchiveDotNet (archive, mode);
	}

	public void AddEntry (byte [] data, string apkPath)
	{
		var entry = Archive.CreateEntry (apkPath);

		using (var stream = entry.Open ())
			stream.Write (data, 0, data.Length);
	}

	public void AddEntry (Stream stream, string apkPath, CompressionLevel compression)
	{
		var entry = Archive.CreateEntry (apkPath, compression);

		using (var entry_stream = entry.Open ())
			stream.CopyTo (entry_stream);
	}

	public bool AddFileIfChanged (TaskLoggingHelper log, string filename, string archiveFileName, CompressionLevel compression)
	{
		if (!FileNeedsUpdating (log, filename, archiveFileName, compression))
			return false;

		DeleteEntry (archiveFileName);
		Archive.CreateEntryFromFile (filename, archiveFileName, compression);

		return true;
	}

	public bool ContainsEntry (string entryName)
	{
		return Archive.GetEntry (entryName) is not null;
	}

	public void DeleteEntry (string entryName)
	{
		var entry = Archive.GetEntry (entryName);

		entry?.Delete ();
	}

	/// <summary>
	/// HACK: aapt2 is creating zip entries on Windows such as `assets\subfolder/asset2.txt`
	/// </summary>
	public void FixupWindowsPathSeparators (TaskLoggingHelper log)
	{
		var malformed_entries = Archive.Entries.Where (entry => entry.FullName.Contains ('\\')).ToList ();

		foreach (var entry in malformed_entries) {
			var name = entry.FullName.Replace ('\\', '/');
			if (name != entry.FullName) {
				log.LogDebugMessage ($"Fixing up malformed entry `{entry.FullName}` -> `{name}`");
				MoveEntry (entry.FullName, name);
			}
		}
	}

	public IEnumerable<string> GetAllEntryNames ()
	{
		return Archive.Entries.Select (entry => entry.FullName);
	}

	public IZipArchiveEntry GetEntry (string entryName)
	{
		var entry = Archive.GetEntry (entryName);

		if (entry is null)
			throw new ArgumentOutOfRangeException (nameof (entryName));

		return new ZipArchiveEntryDotNet (entry, GetCrc32 (entry));
	}

	public void Dispose ()
	{
		Archive.Dispose ();
	}

	public void MoveEntry (string oldPath, string newPath)
	{
		var old_entry = Archive.GetEntry (oldPath);

		if (old_entry is null)
			return;

		var new_entry = Archive.CreateEntry (newPath);

		using (var oldStream = old_entry.Open ())
		using (var newStream = new_entry.Open ())
			oldStream.CopyTo (newStream);

		old_entry.Delete ();
	}

	bool FileNeedsUpdating (TaskLoggingHelper log, string filename, string archiveFileName, CompressionLevel compression)
	{
		var entry = Archive.GetEntry (archiveFileName);

		if (entry is null) {
			log.LogDebugMessage ($"Adding {filename} as it doesn't already exist.");
			return true;
		}

		var stored_compression = GetCompressionLevel (entry);

		if (stored_compression != compression) {
			log.LogDebugMessage ($"Updating {filename} as the compression level changed: existing - '{stored_compression}', requested - '{compression}'.");
			return true;
		}

		var last_write = File.GetLastWriteTimeUtc (filename);
		var file_write_dos_time = DateTimeToDosTime (last_write);
		var zip_write_dos_time = DateTimeToDosTime (entry.LastWriteTime.UtcDateTime);

		if (DateTimeToDosTime (entry.LastWriteTime.UtcDateTime) < DateTimeToDosTime (last_write)) {
			log.LogDebugMessage ($"Updating {filename} as the file write time is newer: file in zip - '{zip_write_dos_time}', file on disk - '{file_write_dos_time}'.");
			return true;
		}

		log.LogDebugMessage ($"Skipping {filename} as the archive file is up to date.");
		return false;
	}

	static uint GetCrc32 (ZipArchiveEntry entry)
	{
		if (crc_field is null)
			throw new NotSupportedException ("This method is not supported on this platform.");

		return (uint) crc_field.GetValue (entry);
	}

	static CompressionLevel GetCompressionLevel (ZipArchiveEntry entry)
	{
		if (comp_field is null)
			throw new NotSupportedException ("This method is not supported on this platform.");

		var level = comp_field.GetValue (entry).ToString ();

		switch (level) {
			case "Stored":
				return CompressionLevel.NoCompression;
			case "Deflate":
				return CompressionLevel.Optimal;
			default:
				throw new NotSupportedException ($"Unsupported compression level: {level}");
		}
	}

	// System.IO.Compression.ZipArchive apparently only provides a 2 second granularity for the LastWriteTime.
	// This should be fine, it would be nearly impossible for someone to complete a build, make a change,
	// and rebuild in under 2 seconds.
	// This is a port of the DateTimeToDosTime method from System.IO.Compression.ZipHelper
	// https://github.com/dotnet/runtime/blob/373f048bae3c46810bc030ed7c1ee0568ee5ecc0/src/libraries/System.IO.Compression/src/System/IO/Compression/ZipHelper.cs#L88
	const int ValidZipDate_YearMin = 1980;

	static uint DateTimeToDosTime (DateTime dateTime)
	{
		int ret = ((dateTime.Year - ValidZipDate_YearMin) & 0x7F);
		ret = (ret << 4) + dateTime.Month;
		ret = (ret << 5) + dateTime.Day;
		ret = (ret << 5) + dateTime.Hour;
		ret = (ret << 6) + dateTime.Minute;
		ret = (ret << 5) + (dateTime.Second / 2); // only 5 bits for second, so we only have a granularity of 2 sec.
		return (uint) ret;
	}

	class ZipArchiveEntryDotNet : IZipArchiveEntry
	{
		readonly ZipArchiveEntry entry;
		readonly uint crc;

		public uint CRC => crc;
		public ulong CompressedSize => (ulong) entry.CompressedLength;

		public ZipArchiveEntryDotNet (ZipArchiveEntry entry, uint crc)
		{
			this.entry = entry;
			this.crc = crc;
		}
	}
}
