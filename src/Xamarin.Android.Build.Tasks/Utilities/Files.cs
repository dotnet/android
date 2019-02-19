using System;
using System.IO;
using System.Security.Cryptography;

using Xamarin.Tools.Zip;
using System.Collections.Generic;
using System.Text;
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

		public static bool ArchiveZipUpdate(string target, Action<string> archiver)
		{
			var lastWrite = File.Exists (target) ? File.GetLastWriteTimeUtc (target) : DateTime.MinValue;
			archiver (target);
			return lastWrite < File.GetLastWriteTimeUtc (target);
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

				if (!Directory.Exists (source)) {
					File.Copy (source, destination, true);
					MonoAndroidHelper.SetWriteable (destination);
					File.SetLastWriteTimeUtc (destination, DateTime.UtcNow);
					File.SetLastAccessTimeUtc (destination, DateTime.UtcNow);
					return true;
				}
			}/* else
				Console.WriteLine ("Skipping copying {0}, unchanged", Path.GetFileName (destination));*/

			return false;
		}

		public static bool CopyIfStringChanged (string contents, string destination)
		{
			//NOTE: this is not optimal since it allocates a byte[]. We can improve this down the road with Span<T> or System.Buffers.
			var bytes = Encoding.UTF8.GetBytes (contents);
			return CopyIfBytesChanged (bytes, destination);
		}

		public static bool CopyIfBytesChanged (byte[] bytes, string destination)
		{
			if (HasBytesChanged (bytes, destination)) {
				var directory = Path.GetDirectoryName (destination);
				if (!string.IsNullOrEmpty (directory))
					Directory.CreateDirectory (directory);

				MonoAndroidHelper.SetWriteable (destination);
				File.WriteAllBytes (destination, bytes);
				return true;
			}
			return false;
		}

		public static bool CopyIfStreamChanged (Stream stream, string destination)
		{
			if (HasStreamChanged (stream, destination)) {
				var directory = Path.GetDirectoryName (destination);
				if (!string.IsNullOrEmpty (directory))
					Directory.CreateDirectory (directory);

				MonoAndroidHelper.SetWriteable (destination);
				using (var fileStream = File.Create (destination)) {
					stream.Position = 0; //HasStreamChanged read to the end
					stream.CopyTo (fileStream);
				}
				return true;
			}
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
				File.SetLastWriteTimeUtc (destination, DateTime.UtcNow);
				File.SetLastAccessTimeUtc (destination, DateTime.UtcNow);
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
				File.SetLastWriteTimeUtc (destination, DateTime.UtcNow);
				File.SetLastAccessTimeUtc (destination, DateTime.UtcNow);
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

			// If the hashes don't match, then the file has changed
			if (src_hash != dst_hash)
				return true;

			return false;
		}

		public static bool HasStreamChanged (Stream source, string destination)
		{
			//If destination is missing, that's definitely a change
			if (!File.Exists (destination))
				return true;

			var src_hash = HashStream (source);
			var dst_hash = HashFile (destination);

			// If the hashes don't match, then the file has changed
			if (src_hash != dst_hash)
				return true;

			return false;
		}

		public static bool HasBytesChanged (byte [] bytes, string destination)
		{
			//If destination is missing, that's definitely a change
			if (!File.Exists (destination))
				return true;

			var src_hash = HashBytes (bytes);
			var dst_hash = HashFile (destination);

			// If the hashes don't match, then the file has changed
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

		public static ZipArchive ReadZipFile (string filename, bool strictConsistencyChecks = false)
		{
			return ZipArchive.Open (filename, FileMode.Open, strictConsistencyChecks: strictConsistencyChecks);
		}

		public static bool ExtractAll (ZipArchive zip, string destination, Action<int, int> progressCallback = null, Func<string, string> modifyCallback = null,
			Func<string, bool> deleteCallback = null)
		{
			int i = 0;
			int total = (int)zip.EntryCount;
			bool updated = false;
			HashSet<string> files = new HashSet<string> ();
			using (var memoryStream = new MemoryStream ()) {
				foreach (var entry in zip) {
					progressCallback?.Invoke (i++, total);
					if (entry.IsDirectory)
						continue;
					if (entry.FullName.Contains ("/__MACOSX/") ||
							entry.FullName.EndsWith ("/__MACOSX", StringComparison.OrdinalIgnoreCase) ||
							entry.FullName.EndsWith ("/.DS_Store", StringComparison.OrdinalIgnoreCase))
						continue;
					var fullName = modifyCallback?.Invoke (entry.FullName) ?? entry.FullName;
					var outfile = Path.GetFullPath (Path.Combine (destination, fullName));
					files.Add (outfile);
					memoryStream.SetLength (0); //Reuse the stream
					entry.Extract (memoryStream);
					try {
						updated |= MonoAndroidHelper.CopyIfStreamChanged (memoryStream, outfile);
					} catch (PathTooLongException) {
						throw new PathTooLongException ($"Could not extract \"{fullName}\" to \"{outfile}\". Path is too long.");
					}
				}
			}
			if (Directory.Exists (destination)) {
				foreach (var file in Directory.GetFiles (destination, "*", SearchOption.AllDirectories)) {
					var outfile = Path.GetFullPath (file);
					if (outfile.Contains ("/__MACOSX/") ||
							outfile.EndsWith ("__AndroidLibraryProjects__.zip", StringComparison.OrdinalIgnoreCase) ||
							outfile.EndsWith ("/__MACOSX", StringComparison.OrdinalIgnoreCase) ||
							outfile.EndsWith ("/.DS_Store", StringComparison.OrdinalIgnoreCase))
						continue;
					if (!files.Contains (outfile) && (deleteCallback?.Invoke (outfile) ?? true)) {
						File.Delete (outfile);
						updated = true;
					}
				}
			}
			return updated;
		}

		public static string HashString (string s)
		{
			var bytes = Encoding.UTF8.GetBytes (s);
			return HashBytes (bytes);
		}

		public static string HashBytes (byte [] bytes)
		{
			using (HashAlgorithm hashAlg = new SHA1Managed ()) {
				byte [] hash = hashAlg.ComputeHash (bytes);
				return BitConverter.ToString (hash);
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
			stream.Position = 0;

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

		const uint ppdb_signature = 0x424a5342;

		public static bool IsPortablePdb (string filename)
		{
			try {
				using (var fs = new FileStream (filename, FileMode.Open, FileAccess.Read)) {
					using (var br = new BinaryReader (fs)) {
						return br.ReadUInt32 () == ppdb_signature;
					}
				}
			}
			catch {
				return false;
			}
		}
	}
}

