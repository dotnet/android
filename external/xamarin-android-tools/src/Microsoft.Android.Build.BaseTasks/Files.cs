// https://github.com/xamarin/xamarin-android/blob/34acbbae6795854cc4e9f8eb7167ab011e0266b4/src/Xamarin.Android.Build.Tasks/Utilities/Files.cs
// https://github.com/xamarin/xamarin-android/blob/34acbbae6795854cc4e9f8eb7167ab011e0266b4/src/Xamarin.Android.Build.Tasks/Utilities/MonoAndroidHelper.cs#L409

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xamarin.Tools.Zip;
using Microsoft.Build.Utilities;
using System.Threading;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Collections;

namespace Microsoft.Android.Build.Tasks
{
	public static class Files
	{
		const int ERROR_ACCESS_DENIED = -2147024891;
		const int ERROR_SHARING_VIOLATION = -2147024864;

		const int DEFAULT_FILE_WRITE_RETRY_ATTEMPTS = 10;

		const int DEFAULT_FILE_WRITE_RETRY_DELAY_MS = 1000;

		static int fileWriteRetry = -1;
		static int fileWriteRetryDelay = -1;

		/// <summary>
		/// Windows has a MAX_PATH limit of 260 characters
		/// See: https://docs.microsoft.com/en-us/windows/win32/fileio/naming-a-file#maximum-path-length-limitation
		/// </summary>
		public const int MaxPath = 260;

		/// <summary>
		/// On Windows, we can opt into a long path with this prefix
		/// </summary>
		public const string LongPathPrefix = @"\\?\";

		public static readonly Encoding UTF8withoutBOM = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false);
		readonly static byte[] Utf8Preamble = Encoding.UTF8.GetPreamble ();

		/// <summary>
		/// Checks for the environment variable DOTNET_ANDROID_FILE_WRITE_RETRY_ATTEMPTS to
		/// see if a custom value for the number of times to retry writing a file has been 
		/// set.
		/// </summary>
		/// <returns>The value of DOTNET_ANDROID_FILE_WRITE_RETRY_ATTEMPTS or the default of DEFAULT_FILE_WRITE_RETRY_ATTEMPTS</returns>
		public static int GetFileWriteRetryAttempts ()
		{
			if (fileWriteRetry == -1) {
				var retryVariable = Environment.GetEnvironmentVariable ("DOTNET_ANDROID_FILE_WRITE_RETRY_ATTEMPTS");
				if (string.IsNullOrEmpty (retryVariable) || !int.TryParse (retryVariable, out fileWriteRetry))
					fileWriteRetry = DEFAULT_FILE_WRITE_RETRY_ATTEMPTS;
			}
			return fileWriteRetry;
		}

		/// <summary>
		/// Checks for the environment variable DOTNET_ANDROID_FILE_WRITE_RETRY_DELAY_MS to
		/// see if a custom value for the delay between trying to write a file has been 
		/// set.
		/// </summary>
		/// <returns>The value of DOTNET_ANDROID_FILE_WRITE_RETRY_DELAY_MS or the default of DEFAULT_FILE_WRITE_RETRY_DELAY_MS</returns>
		public static int GetFileWriteRetryDelay ()
		{
			if (fileWriteRetryDelay == -1) {
				var delayVariable = Environment.GetEnvironmentVariable ("DOTNET_ANDROID_FILE_WRITE_RETRY_DELAY_MS");
				if (string.IsNullOrEmpty (delayVariable) || !int.TryParse (delayVariable, out fileWriteRetryDelay))
					fileWriteRetryDelay = DEFAULT_FILE_WRITE_RETRY_DELAY_MS;
			}
			return fileWriteRetryDelay;
		} 
		/// <summary>
		/// Converts a full path to a \\?\ prefixed path that works on all Windows machines when over 260 characters
		/// NOTE: requires a *full path*, use sparingly
		/// </summary>
		public static string ToLongPath (string fullPath)
		{
			// On non-Windows platforms, return the path unchanged
			if (Path.DirectorySeparatorChar != '\\') {
				return fullPath;
			}
			return LongPathPrefix + fullPath;
		}

		public static void SetWriteable (string source, bool checkExists = true)
		{
			if (checkExists && !File.Exists (source))
				return;

			var attributes = File.GetAttributes (source);
			if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
				File.SetAttributes (source, attributes & ~FileAttributes.ReadOnly);
		}

		public static void SetDirectoryWriteable (string directory)
		{
			if (!Directory.Exists (directory))
				return;

			var dirInfo = new DirectoryInfo (directory);
			if ((dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
				dirInfo.Attributes &= ~FileAttributes.ReadOnly;

			foreach (var dir in Directory.EnumerateDirectories (directory, "*", SearchOption.AllDirectories)) {
				dirInfo = new DirectoryInfo (dir);
				if ((dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
					dirInfo.Attributes &= ~FileAttributes.ReadOnly;
			}

			foreach (var file in Directory.EnumerateFiles (directory, "*", SearchOption.AllDirectories)) {
				Files.SetWriteable (Path.GetFullPath (file));
			}
		}

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

		public static bool ArchiveZipUpdate (string target, Action<string> archiver)
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
			int retryCount = 0;
			int attempts = GetFileWriteRetryAttempts ();
			int delay = GetFileWriteRetryDelay ();
			while (retryCount <= attempts) {
				try {
					return CopyIfChangedOnce (source, destination);
				} catch (Exception e) {
					switch (e) {
						case UnauthorizedAccessException:
						case IOException:
							int code = Marshal.GetHRForException (e);
							if ((code != ERROR_ACCESS_DENIED && code != ERROR_SHARING_VIOLATION) || retryCount == attempts) {
								throw;
							};
							break;
						default:
							throw;
					} 
				}
				retryCount++;
				Thread.Sleep (delay);
			}
			return false;
		}

		public static bool CopyIfChangedOnce (string source, string destination)
		{
			if (HasFileChanged (source, destination)) {
				var directory = Path.GetDirectoryName (destination);
				if (!string.IsNullOrEmpty (directory))
					Directory.CreateDirectory (directory);

				if (!Directory.Exists (source)) {
					if (File.Exists (destination)) {
						SetWriteable (destination, checkExists: false);
						File.Delete (destination);
					}
					File.Copy (source, destination, overwrite: true);
					SetWriteable (destination, checkExists: false);
					File.SetLastWriteTimeUtc (destination, DateTime.UtcNow);
					return true;
				}
			}

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

				if (File.Exists (destination)) {
					SetWriteable (destination, checkExists: false);
					File.Delete (destination);
				}
				File.WriteAllBytes (destination, bytes);
				return true;
			}
			return false;
		}

		public static bool CopyIfStreamChanged (Stream stream, string destination)
		{
			int retryCount = 0;
			int attempts = GetFileWriteRetryAttempts ();
			int delay = GetFileWriteRetryDelay ();
			while (retryCount <= attempts) {
				try {
					return CopyIfStreamChangedOnce (stream, destination);
				} catch (Exception e) {
					switch (e) {
						case UnauthorizedAccessException:
						case IOException:
							int code = Marshal.GetHRForException (e);
							if ((code != ERROR_ACCESS_DENIED && code != ERROR_SHARING_VIOLATION) || retryCount >= attempts) {
								throw;
							};
							break;
						default:
							throw;
					} 
				}
				retryCount++;
				Thread.Sleep (delay);
			}
			return false;
		}

		public static bool CopyIfStreamChangedOnce (Stream stream, string destination)
		{
			if (HasStreamChanged (stream, destination)) {
				var directory = Path.GetDirectoryName (destination);
				if (!string.IsNullOrEmpty (directory))
					Directory.CreateDirectory (directory);

				if (File.Exists (destination)) {
					SetWriteable (destination, checkExists: false);
					File.Delete (destination);
				}
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
				return true;
			}

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

		public static bool ZipAny (string filename, Func<ZipEntry, bool> filter)
		{
			using (var zip = ReadZipFile (filename)) {
				return zip.Any (filter);
			}
		}

		public static bool ExtractAll (ZipArchive zip, string destination, Action<int, int> progressCallback = null, Func<string, string> modifyCallback = null,
			Func<string, bool> deleteCallback = null, Func<string, bool> skipCallback = null)
		{
			int i = 0;
			int total = (int)zip.EntryCount;
			bool updated = false;
			var files = new HashSet<string> ();
			var memoryStream = MemoryStreamPool.Shared.Rent ();
			try {
				foreach (var entry in zip) {
					progressCallback?.Invoke (i++, total);
					if (entry.IsDirectory)
						continue;
					if (entry.FullName.Contains ("/__MACOSX/") ||
							entry.FullName.EndsWith ("/__MACOSX", StringComparison.OrdinalIgnoreCase) ||
							string.Equals (entry.FullName, ".DS_Store", StringComparison.OrdinalIgnoreCase) ||
							entry.FullName.EndsWith ("/.DS_Store", StringComparison.OrdinalIgnoreCase))
						continue;
					if (skipCallback != null && skipCallback (entry.FullName))
						continue;
					var fullName = modifyCallback?.Invoke (entry.FullName) ?? entry.FullName;
					var outfile = Path.GetFullPath (Path.Combine (destination, fullName));
					files.Add (outfile);
					memoryStream.SetLength (0); //Reuse the stream
					entry.Extract (memoryStream);
					try {
						updated |= CopyIfStreamChanged (memoryStream, outfile);
					} catch (PathTooLongException) {
						throw new PathTooLongException ($"Could not extract \"{fullName}\" to \"{outfile}\". Path is too long.");
					}
				}
			} finally {
				MemoryStreamPool.Shared.Return (memoryStream);
			}
			if (Directory.Exists (destination)) {
				foreach (var file in Directory.GetFiles (destination, "*", SearchOption.AllDirectories)) {
					var outfile = Path.GetFullPath (file);
					if (outfile.Contains ("/__MACOSX/") ||
							outfile.EndsWith (".flat", StringComparison.OrdinalIgnoreCase) ||
							outfile.EndsWith ("files.cache", StringComparison.OrdinalIgnoreCase) ||
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

		/// <summary>
		/// Callback that can be used in combination with ExtractAll for extracting .aar files
		/// </summary>
		public static bool ShouldSkipEntryInAar (string entryFullName)
		{
			// AAR files may contain other jars not needed for compilation
			// See: https://developer.android.com/studio/projects/android-library.html#aar-contents
			if (!entryFullName.EndsWith (".jar", StringComparison.OrdinalIgnoreCase))
				return false;
			if (entryFullName == "classes.jar" ||
					entryFullName.StartsWith ("libs/", StringComparison.OrdinalIgnoreCase) ||
					entryFullName.StartsWith ("libs\\", StringComparison.OrdinalIgnoreCase))
				return false;
			// This could be `lint.jar` or `api.jar`, etc.
			return true;
		}

		public static string HashString (string s)
		{
			var bytes = Encoding.UTF8.GetBytes (s);
			return HashBytes (bytes);
		}

		public static string HashBytes (byte [] bytes)
		{
			using (HashAlgorithm hashAlg = new Crc64 ()) {
				byte [] hash = hashAlg.ComputeHash (bytes);
				return ToHexString (hash);
			}
		}

		public static string HashFile (string filename)
		{
			using (HashAlgorithm hashAlg = new Crc64 ()) {
				return HashFile (filename, hashAlg);
			}
		}

		public static string HashFile (string filename, HashAlgorithm hashAlg)
		{
			using (Stream file = new FileStream (filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				byte[] hash = hashAlg.ComputeHash (file);
				return ToHexString (hash);
			}
		}

		public static string HashStream (Stream stream)
		{
			stream.Position = 0;

			using (HashAlgorithm hashAlg = new Crc64 ()) {
				byte[] hash = hashAlg.ComputeHash (stream);
				return ToHexString (hash);
			}
		}

		public static string ToHexString (byte[] hash)
		{
			char [] array = new char [hash.Length * 2];
			for (int i = 0, j = 0; i < hash.Length; i += 1, j += 2) {
				byte b = hash [i];
				array [j] = GetHexValue (b / 16);
				array [j + 1] = GetHexValue (b % 16);
			}
			return new string (array);
		}

		static char GetHexValue (int i) => (char) (i < 10 ? i + 48 : i - 10 + 65);

		public static void DeleteFile (string filename, object log)
		{
			try {
				File.Delete (filename);
			} catch (Exception ex) {
				var helper = log as TaskLoggingHelper;
				helper.LogErrorFromException (ex);
			}
		}

		const uint ppdb_signature = 0x424a5342;

		public static bool IsPortablePdb (string filename)
		{
			try {
				using (var fs = new FileStream (filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					using (var br = new BinaryReader (fs)) {
						return br.ReadUInt32 () == ppdb_signature;
					}
				}
			}
			catch {
				return false;
			}
		}

		/// <summary>
		/// Open a file given its path and remove the 3 bytes UTF-8 BOM if there is one
		/// </summary>
		public static void CleanBOM (string filePath)
		{
			if (string.IsNullOrEmpty (filePath) || !File.Exists (filePath))
				return;

			string temp = null;
			try {
				using (var input = File.OpenRead (filePath)) {
					// Check if the file actually has a BOM
					for (int i = 0; i < Utf8Preamble.Length; i++) {
						var next = input.ReadByte ();
						if (next == -1)
							return;
						if (Utf8Preamble [i] != (byte) next)
							return;
					}

					temp = Path.GetTempFileName ();
					using (var stream = File.OpenWrite (temp))
						input.CopyTo (stream);
				}

				Files.SetWriteable (filePath);
				File.Delete (filePath);
				File.Copy (temp, filePath);
			} finally {
				if (temp != null) {
					File.Delete (temp);
				}
			}
		}
	}
}
