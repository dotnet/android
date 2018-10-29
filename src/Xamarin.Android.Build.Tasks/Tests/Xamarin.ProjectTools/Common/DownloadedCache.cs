using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Xamarin.ProjectTools
{
	public class DownloadedCache
	{
		static readonly ConcurrentDictionary<string, object> locks = new ConcurrentDictionary<string, object> ();

		public DownloadedCache ()
			: this (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Xamarin.ProjectTools"))
		{
		}

		public DownloadedCache (string cacheDirectory)
		{
			if (cacheDirectory == null)
				throw new ArgumentNullException ("cacheDirectory");
			CacheDirectory = cacheDirectory;
		}

		public string CacheDirectory { get; private set; }

		public string GetAsFile (string url, string md5 = null)
		{
			Directory.CreateDirectory (CacheDirectory);

			var filename = Path.Combine (CacheDirectory, Path.GetFileName (new Uri (url).LocalPath));
			lock (locks.GetOrAdd (filename, _ => new object ())) {
				if (File.Exists (filename) && (md5 == null || GetMd5 (filename) == md5))
					return filename;
				// FIXME: should be clever enough to resolve name conflicts.
				new System.Net.WebClient ().DownloadFile (url, filename);
				if (md5 != null && GetMd5 (filename) != md5)
					throw new InvalidOperationException (string.Format ("The given md5sum doesn't match the actual web resource. '{0}' for '{1}'", md5, url));
				return filename;
			}
		}

		string GetMd5 (string filename)
		{
			var md5 = MD5.Create ();
			return new string (md5.ComputeHash (File.ReadAllBytes (filename)).Select (b => (char) (b < 10 ? '0' + b : 'a' + b - 10)).ToArray ());
		}
	}
}

