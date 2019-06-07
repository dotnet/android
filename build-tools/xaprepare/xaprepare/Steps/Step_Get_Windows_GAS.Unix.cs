using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_Get_Windows_GAS : Step
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

		public Step_Get_Windows_GAS ()
			: base ("Downloading NDK tools for Windows")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			string ndkVersion = BuildAndroidPlatforms.AndroidNdkVersion;

			var neededFiles = new HashSet<string> (StringComparer.OrdinalIgnoreCase) {
				$"android-ndk-r{ndkVersion}/toolchains/llvm/prebuilt/windows-x86_64/bin/i686-linux-android-as.exe",
				$"android-ndk-r{ndkVersion}/toolchains/llvm/prebuilt/windows-x86_64/bin/arm-linux-androideabi-as.exe",
				$"android-ndk-r{ndkVersion}/toolchains/llvm/prebuilt/windows-x86_64/bin/x86_64-linux-android-as.exe",
				$"android-ndk-r{ndkVersion}/toolchains/llvm/prebuilt/windows-x86_64/bin/aarch64-linux-android-as.exe",
			};

			string destinationDirectory = Path.Combine (Configurables.Paths.InstallMSBuildDir);
			int existingFiles = 0;
			foreach (string f in neededFiles) {
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
				Log.StatusLine ("All Windows GAS binaries already downloaded.");
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

		void StampFiles (HashSet<string> neededFiles, string destinationDirectory, string ndkVersion)
		{
			var now = DateTime.UtcNow;
			foreach (string file in neededFiles) {
				File.WriteAllText (GetStampFile (file, destinationDirectory, ndkVersion), now.ToString ());
			}
		}

		string GetStampFile (string file, string destinationDirectory, string ndkVersion)
		{
			return Path.Combine (destinationDirectory, $"{Path.GetFileName (file)}.{ndkVersion}");
		}

		async Task<bool> FetchFiles (HashSet<string> neededFiles, string destinationDirectory, Uri url)
		{
			Utilities.CreateDirectory (destinationDirectory);
			using (var httpClient = new HttpClient ()) {
				bool success;
				long size;

				Log.StatusLine ($"Accessing {url}");
				(success, size) = await GetFileSize (httpClient, url);
				if (!success)
					return false;

				Log.DebugLine ($"  File size: {size}");

				EOCD eocd;
				(success, eocd) = await GetEOCD (httpClient, url, size);
				if (!success) {
					Log.ErrorLine ("Failed to find the End of Central Directory record");
					return false;
				}

				if (eocd.DiskNumber != 0)
					throw new InvalidOperationException ("Multi-disk ZIP archives not supported");

				Log.DebugLine ($"  Central Directory offset: {eocd.CDOffset} (0x{eocd.CDOffset:x})");
				Log.DebugLine ($"  Central Directory size: {eocd.CDSize} (0x{eocd.CDSize})");
				Log.DebugLine ($"  Total Entries: {eocd.TotalEntries}");

				Stream cd;
				(success, cd) = await ReadCD (httpClient, url, eocd.CDOffset, eocd.CDSize, size);
				if (!success) {
					Log.ErrorLine ("Failed to read the Central Directory");
					return false;
				}

				Log.StatusLine ("Files:");
				if (!await ProcessEntries (httpClient, url, eocd, cd, neededFiles, destinationDirectory))
					return false;
			}

			return true;
		}

		async Task<bool> ProcessEntries (HttpClient httpClient, Uri url, EOCD eocd, Stream centralDirectory, HashSet<string> neededFiles, string destinationDirectory)
		{
			long foundEntries = 0;

			using (var br = new BinaryReader (centralDirectory)) {
				long nread = 0;
				long nentries = 1;

				while (nread < centralDirectory.Length && nentries <= eocd.TotalEntries) {
					(bool success, CDHeader cdh) = ReadCDHeader (br, centralDirectory.Length, ref nread);
					nentries++;
					if (!success) {
						Log.ErrorLine ($"Failed to read a Central Directory file header for entry {nentries}");
						return false;
					}

					if (!neededFiles.Contains (cdh.FileName))
						continue;

					if (!await ReadEntry (httpClient, url, cdh, br, destinationDirectory))
						return false;
					foundEntries++;
				}
			}

			if (foundEntries < neededFiles.Count) {
				Log.ErrorLine ($"Could not find all required binaries. Found {foundEntries} out of {neededFiles.Count}");
				return false;
			}

			return true;
		}

		async Task<bool> ReadEntry (HttpClient httpClient, Uri url, CDHeader cdh, BinaryReader br, string destinationDirectory)
		{
			Context context = Context.Instance;
			string destFilePath = Path.Combine (destinationDirectory, Path.GetFileName (cdh.FileName));
			string compressedFilePath = Path.Combine (destinationDirectory, $"{destFilePath}.deflated");
			Log.Status ($"  {context.Characters.Bullet} {Path.GetFileName (cdh.FileName)} ");
			Log.Status ($"{context.Characters.RightArrow}", ConsoleColor.Cyan);
			Log.StatusLine ($" {Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, destFilePath)}");
			Log.DebugLine ($" {cdh.FileName} (offset: {cdh.RelativeOffsetOfLocalHeader})");

			(bool success, Stream contentStream) = await ReadFileData (httpClient, url, cdh);
			if (!success) {
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
			(bool success, LFHeader lfh) = ReadLFHeader (fbr, contentStream.Length, ref fread);
			if (!success) {
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
			string outputFile = Path.Combine (Path.GetDirectoryName (compressedFilePath), Path.GetFileNameWithoutExtension (compressedFilePath));
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

		async Task<(bool success, Stream data)> ReadFileData (HttpClient httpClient, Uri url, CDHeader cdh)
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

		(bool success, LFHeader lfh) ReadLFHeader (BinaryReader cdr, long dataLength, ref long nread)
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

			byte[] bytes = ReadBytes (cdr, lfh.FileNameLength, dataLength, ref nread, out worked);
			if (!worked) {
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

		(bool success, CDHeader cdh) ReadCDHeader (BinaryReader cdr, long dataLength, ref long nread)
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

			byte[] bytes = ReadBytes (cdr, cdh.FileNameLength, dataLength, ref nread, out worked);
			if (!worked) {
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
				if (!worked) {
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

		byte[] ReadBytes (BinaryReader br, int neededBytes, long dataLength, ref long nread, out bool success)
		{
			success = false;
			if (dataLength - nread < neededBytes)
				return null;

			byte[] ret = br.ReadBytes (neededBytes);
			nread += neededBytes;

			success = true;
			return ret;
		}

		async Task<(bool success, Stream cd)> ReadCD (HttpClient httpClient, Uri url, uint cdOffset, uint cdSize, long fileSize)
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

		async Task<(bool success, EOCD eocd)> GetEOCD (HttpClient httpClient, Uri url, long fileSize)
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

	//
	// Taken from:
	//   https://github.com/force-net/Crc32.NET/blob/fbc1061b0cb53df2322d5aed33167a2e6335970b/Crc32.NET/SafeProxy.cs
	//
	// License: MIT
	//   https://github.com/force-net/Crc32.NET/blob/fbc1061b0cb53df2322d5aed33167a2e6335970b/LICENSE
	//
	class CRC32
	{
		const uint Poly = 0xedb88320u;

		readonly uint[] _table = new uint[16 * 256];

		internal CRC32 ()
		{
			Init (Poly);
		}

		protected void Init (uint poly)
		{
			var table = _table;
			for (uint i = 0; i < 256; i++) {
				uint res = i;
				for (int t = 0; t < 16; t++) {
					for (int k = 0; k < 8; k++) res = (res & 1) == 1 ? poly ^ (res >> 1) : (res >> 1);
					table[(t * 256) + i] = res;
				}
			}
		}

		public uint Append (uint crc, byte[] input, int offset, int length)
		{
			uint crcLocal = uint.MaxValue ^ crc;

			uint[] table = _table;
			while (length >= 16) {
				var a = table[(3 * 256) + input[offset + 12]]
					^ table[(2 * 256) + input[offset + 13]]
					^ table[(1 * 256) + input[offset + 14]]
					^ table[(0 * 256) + input[offset + 15]];

				var b = table[(7 * 256) + input[offset + 8]]
					^ table[(6 * 256) + input[offset + 9]]
					^ table[(5 * 256) + input[offset + 10]]
					^ table[(4 * 256) + input[offset + 11]];

				var c = table[(11 * 256) + input[offset + 4]]
					^ table[(10 * 256) + input[offset + 5]]
					^ table[(9 * 256) + input[offset + 6]]
					^ table[(8 * 256) + input[offset + 7]];

				var d = table[(15 * 256) + ((byte)crcLocal ^ input[offset])]
					^ table[(14 * 256) + ((byte)(crcLocal >> 8) ^ input[offset + 1])]
					^ table[(13 * 256) + ((byte)(crcLocal >> 16) ^ input[offset + 2])]
					^ table[(12 * 256) + ((crcLocal >> 24) ^ input[offset + 3])];

				crcLocal = d ^ c ^ b ^ a;
				offset += 16;
				length -= 16;
			}

			while (--length >= 0)
				crcLocal = table[(byte)(crcLocal ^ input[offset++])] ^ crcLocal >> 8;

			return crcLocal ^ uint.MaxValue;
		}
	}
}
