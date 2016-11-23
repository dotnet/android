using System;
using System.IO;
using System.Linq;
using Xamarin.Tools.Zip;

namespace Xamarin.ProjectTools
{
	public static class ZipHelper
	{

		public static ZipArchive OpenZip (string zipFile)
		{
			if (!File.Exists (zipFile))
				return null;
			return ZipArchive.Open (zipFile, FileMode.Open);
		}

		public static byte [] ReadFileFromZip (ZipArchive zip, string filename)
		{
			if (zip.ContainsEntry (filename)) {
				var entry = zip.FirstOrDefault (x => x.FullName == filename);
				if (entry != null) {
					using (var ms = new MemoryStream ()) {
						entry.Extract (ms);
						return ms.ToArray ();
					}
				}
			}
			return null;
		}

		public static byte [] ReadFileFromZip (string zipFile, string filename)
		{
			using (var zip = ZipArchive.Open (zipFile, FileMode.Open)) {
				return ReadFileFromZip (zip, filename);
			}
		}
	}
}

