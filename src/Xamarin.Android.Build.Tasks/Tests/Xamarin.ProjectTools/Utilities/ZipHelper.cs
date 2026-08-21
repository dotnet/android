using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Xamarin.ProjectTools
{
	public static class ZipHelper
	{

		public static ZipArchive OpenZip (string zipFile)
		{
			if (!File.Exists (zipFile))
				return null;
			return ZipFile.OpenRead (zipFile);
		}

		public static byte [] ReadFileFromZip (ZipArchive zip, string filename)
		{
			var entry = zip.Entries.FirstOrDefault (x => x.FullName == filename);
			if (entry != null) {
				using (var ms = new MemoryStream ())
				using (var stream = entry.Open ()) {
					stream.CopyTo (ms);
					return ms.ToArray ();
				}
			}

			return null;
		}

		public static byte [] ReadFileFromZip (string zipFile, string filename)
		{
			using (var zip = ZipFile.OpenRead (zipFile)) {
				return ReadFileFromZip (zip, filename);
			}
		}

		public static IEnumerator<ZipArchiveEntry> GetEnumerator (this ZipArchive zip)
			=> zip.Entries.GetEnumerator ();

		public static bool Any (this ZipArchive zip, Func<ZipArchiveEntry, bool> predicate)
			=> zip.Entries.Any (predicate);

		public static int Count (this ZipArchive zip, Func<ZipArchiveEntry, bool> predicate)
			=> zip.Entries.Count (predicate);

		public static ZipArchiveEntry Single (this ZipArchive zip, Func<ZipArchiveEntry, bool> predicate)
			=> zip.Entries.Single (predicate);

		public static IEnumerable<ZipArchiveEntry> Where (this ZipArchive zip, Func<ZipArchiveEntry, bool> predicate)
			=> zip.Entries.Where (predicate);

		public static IEnumerable<TResult> Select<TResult> (this ZipArchive zip, Func<ZipArchiveEntry, TResult> selector)
			=> zip.Entries.Select (selector);
	}
}
