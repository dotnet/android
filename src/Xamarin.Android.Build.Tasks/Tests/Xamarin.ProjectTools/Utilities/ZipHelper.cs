using System;
using System.IO;
using System.Linq;
using Ionic.Zip;

namespace Xamarin.ProjectTools
{
	public static class ZipHelper
	{

		public static ZipFile OpenZip (string zipFile)
		{
			if (!File.Exists (zipFile))
				return null;
			return ZipFile.Read (zipFile);
		}

		public static byte [] ReadFileFromZip (ZipFile zip, string filename)
		{
			if (zip.ContainsEntry (filename)) {
				var entry = zip.FirstOrDefault (x => x.FileName == filename);
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
			using (var zip = ZipFile.Read (zipFile)) {
				return ReadFileFromZip (zip, filename);
			}
		}
	}
}

