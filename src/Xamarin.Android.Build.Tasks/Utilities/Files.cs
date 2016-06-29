using System;
using System.IO;
using System.Security.Cryptography;

using Xamarin.Tools.Zip;
#if MSBUILD
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks;
#endif

namespace Xamarin.Android.Tools {

	static class Files {

		public static bool Archive (string target, Action<string> archiver)
		{
			string newTarget = target + ".new";

			archiver (newTarget);

			bool changed = CopyIfChanged (newTarget, target);

			try {
				File.Delete (newTarget);
			} catch {
			}

			return changed;
		}

		public static bool ArchiveZip (string target, Action<string> archiver)
		{
			string newTarget = target + ".new";

			archiver (newTarget);

			bool changed = CopyIfZipChanged (newTarget, target);

			try {
				File.Delete (newTarget);
			} catch {
			}

			return changed;
		}

		public static bool CopyIfChanged (string source, string destination)
		{
			if (HasFileChanged (source, destination)) {
				var directory = Path.GetDirectoryName (destination);
				if (!string.IsNullOrEmpty (directory))
					Directory.CreateDirectory (directory);

				File.Copy (source, destination, true);
				return true;
			}/* else
				Console.WriteLine ("Skipping copying {0}, unchanged", Path.GetFileName (destination));*/

			return false;
		}

		public static bool CopyIfZipChanged (Stream source, string destination)
		{
			string hash;
			if (HasZipChanged (source, destination, out hash)) {
				Directory.CreateDirectory (Path.GetDirectoryName (destination));
				source.Position = 0;
				using (var f = File.Create (destination)) {
					source.CopyTo (f);
				}
#if TESTCACHE
				if (hash != null)
					File.WriteAllText (destination + ".hash", hash);
#endif
				return true;
			}/* else
				Console.WriteLine ("Skipping copying {0}, unchanged", Path.GetFileName (destination));*/

			return false;
		}

		public static bool CopyIfZipChanged (string source, string destination)
		{
			string hash;
			if (HasZipChanged (source, destination, out hash)) {
				Directory.CreateDirectory (Path.GetDirectoryName (destination));

				File.Copy (source, destination, true);
#if TESTCACHE
				if (hash != null)
					File.WriteAllText (destination + ".hash", hash);
#endif
				return true;
			}/* else
				Console.WriteLine ("Skipping copying {0}, unchanged", Path.GetFileName (destination));*/

			return false;
		}

		public static bool HasZipChanged (Stream source, string destination, out string hash)
		{
			hash = null;

			string src_hash = hash = HashZip (source);

			if (!File.Exists (destination))
				return true;

			string dst_hash = HashZip (destination);

			if (src_hash == null || dst_hash == null)
				return true;

			return src_hash != dst_hash;
		}

		public static bool HasZipChanged (string source, string destination, out string hash)
		{
			hash = null;
			if (!File.Exists (source))
				return true;

			string src_hash = hash = HashZip (source);

			if (!File.Exists (destination))
				return true;

			string dst_hash = HashZip (destination);

			if (src_hash == null || dst_hash == null)
				return true;

			return src_hash != dst_hash;
		}

		// This is for if the file contents have changed.  Often we have to
		// regenerate a file, but we don't want to update it if hasn't changed
		// so that incremental build is as efficient as possible
		public static bool HasFileChanged (string source, string destination)
		{
			// If either are missing, that's definitely a change
			if (!File.Exists (source) || !File.Exists (destination))
				return true;

			var src_hash = HashFile (source);
			var dst_hash = HashFile (destination);

			// If the hashed don't match, then the file has changed
			if (src_hash != dst_hash)
				return true;

			return false;
		}

		static string HashZip (Stream stream)
		{
			string hashes = String.Empty;

			try {
				using (var zip = ZipArchive.Open (stream)) {
					foreach (var item in zip) {
						hashes += String.Format ("{0}{1}", item.FullName, item.CRC);
					}
				}
			} catch {
				return null;
			}
			return hashes;
		}

		static string HashZip (string filename)
		{
			string hashes = String.Empty;

			try {
				// check cache
				if (File.Exists (filename + ".hash"))
					return File.ReadAllText (filename + ".hash");

				using (var zip = ReadZipFile (filename)) {
					foreach (var item in zip) {
						hashes += String.Format ("{0}{1}", item.FullName, item.CRC);
					}
				}
			} catch {
				return null;
			}
			return hashes;
		}

		public static ZipArchive ReadZipFile (string filename)
		{
			return ZipArchive.Open (filename, FileMode.Open);
		}

		public static void ExtractAll(ZipArchive zip, string destination, Action<int, int> progressCallback = null)
		{
			int i = 0;
			int total = (int)zip.EntryCount;
			foreach (var entry in zip) {
				if (entry.FullName.Contains ("/__MACOSX/") ||
						entry.FullName.EndsWith ("/__MACOSX", StringComparison.OrdinalIgnoreCase) ||
						entry.FullName.EndsWith ("/.DS_Store", StringComparison.OrdinalIgnoreCase))
					continue;
				if (entry.IsDirectory) {
					Directory.CreateDirectory (Path.Combine (destination, entry.FullName));
					continue;
				}
				if (progressCallback != null)
					progressCallback (i++, total);
				Directory.CreateDirectory (Path.Combine (destination, Path.GetDirectoryName (entry.FullName)));
				entry.Extract (destination, entry.FullName);
			}
		}

		public static string HashFile (string filename)
		{
			using (HashAlgorithm hashAlg = new SHA1Managed ()) {
				return HashFile (filename, hashAlg);
			}
		}

		public static string HashFile (string filename, HashAlgorithm hashAlg)
		{
			using (Stream file = new FileStream (filename, FileMode.Open, FileAccess.Read)) {
				byte[] hash = hashAlg.ComputeHash (file);

				return BitConverter.ToString (hash);
			}
		}

		public static string HashStream (Stream stream)
		{
			using (HashAlgorithm hashAlg = new SHA1Managed ()) {
				byte[] hash = hashAlg.ComputeHash (stream);
				return BitConverter.ToString (hash);
			}
		}

		public static void DeleteFile (string filename, object log)
		{
			try {
				File.Delete (filename);
			} catch (Exception ex) {
#if MSBUILD
				var helper = log as TaskLoggingHelper;
				helper.LogErrorFromException (ex);
#else
				Console.Error.WriteLine (ex.ToString ());
#endif
			}
		}
	}
}

