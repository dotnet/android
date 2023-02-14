using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using Force.Crc32;

namespace Xamarin.Android.Prepare
{
	class Step_Get_Windows_Binutils : Step
	{
		const int EndFileChunkSize   = 65535 + 22; // Maximum comment size + EOCD size
		const uint EOCDSignature     = 0x06054b50;
		const uint CDHeaderSignature = 0x02014b50;
		const uint LFHeaderSignature = 0x04034b50;

		class EOCD
		{
			public uint Signature; // Signature (0x06054b50)
			public ushort DiskNumber; // number of this disk
			public ushort CDStartDisk; // number of the disk with the start of the central directory
			public ushort TotalEntriesThisDisk; // total number of entries in the central directory on this disk
			public ushort TotalEntries; // total number of entries in the central directory
			public uint CDSize; // size of the central directory
			public uint CDOffset; // offset of start of central     directory with respect to the starting disk number
			public ushort CommentLength; // .ZIP file comment length
		};
#nullable disable
		class CDHeader
		{
			public uint Signature; // 0x02014b50
			public ushort VersionMadeBy;
			public ushort VersionNeededToExtract;
			public ushort GeneralPurposeBitFlag;
			public ushort CompressionMethod;
			public ushort LastModFileTime;
			public ushort LastModFileDate;
			public uint CRC32;
			public uint CompressedSize;
			public uint UncompressedSize;
			public ushort FileNameLength;
			public ushort ExtraFieldLength;
			public ushort FileCommentLength;
			public ushort DiskNumberStart;
			public ushort InternalFileAttributes;
			public uint ExternalFileAttributes;
			public uint RelativeOffsetOfLocalHeader;
			public string FileName;
			public byte[] ExtraField;
			public string FileComment;
		};

		class LFHeader
		{
			public uint Signature; // 0x04034b50
			public ushort VersionNeededToExtract;
			public ushort GeneralPurposeBitFlag;
			public ushort CompressionMethod;
			public ushort LastModFileTime;
			public ushort LastModFileDate;
			public uint CRC32;
			public uint CompressedSize;
			public uint UncompressedSize;
			public ushort FileNameLength;
			public ushort ExtraFieldLength;
			public string FileName;
			public byte[] ExtraField;
		};
#nullable enable

		public Step_Get_Windows_Binutils ()
			: base ("Downloading NDK tools for Windows")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			string ndkVersion = BuildAndroidPlatforms.AndroidNdkVersion;
			string baseArchivePath = $"android-ndk-r{ndkVersion}/toolchains/llvm/prebuilt/windows-x86_64/bin";

			var neededFiles = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
				{ $"{baseArchivePath}/libwinpthread-1.dll", String.Empty },
			};

			foreach (var kvp in Configurables.Defaults.AndroidToolchainPrefixes) {
				string archPrefix = kvp.Value;
				foreach (NDKTool ndkTool in Configurables.Defaults.NDKTools) {
					string sourcePath = $"{baseArchivePath}/{archPrefix}-{ndkTool.Name}.exe";
					string destPath;

					if (ndkTool.DestinationName.Length == 0) {
						destPath = String.Empty;
					} else {
						destPath = $"{baseArchivePath}/{archPrefix}-{ndkTool.DestinationName}.exe";
					}

					neededFiles [sourcePath] = destPath;
				}
			}

			string destinationDirectory = Configurables.Paths.WindowsBinutilsInstallDir;
			int existingFiles = 0;
			foreach (var kvp in neededFiles) {
				string f = GetDestinationFile (kvp);
				string file = Path.Combine (destinationDirectory, Path.GetFileName (f));
				string stampFile = GetStampFile (f, destinationDirectory, ndkVersion);
				if (File.Exists (file)) {
					Log.DebugLine ($"{file} exists");
					if (File.Exists (stampFile)) {
						existingFiles++;
					} else {
						Log.DebugLine ($"Stamp file {stampFile} does not exist, will need to download the executable again");
					}
				}
			}

			if (existingFiles == neededFiles.Count) {
				Log.StatusLine ("All Windows binutils binaries already downloaded.");
				return true;
			}

			bool result = await FetchFiles (
				neededFiles,
				destinationDirectory,
				new Uri (AndroidToolchain.AndroidUri, $"android-ndk-r{ndkVersion}-windows-x86_64.zip")
			);

			if (!result)
				return false;

			StampFiles (neededFiles, destinationDirectory, ndkVersion);
			return true;
		}

		string GetDestinationFile (KeyValuePair<string, string> kvp)
		{
			return kvp.Value.Length == 0 ? kvp.Key : kvp.Value;
		}

		void StampFiles (Dictionary<string, string> neededFiles, string destinationDirectory, string ndkVersion)
		{
			var now = DateTime.UtcNow;
			foreach (var kvp in neededFiles) {
				string file = GetDestinationFile (kvp);
				File.WriteAllText (GetStampFile (file, destinationDirectory, ndkVersion), now.ToString ());
			}
		}

		string GetStampFile (string file, string destinationDirectory, string ndkVersion)
		{
			return Path.Combine (destinationDirectory, $"{Path.GetFileName (file)}.{ndkVersion}");
		}

		async Task<bool> FetchFiles (Dictionary<string, string> neededFiles, string destinationDirectory, Uri url)
		{
			Utilities.CreateDirectory (destinationDirectory);
			using (HttpClient httpClient = Utilities.CreateHttpClient ()) {
				bool success;
				long size;

				Log.StatusLine ($"Accessing {url}");
				(success, size) = await GetFileSize (httpClient, url);
				if (!success)
					return false;

				Log.DebugLine ($"  File size: {size}");

				EOCD? eocd;
				(success, eocd) = await GetEOCD (httpClient, url, size);
				if (!success || eocd == null) {
					Log.ErrorLine ("Failed to find the End of Central Directory record");
					return false;
				}

				if (eocd.DiskNumber != 0)
					throw new InvalidOperationException ("Multi-disk ZIP archives not supported");

				Log.DebugLine ($"  Central Directory offset: {eocd.CDOffset} (0x{eocd.CDOffset:x})");
				Log.DebugLine ($"  Central Directory size: {eocd.CDSize} (0x{eocd.CDSize})");
				Log.DebugLine ($"  Total Entries: {eocd.TotalEntries}");

				Stream? cd;
				(success, cd) = await ReadCD (httpClient, url, eocd.CDOffset, eocd.CDSize, size);
				if (!success || cd == null) {
					Log.ErrorLine ("Failed to read the Central Directory");
					return false;
				}

				Log.StatusLine ("Files:");
				if (!await ProcessEntries (httpClient, url, eocd, cd, neededFiles, destinationDirectory))
					return false;
			}

			return true;
		}

		async Task<bool> ProcessEntries (HttpClient httpClient, Uri url, EOCD eocd, Stream centralDirectory, Dictionary<string, string> neededFiles, string destinationDirectory)
		{
			long foundEntries = 0;
			var foundFiles = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

			using (var br = new BinaryReader (centralDirectory)) {
				long nread = 0;
				long nentries = 1;

				while (nread < centralDirectory.Length && nentries <= eocd.TotalEntries) {
					(bool success, CDHeader? cdh) = ReadCDHeader (br, centralDirectory.Length, ref nread);
					nentries++;
					if (!success || cdh == null) {
						Log.ErrorLine ($"Failed to read a Central Directory file header for entry {nentries}");
						return false;
					}

					if (!neededFiles.TryGetValue (cdh.FileName, out string? destinationFileName))
						continue;

					foundFiles.Add (cdh.FileName);

					if (!await ReadEntry (httpClient, url, cdh, br, destinationDirectory, destinationFileName))
						return false;
					foundEntries++;
				}
			}

			if (foundEntries < neededFiles.Count) {
				Log.ErrorLine ();
				Log.ErrorLine ($"Could not find all required binaries. Found {foundEntries} out of {neededFiles.Count}, the missing files are:");
				foreach (string file in neededFiles.Keys) {
					if (foundFiles.Contains (file)) {
						continue;
					}
					Log.StatusLine ($"  {Context.Instance.Characters.Bullet} {file} ");
				}

				return false;
			}

			return true;
		}

		async Task<bool> ReadEntry (HttpClient httpClient, Uri url, CDHeader cdh, BinaryReader br, string destinationDirectory, string destinationFileName)
		{
			Context context = Context.Instance;
			string destFileName = Path.GetFileName (destinationFileName.Length == 0 ? cdh.FileName : destinationFileName);
			string destFilePath = Path.Combine (destinationDirectory, destFileName);
			string compressedFilePath = Path.Combine (destinationDirectory, $"{destFilePath}.deflated");
			Log.Status ($"  {context.Characters.Bullet} {Path.GetFileName (cdh.FileName)} ");
			Log.Status ($"{context.Characters.RightArrow}", ConsoleColor.Cyan);
			Log.StatusLine ($" {Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, destFilePath)}");
			Log.DebugLine ($" {cdh.FileName} (offset: {cdh.RelativeOffsetOfLocalHeader})");

			(bool success, Stream? contentStream) = await ReadFileData (httpClient, url, cdh);
			if (!success || contentStream == null) {
				Log.ErrorLine ("Failed to read file data");
				return false;
			}

			using (var destFile = new BinaryWriter (File.OpenWrite (compressedFilePath))) {
				using (var fbr = new BinaryReader (contentStream)) {
					if (!await DownloadAndExtract (fbr, contentStream, destFile, compressedFilePath))
						return CleanupAndReturn (false);
				}
			}

			return CleanupAndReturn (true);

			bool CleanupAndReturn (bool retval)
			{
				if (File.Exists (compressedFilePath))
					File.Delete (compressedFilePath);

				return retval;
			}
		}

		async Task<bool> DownloadAndExtract (BinaryReader fbr, Stream contentStream, BinaryWriter destFile, string destFileName)
		{
			long fread = 0;
			(bool success, LFHeader? lfh) = ReadLFHeader (fbr, contentStream.Length, ref fread);
			if (!success || lfh == null) {
				Log.ErrorLine ("Failed to read local file header");
				return false;
			}

			uint dread = 0;
			var buffer = new byte [8192];
			while (fread <= contentStream.Length && dread < lfh.CompressedSize) {
				uint toRead;
				if (lfh.CompressedSize - dread < buffer.Length)
					toRead = lfh.CompressedSize - dread;
				else
					toRead = (uint)buffer.Length;

				int bread = await contentStream.ReadAsync (buffer, 0, (int)toRead);
				if (bread == 0)
					break;
				destFile.Write (buffer, 0, bread);
				fread += bread;
				dread += (uint)bread;
			}

			destFile.Flush ();
			destFile.Close ();
			destFile.Dispose ();
			Extract (destFileName, lfh.CRC32);

			if (dread != lfh.CompressedSize)
				Log.ErrorLine ($"  Invalid data size: expected {lfh.CompressedSize} bytes, read {dread} bytes");

			return true;
		}

		void Extract (string compressedFilePath, uint crc32FromHeader)
		{
			if (String.IsNullOrEmpty (compressedFilePath)) {
				throw new ArgumentException ("must not be null or empty", nameof (compressedFilePath));
			}

			string outputFile = Path.Combine (Path.GetDirectoryName (compressedFilePath) ?? String.Empty, Path.GetFileNameWithoutExtension (compressedFilePath) ?? String.Empty);
			using (var fs = File.OpenRead (compressedFilePath)) {
				using (var dfs = File.OpenWrite (outputFile)) {
					Extract (fs, dfs, crc32FromHeader);
				}
			}
		}

		void Extract (Stream src, Stream dest, uint crc32FromHeader)
		{
			uint fileCRC = 0;
			int fread = 0;
			var crc32 = new CRC32 ();
			var buffer = new byte [8192];

			using (var iis = new DeflateStream (src, CompressionMode.Decompress)) {
				while (true) {
					fread = iis.Read (buffer, 0, buffer.Length);
					if (fread <= 0)
						break;

					fileCRC = crc32.Append (fileCRC, buffer, 0, fread);
					dest.Write (buffer, 0, fread);
				}
				dest.Flush ();
			}

			if (fileCRC != crc32FromHeader)
				Log.ErrorLine ($"  Invalid CRC32: expected 0x{crc32FromHeader:x}, got 0x{fileCRC:x}");
		}

		async Task<(bool success, Stream? data)> ReadFileData (HttpClient httpClient, Uri url, CDHeader cdh)
		{
			long fileOffset = cdh.RelativeOffsetOfLocalHeader;
			long dataSize =
				cdh.CompressedSize +
				30 + // local file header size, the static portion
				cdh.FileName.Length + // They're the same in both haders
				cdh.ExtraFieldLength + // This may differ between headers...
				16384; // ...so we add some extra padding

			var req = new HttpRequestMessage (HttpMethod.Get, url);
			req.Headers.ConnectionClose = true;
			req.Headers.Range = new RangeHeaderValue (fileOffset, fileOffset + dataSize);

			HttpResponseMessage resp = await httpClient.SendAsync (req).ConfigureAwait (false);

			if (!resp.IsSuccessStatusCode) {
				Log.ErrorLine ($"Failed to read file data: HTTP error {resp.StatusCode}");
				return (false, null);
			}

			Stream s = await resp.Content.ReadAsStreamAsync ().ConfigureAwait (false);
			if (s.Length < dataSize) {
				Log.ErrorLine ($"Failed to read file data: invalid data length ({s.Length} < {dataSize})");
				s.Dispose ();
				return (false, null);
			}

			return (true, s);
		}

		(bool success, LFHeader? lfh) ReadLFHeader (BinaryReader cdr, long dataLength, ref long nread)
		{
			var lfh = new LFHeader ();

			bool worked;
			lfh.Signature = ReadUInt (cdr, dataLength, ref nread, out worked);
			if (!worked || lfh.Signature != LFHeaderSignature) {
				Log.ErrorLine ($"Invalid signature ({lfh.Signature:x} != {LFHeaderSignature:x})");
				goto failed;
			}

			lfh.VersionNeededToExtract = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			lfh.GeneralPurposeBitFlag = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			lfh.CompressionMethod = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			lfh.LastModFileTime = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			lfh.LastModFileDate = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			lfh.CRC32 = ReadUInt (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			lfh.CompressedSize = ReadUInt (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			lfh.UncompressedSize = ReadUInt (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			lfh.FileNameLength = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			lfh.ExtraFieldLength = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			byte[]? bytes = ReadBytes (cdr, lfh.FileNameLength, dataLength, ref nread, out worked);
			if (!worked || bytes == null) {
				goto failed;
			}

			lfh.FileName = Encoding.ASCII.GetString (bytes);
			if (!worked) {
				goto failed;
			}

			if (lfh.ExtraFieldLength > 0) {
				lfh.ExtraField = ReadBytes (cdr, lfh.ExtraFieldLength, dataLength, ref nread, out worked);
				if (!worked) {
					goto failed;
				}
			}

			return (true, lfh);

		  failed:
			return (false, null);
		}

		(bool success, CDHeader? cdh) ReadCDHeader (BinaryReader cdr, long dataLength, ref long nread)
		{
			var cdh = new CDHeader ();

			bool worked;
			cdh.Signature = ReadUInt (cdr, dataLength, ref nread, out worked);
			if (!worked || cdh.Signature != CDHeaderSignature) {
				Log.ErrorLine ($"Invalid signature ({cdh.Signature:x} != {CDHeaderSignature:x})");
				goto failed;
			}

			cdh.VersionMadeBy = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.VersionNeededToExtract = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.GeneralPurposeBitFlag = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.CompressionMethod = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.LastModFileTime = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.LastModFileDate = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.CRC32 = ReadUInt (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.CompressedSize = ReadUInt (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.UncompressedSize = ReadUInt (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.FileNameLength = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.ExtraFieldLength = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.FileCommentLength = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.DiskNumberStart = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.InternalFileAttributes = ReadUShort (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.ExternalFileAttributes = ReadUInt (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			cdh.RelativeOffsetOfLocalHeader = ReadUInt (cdr, dataLength, ref nread, out worked);
			if (!worked) {
				goto failed;
			}

			byte[]? bytes = ReadBytes (cdr, cdh.FileNameLength, dataLength, ref nread, out worked);
			if (!worked || bytes == null) {
				goto failed;
			}

			cdh.FileName = Encoding.ASCII.GetString (bytes);
			if (!worked) {
				goto failed;
			}

			if (cdh.ExtraFieldLength > 0) {
				cdh.ExtraField = ReadBytes (cdr, cdh.ExtraFieldLength, dataLength, ref nread, out worked);
				if (!worked) {
					goto failed;
				}
			}

			if (cdh.FileCommentLength > 0) {
				bytes = ReadBytes (cdr, cdh.FileCommentLength, dataLength, ref nread, out worked);
				if (!worked || bytes == null) {
					goto failed;
				}
				cdh.FileComment = Encoding.ASCII.GetString (bytes);
			}

			return (true, cdh);

		  failed:
			return (false, null);
		}

		ushort ReadUShort (BinaryReader br, long dataLength, ref long nread, out bool success)
		{
			success = false;
			if (dataLength - nread < 2)
				return 0;

			ushort ret = br.ReadUInt16 ();
			nread += 2;

			success = true;
			return ret;
		}

		uint ReadUInt (BinaryReader br, long dataLength, ref long nread, out bool success)
		{
			success = false;
			if (dataLength - nread < 4)
				return 0;

			uint ret = br.ReadUInt32 ();
			nread += 4;

			success = true;
			return ret;
		}

		byte[]? ReadBytes (BinaryReader br, int neededBytes, long dataLength, ref long nread, out bool success)
		{
			success = false;
			if (dataLength - nread < neededBytes)
				return null;

			byte[] ret = br.ReadBytes (neededBytes);
			nread += neededBytes;

			success = true;
			return ret;
		}

		async Task<(bool success, Stream? cd)> ReadCD (HttpClient httpClient, Uri url, uint cdOffset, uint cdSize, long fileSize)
		{
			long fileOffset = cdOffset;
			var req = new HttpRequestMessage (HttpMethod.Get, url);
			req.Headers.ConnectionClose = true;
			req.Headers.Range = new RangeHeaderValue (fileOffset, fileOffset + cdSize);

			HttpResponseMessage resp = await httpClient.SendAsync (req).ConfigureAwait (false);
			if (!resp.IsSuccessStatusCode) {
				Log.ErrorLine ($"Failed to read Central Directory: HTTP error {resp.StatusCode}");
				return (false, null);
			}

			Stream s = await resp.Content.ReadAsStreamAsync ().ConfigureAwait (false);
			if (s.Length < cdSize) {
				Log.ErrorLine ($"Failed to read Central Directory: invalid data length ({s.Length} < {cdSize})");
				s.Dispose ();
				return (false, null);
			}

			return (true, s);
		}

		async Task<(bool success, EOCD? eocd)> GetEOCD (HttpClient httpClient, Uri url, long fileSize)
		{
			long fileOffset = fileSize - EndFileChunkSize;
			var req = new HttpRequestMessage (HttpMethod.Get, url);
			req.Headers.ConnectionClose = true;
			req.Headers.Range = new RangeHeaderValue (fileOffset, fileSize);

			HttpResponseMessage resp = await httpClient.SendAsync (req).ConfigureAwait (false);
			if (!resp.IsSuccessStatusCode)
				return (false, null);

			using (var eocdStream = await resp.Content.ReadAsStreamAsync ().ConfigureAwait (false)) {
				using (var sr = new BinaryReader (eocdStream)) {
					byte[] expected = {0x50, 0x4b, 0x05, 0x06};
					int expectedPos = 0;

					for (int i = 0; i < eocdStream.Length; i++) {
						byte b = sr.ReadByte ();
						if (b != expected [expectedPos]) {
							expectedPos = 0;
							continue;
						}

						if (expectedPos == expected.Length - 1) {
							// We've found the signature
							var eocd = new EOCD ();
							eocd.Signature = 0x06054b50;
							eocd.DiskNumber = sr.ReadUInt16 ();
							eocd.CDStartDisk = sr.ReadUInt16 ();
							eocd.TotalEntriesThisDisk = sr.ReadUInt16 ();
							eocd.TotalEntries = sr.ReadUInt16 ();
							eocd.CDSize = sr.ReadUInt32 ();
							eocd.CDOffset = sr.ReadUInt32 ();
							eocd.CommentLength = sr.ReadUInt16 ();

							return (true, eocd);
						}

						expectedPos++;
						if (expectedPos >= expected.Length)
							expectedPos = 0;
					}
				}
			}

			return (false, null);
		}

		async Task<(bool success, long size)> GetFileSize (HttpClient httpClient, Uri url)
		{
			var req = new HttpRequestMessage (HttpMethod.Head, url);
			req.Headers.ConnectionClose = true;

			HttpResponseMessage resp = await httpClient.SendAsync (req).ConfigureAwait (false);
			if (!resp.IsSuccessStatusCode || !resp.Content.Headers.ContentLength.HasValue)
				return (false, 0);

			return (true, resp.Content.Headers.ContentLength.Value);
		}
	}
}
