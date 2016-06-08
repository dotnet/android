using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Security.Cryptography;

namespace Xamarin.Android.Tasks
{
	static class ZipArchiveExtensions 
	{
		public static void AddEntry (this ZipArchive archive, string entryName, Stream data,
			CompressionLevel compressionLevel = CompressionLevel.Optimal )
		{
			ZipArchiveEntry entry = archive.CreateEntry(entryName, compressionLevel: compressionLevel);
			using (StreamWriter writer = new StreamWriter(entry.Open()))
			{
				data.CopyTo (writer.BaseStream);
				writer.Flush ();
			}
		}

		public static void AddEntry (this ZipArchive archive, string entryName, byte[] data,
			CompressionLevel compressionLevel = CompressionLevel.Optimal)
		{
			ZipArchiveEntry entry = archive.CreateEntry (entryName, compressionLevel: compressionLevel);
			using (StreamWriter writer = new StreamWriter (entry.Open ())) {
				writer.BaseStream.Write (data, 0, data.Length);
				writer.Flush ();
			}
		}

		public static void AddEntry (this ZipArchive archive, string entryName, string s, Encoding encoding,
			CompressionLevel compressionLevel = CompressionLevel.Optimal)
		{
			ZipArchiveEntry entry = archive.CreateEntry (entryName, compressionLevel: compressionLevel);
			using (StreamWriter writer = new StreamWriter (entry.Open ())) {
				var data = encoding.GetBytes (s);
				writer.BaseStream.Write (data, 0, data.Length);
				writer.Flush ();
			}
		}

		internal static string ArchiveNameForFile (string filename, string directoryPathInZip)
		{
			string pathName;
			if (string.IsNullOrEmpty (directoryPathInZip)) {
				pathName = Path.GetFileName (filename);
			}
			else {
				pathName = Path.Combine (directoryPathInZip, Path.GetFileName (filename));
			}
			return pathName.Replace ("\\", "/");
		}

		public static ZipArchiveEntry AddFile (this ZipArchive archive, string fileName, string directoryPathInZip = null, CompressionLevel compressionLevel = CompressionLevel.Optimal )
		{
			ZipArchiveEntry entry = archive.CreateEntry(ArchiveNameForFile(fileName, directoryPathInZip), compressionLevel: compressionLevel);
			using (StreamWriter writer = new StreamWriter(entry.Open()))
			{
				var data = File.ReadAllBytes (fileName);
				writer.BaseStream.Write (data, 0, data.Length);
			}
			return entry;
		}

		public static void AddFiles (this ZipArchive archive, IEnumerable<string> fileNames, string directoryPathInZip)
		{
			foreach (var file in fileNames) {
				AddFile (archive, file, directoryPathInZip);
			}
		}

		public static bool ContainsEntry (this ZipArchive archive, string entryName)
		{
			return archive.Entries.Any (x => string.Compare (x.Name, entryName, StringComparison.OrdinalIgnoreCase) == 0);
		}

		public static bool IsDirectory (this ZipArchiveEntry entry)
		{
			return entry.FullName.EndsWith ("/", StringComparison.OrdinalIgnoreCase);
		}

		public static void Extract (this ZipArchiveEntry entry, Stream stream)
		{
			entry.Open ().CopyTo (stream);
		}

		public static void Extract (this ZipArchiveEntry entry, string destination)
		{
			entry.ExtractToFile (destination, overwrite: true);
		}

		public static void AddDirectory (this ZipArchive archive, string folder, string folderInArchive)
		{
			string root = folderInArchive;
			foreach(var fileName in Directory.GetFiles (folder)) {
				archive.AddFile (fileName, root);
			}
			foreach (var dir in Directory.GetDirectories (folder)) {
				var internalDir = dir.Replace ("./", string.Empty).Replace (folder, string.Empty);
				archive.AddDirectory (dir, folderInArchive + internalDir);
			}
		}

		public static string Hash (this ZipArchiveEntry entry)
		{
			using (var stream = entry.Open ())
			using (var sha1 = SHA1.Create ()) {
				return Convert.ToBase64String (sha1.ComputeHash (stream));
			}
		}
	}
}

