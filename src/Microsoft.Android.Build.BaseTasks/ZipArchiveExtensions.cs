using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Microsoft.Android.Build.Tasks
{
	public static class ZipArchiveExtensions
	{
		public static CompressionLevel ToCompressionLevel (this ZipEntryCompressionMethod compressionMethod)
		{
			return compressionMethod switch {
				ZipEntryCompressionMethod.Store => CompressionLevel.NoCompression,
				ZipEntryCompressionMethod.Deflate => CompressionLevel.Optimal,
				_ => throw new NotSupportedException ($"Unsupported ZIP compression method: {(ushort) compressionMethod}"),
			};
		}

		public static ZipArchive OpenZip (string archivePath, FileMode fileMode, Encoding? entryNameEncoding = null)
		{
			if (archivePath == null)
				throw new ArgumentNullException (nameof (archivePath));

			FileMode actualFileMode = fileMode;
			ZipArchiveMode archiveMode;

			switch (fileMode) {
				case FileMode.Create:
				case FileMode.CreateNew:
				case FileMode.Truncate:
					archiveMode = ZipArchiveMode.Create;
					break;
				case FileMode.Open:
					archiveMode = ZipArchiveMode.Update;
					break;
				case FileMode.OpenOrCreate:
					archiveMode = ZipArchiveMode.Update;
					break;
				default:
					throw new ArgumentOutOfRangeException (nameof (fileMode), fileMode, null);
			}

			var stream = new FileStream (archivePath, actualFileMode, FileAccess.ReadWrite, FileShare.Read);
			return OpenZip (stream, archiveMode, leaveOpen: false, entryNameEncoding);
		}

		public static ZipArchive OpenZip (Stream stream, ZipArchiveMode mode = ZipArchiveMode.Read, bool leaveOpen = false, Encoding? entryNameEncoding = null)
		{
			if (stream == null)
				throw new ArgumentNullException (nameof (stream));

			return new ZipArchive (stream, mode, leaveOpen, entryNameEncoding);
		}

		public static bool ContainsEntry (this ZipArchive archive, string entryName, StringComparison comparison = StringComparison.Ordinal)
			=> archive.ReadEntry (entryName, comparison) != null;

		public static ZipArchiveEntry? ReadEntry (this ZipArchive archive, string entryName, StringComparison comparison = StringComparison.Ordinal)
		{
			if (archive == null)
				throw new ArgumentNullException (nameof (archive));
			if (entryName == null)
				throw new ArgumentNullException (nameof (entryName));

			return archive.Entries.FirstOrDefault (entry => string.Equals (entry.FullName, entryName, comparison));
		}

		public static bool IsDirectory (this ZipArchiveEntry entry)
		{
			if (entry == null)
				throw new ArgumentNullException (nameof (entry));

			return entry.FullName.EndsWith ("/", StringComparison.Ordinal) || entry.FullName.EndsWith ("\\", StringComparison.Ordinal);
		}

		public static void Extract (this ZipArchiveEntry entry, Stream destination)
		{
			if (entry == null)
				throw new ArgumentNullException (nameof (entry));
			if (destination == null)
				throw new ArgumentNullException (nameof (destination));

			// Some Android archives encode empty stored entries with non-zero compressed data.
			// ZipArchive validates that data when opening the entry, even though there is
			// nothing to extract.
			if (entry.Length == 0)
				return;

			using var source = entry.Open ();
			source.CopyTo (destination);
		}

		public static void Extract (this ZipArchiveEntry entry, string destinationDirectory, string? destinationFileName = null)
		{
			if (entry == null)
				throw new ArgumentNullException (nameof (entry));
			if (destinationDirectory == null)
				throw new ArgumentNullException (nameof (destinationDirectory));

			var fileName = destinationFileName ?? entry.FullName.Replace ('/', Path.DirectorySeparatorChar);
			var destinationPath = Path.Combine (destinationDirectory, fileName);
			var destinationFolder = Path.GetDirectoryName (destinationPath);
			if (!string.IsNullOrEmpty (destinationFolder))
				Directory.CreateDirectory (destinationFolder);

			using var output = File.Create (destinationPath);
			entry.Extract (output);
		}

		public static void AddEntry (this ZipArchive archive, string entryName, string contents, Encoding encoding, CompressionLevel compressionLevel = CompressionLevel.Optimal)
		{
			if (archive == null)
				throw new ArgumentNullException (nameof (archive));
			if (entryName == null)
				throw new ArgumentNullException (nameof (entryName));
			if (contents == null)
				throw new ArgumentNullException (nameof (contents));
			if (encoding == null)
				throw new ArgumentNullException (nameof (encoding));

			DeleteEntry (archive, entryName);
			var entry = archive.CreateEntry (entryName, compressionLevel);
			using var writer = new StreamWriter (entry.Open (), encoding);
			writer.Write (contents);
		}

		public static void AddStream (this ZipArchive archive, Stream source, string entryName, CompressionLevel compressionLevel = CompressionLevel.Optimal)
		{
			if (archive == null)
				throw new ArgumentNullException (nameof (archive));
			if (source == null)
				throw new ArgumentNullException (nameof (source));
			if (entryName == null)
				throw new ArgumentNullException (nameof (entryName));

			DeleteEntry (archive, entryName);
			var entry = archive.CreateEntry (entryName, compressionLevel);
			using var destination = entry.Open ();
			source.CopyTo (destination);
		}

		public static void AddFile (this ZipArchive archive, string filePath, string entryName, CompressionLevel compressionLevel = CompressionLevel.Optimal)
		{
			if (archive == null)
				throw new ArgumentNullException (nameof (archive));
			if (filePath == null)
				throw new ArgumentNullException (nameof (filePath));
			if (entryName == null)
				throw new ArgumentNullException (nameof (entryName));

			DeleteEntry (archive, entryName);
			ZipFileExtensions.CreateEntryFromFile (archive, filePath, entryName, compressionLevel);
		}

		public static void AddDirectory (this ZipArchive archive, string directory, string directoryPathInArchive = "", CompressionLevel compressionLevel = CompressionLevel.Optimal)
		{
			if (archive == null)
				throw new ArgumentNullException (nameof (archive));
			if (directory == null)
				throw new ArgumentNullException (nameof (directory));

			directory = directory.Replace ('/', Path.DirectorySeparatorChar).Replace ('\\', Path.DirectorySeparatorChar);
			directory = Path.GetFullPath (directory);
			if (directory [directory.Length - 1] == Path.DirectorySeparatorChar)
				directory = directory.Substring (0, directory.Length - 1);

			AddDirectoryContents (directory);

			void AddDirectoryContents (string currentDirectory)
			{
				foreach (var filePath in Directory.GetFiles (currentDirectory, "*.*", SearchOption.TopDirectoryOnly).OrderBy (path => path, StringComparer.Ordinal)) {
					var fileInfo = new FileInfo (filePath);
					if ((fileInfo.Attributes & FileAttributes.Hidden) != 0)
						continue;

					var relativePath = filePath.Substring (directory.Length).TrimStart (Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace ('\\', '/');
					var entryName = string.IsNullOrEmpty (directoryPathInArchive) ? relativePath : $"{directoryPathInArchive.TrimEnd ('/')}/{relativePath}";
					archive.AddFile (filePath, entryName, compressionLevel);
				}

				foreach (var childDirectory in Directory.GetDirectories (currentDirectory, "*", SearchOption.TopDirectoryOnly).OrderBy (path => path, StringComparer.Ordinal)) {
					var directoryInfo = new DirectoryInfo (childDirectory);
					if ((directoryInfo.Attributes & FileAttributes.Hidden) != 0)
						continue;

					AddDirectoryContents (childDirectory);
				}
			}
		}

		public static bool MoveEntry (this ZipArchive archive, string oldEntryName, string newEntryName, CompressionLevel compressionLevel)
		{
			if (archive == null)
				throw new ArgumentNullException (nameof (archive));
			if (oldEntryName == null)
				throw new ArgumentNullException (nameof (oldEntryName));
			if (newEntryName == null)
				throw new ArgumentNullException (nameof (newEntryName));

			var source = archive.ReadEntry (oldEntryName, StringComparison.Ordinal);
			if (source == null)
				return false;

			using var buffer = MemoryStreamPool.Shared.Rent ();
			source.Extract (buffer);
			buffer.Position = 0;

			DeleteEntry (archive, newEntryName);
			var destination = archive.CreateEntry (newEntryName, compressionLevel);
			destination.LastWriteTime = source.LastWriteTime;
			using (var destinationStream = destination.Open ()) {
				buffer.CopyTo (destinationStream);
			}

			source.Delete ();
			return true;
		}

		public static void FixupWindowsPathSeparators (this ZipArchive archive, Func<ZipArchiveEntry, CompressionLevel> compressionLevelSelector, Action<string, string>? onRename = null)
		{
			if (archive == null)
				throw new ArgumentNullException (nameof (archive));
			if (compressionLevelSelector == null)
				throw new ArgumentNullException (nameof (compressionLevelSelector));

			foreach (var entryName in archive.Entries
				.Where (entry => entry.FullName.Contains ('\\'))
				.Select (entry => entry.FullName)
				.ToArray ()) {
				var entry = archive.ReadEntry (entryName, StringComparison.Ordinal);
				if (entry == null)
					continue;

				var normalizedName = entryName.Replace ('\\', '/');
				if (normalizedName == entryName)
					continue;

				var compressionLevel = compressionLevelSelector (entry);
				onRename?.Invoke (entryName, normalizedName);
				archive.MoveEntry (entryName, normalizedName, compressionLevel);
			}
		}

		static void DeleteEntry (ZipArchive archive, string entryName)
		{
			if (archive.Mode == ZipArchiveMode.Create)
				return;

			archive.ReadEntry (entryName, StringComparison.Ordinal)?.Delete ();
		}
	}
}
