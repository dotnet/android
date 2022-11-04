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

		public string GetAsFile (string url, string filename = "")
		{
			Directory.CreateDirectory (CacheDirectory);

			filename = Path.Combine (CacheDirectory, string.IsNullOrEmpty (filename) ? Path.GetFileName (new Uri (url).LocalPath) : filename);
			lock (locks.GetOrAdd (filename, _ => new object ())) {
				if (File.Exists (filename))
					return filename;
				// FIXME: should be clever enough to resolve name conflicts.
				new System.Net.WebClient ().DownloadFile (url, filename);
				return filename;
			}
		}
	}
}

