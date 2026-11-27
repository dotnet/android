using System;
using System.IO;
using System.IO.Compression;

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
			var entry = zip.GetEntry (filename);
			if (entry != null) {
				using (var stream = entry.Open ())
				using (var ms = new MemoryStream ()) {
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
	}
}
