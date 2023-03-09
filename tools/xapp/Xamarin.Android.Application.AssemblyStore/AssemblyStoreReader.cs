using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Mono.Cecil;
using K4os.Compression.LZ4;

namespace Xamarin.Android.AssemblyStore
{
	class AssemblyStoreReader
	{
		// These two constants must be identical to the native ones in src/monodroid/jni/xamarin-app.hh
		public const uint ASSEMBLY_STORE_MAGIC = 0x41424158; // 'XABA', little-endian
		const uint ASSEMBLY_STORE_FORMAT_VERSION = 1; // The highest format version this reader understands

		static readonly byte[] CompressedDataMagic = { 0x58, 0x41, 0x4c, 0x5a }; // 'XALZ', little-endian

		readonly ArrayPool<byte> bytePool = ArrayPool<byte>.Shared;
		MemoryStream? storeData;

		public uint Version                                              { get; private set; }
		public uint LocalEntryCount                                      { get; private set; }
		public uint GlobalEntryCount                                     { get; private set; }
		public uint StoreID                                              { get; private set; }
		public List<AssemblyStoreAssembly> Assemblies                    { get; }
		public List<AssemblyStoreHashEntry> GlobalIndex32                { get; } = new List<AssemblyStoreHashEntry> ();
		public List<AssemblyStoreHashEntry> GlobalIndex64                { get; } = new List<AssemblyStoreHashEntry> ();
		public string Arch                                               { get; }

		public bool HasGlobalIndex => StoreID == 0;
		public bool IsArchSpecific => StoreID > 0;

		public AssemblyStoreReader (Stream store, string? inputPath = null, bool keepStoreInMemory = false)
		{
			store.Seek (0, SeekOrigin.Begin);
			if (keepStoreInMemory) {
				storeData = new MemoryStream ();
				store.CopyTo (storeData);
				storeData.Flush ();
				store.Seek (0, SeekOrigin.Begin);
			}

			using (var reader = new BinaryReader (store, Encoding.UTF8, leaveOpen: true)) {
				ReadHeader (reader);

				Assemblies = new List<AssemblyStoreAssembly> ();
				ReadLocalEntries (reader, Assemblies);
				if (HasGlobalIndex) {
					ReadGlobalIndex (reader, GlobalIndex32, GlobalIndex64);
				}
			}

			Arch = GetBlobArchitecture (inputPath);
		}

		public void EnsureAssemblyNames (AssemblyStoreManifestReader? manifest = null)
		{
			foreach (AssemblyStoreAssembly asm in Assemblies) {
				if (!String.IsNullOrEmpty (asm.Name)) {
					continue;
				}

				DetermineName (asm, manifest);
			}
		}

		void DetermineName (AssemblyStoreAssembly asm, AssemblyStoreManifestReader? manifest)
		{
			if (manifest != null && manifest.EntriesByHash64.TryGetValue (asm.Hash64, out AssemblyStoreManifestEntry entry)) {
				asm.Name = entry.Name;
				return;
			}

			// Going the slow way...
			EnsureStoreDataAvailable ();

			using var data = new MemoryStream ();
			if (!SaveDataToStream (data, asm.DataOffset, asm.DataSize, decompress: true)) {
				asm.Name = MakeFullName ($"{asm.Hash32:x}_{asm.Hash64:x}");
				return;
			}
			data.Seek (0, SeekOrigin.Begin);

			AssemblyDefinition asmdef = AssemblyDefinition.ReadAssembly (data);
			var name = new StringBuilder ();

			if (!String.IsNullOrEmpty (asmdef.Name.Culture)) {
				name.Append (asmdef.Name.Culture);
				name.Append ('/');
			}
			name.Append (asmdef.Name.Name);
			asm.Name = MakeFullName (name.ToString ());

			string MakeFullName (string baseName)
			{
				return $"{baseName}.dll";
			}
		}

		public static string GetBlobArchitecture (string? fullBlobPath)
		{
			if (String.IsNullOrEmpty (fullBlobPath)) {
				return String.Empty;
			}

			string? fileName = Path.GetFileName (fullBlobPath);
			if (String.IsNullOrEmpty (fileName)) {
				return String.Empty;
			}

			// Detect arch from the name: assemblies.ARCH.blob
			string[] parts = fileName.Split ('.');
			if (parts.Length != 3) {
				return String.Empty;
			}

			if (String.Compare ("assemblies", parts[0], StringComparison.Ordinal) != 0 ||
			    String.Compare ("blob", parts[2], StringComparison.Ordinal) != 0) {
				return String.Empty;
			}

			if (String.Compare ("x86_64", parts[1], StringComparison.Ordinal) == 0) {
				return parts[1];
			}

			return parts[1].Replace ("_", "-");
		}

		internal void ExtractAssemblyImage (AssemblyStoreAssembly assembly, string outputFilePath, bool decompress)
		{
			SaveDataToFile (outputFilePath, assembly.DataOffset, assembly.DataSize, decompress);
		}

		internal void ExtractAssemblyImage (AssemblyStoreAssembly assembly, Stream output, bool decompress)
		{
			SaveDataToStream (output, assembly.DataOffset, assembly.DataSize, decompress);
		}

		internal void ExtractAssemblyDebugData (AssemblyStoreAssembly assembly, string outputFilePath)
		{
			if (assembly.DebugDataOffset == 0 || assembly.DebugDataSize == 0) {
				return;
			}
			SaveDataToFile (outputFilePath, assembly.DebugDataOffset, assembly.DebugDataSize, decompress: false);
		}

		internal void ExtractAssemblyDebugData (AssemblyStoreAssembly assembly, Stream output)
		{
			if (assembly.DebugDataOffset == 0 || assembly.DebugDataSize == 0) {
				return;
			}
			SaveDataToStream (output, assembly.DebugDataOffset, assembly.DebugDataSize, decompress: false);
		}

		internal void ExtractAssemblyConfig (AssemblyStoreAssembly assembly, string outputFilePath)
		{
			if (assembly.ConfigDataOffset == 0 || assembly.ConfigDataSize == 0) {
				return;
			}

			SaveDataToFile (outputFilePath, assembly.ConfigDataOffset, assembly.ConfigDataSize, decompress: false);
		}

		internal void ExtractAssemblyConfig (AssemblyStoreAssembly assembly, Stream output)
		{
			if (assembly.ConfigDataOffset == 0 || assembly.ConfigDataSize == 0) {
				return;
			}
			SaveDataToStream (output, assembly.ConfigDataOffset, assembly.ConfigDataSize, decompress: false);
		}

		void SaveDataToFile (string outputFilePath, uint offset, uint size, bool decompress)
		{
			EnsureStoreDataAvailable ();
			using (var fs = File.Open (outputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)) {
				SaveDataToStream (fs, offset, size, decompress);
			}
		}

		bool SaveDataToStream (Stream output, uint offset, uint size, bool decompress)
		{
			EnsureStoreDataAvailable ();

			if (IsCompressed (storeData!, offset)) {
				if (!Decompress (output, offset, size)) {
					return false;
				}
			} else {
				storeData!.Seek (offset, SeekOrigin.Begin);
				byte[] buf = bytePool.Rent (16384);
				int nread;
				long toRead = size;
				while (toRead > 0 && (nread = storeData.Read (buf, 0, buf.Length)) > 0) {
					if (nread > toRead) {
						nread = (int)toRead;
					}

					output.Write (buf, 0, nread);
					toRead -= nread;
				}
				output.Flush ();
				bytePool.Return (buf);
			}

			return true;
		}

		internal bool IsCompressed (uint offset)
		{
			EnsureStoreDataAvailable ();
			return IsCompressed (storeData!, offset);
		}

		bool IsCompressed (Stream stream, uint offset)
		{
			stream.Seek (offset, SeekOrigin.Begin);
			long pos = stream.Position;

			try {
				foreach (byte b in CompressedDataMagic) {
					int rval = stream.ReadByte ();
					if (rval == -1) {
						return false;
					}

					var r = (byte)rval;
					if (r != b) {
						return false;
					}
				}
			} finally {
				stream.Seek (pos, SeekOrigin.Begin);
			}

			return true;
		}

		bool Decompress (Stream output, uint offset, uint size)
		{
			// For the compressed file header format, see src/monodroid/jni/xamarin-app.hh; struct CompressedAssemblyHeader
			using var reader = new BinaryReader (storeData!, Encoding.UTF8, leaveOpen: true);

			// magic
			reader.ReadUInt32 ();

			// descriptor_index
			reader.ReadUInt32 ();

			int uncompressedLength = (int)reader.ReadUInt32 ();
			int dataSize = (int)size - 12; // subtract the compression header size
			byte[]  inputBytes = bytePool.Rent (dataSize);
			byte[]? outputBytes = null;

			try {
				int nread = reader.Read (inputBytes, 0, dataSize);
				if (nread < dataSize) {
					Console.WriteLine ($"Read less data from stream ({nread} than expected ({inputBytes.Length})");
					return false;
				}

				outputBytes = bytePool.Rent (uncompressedLength);
				int decoded = LZ4Codec.Decode (inputBytes, 0, dataSize, outputBytes, 0, uncompressedLength);
				if (decoded < uncompressedLength) {
					Console.WriteLine ($"LZ4 Decoded less bytes ({decoded}) than expected ({uncompressedLength})");
					return false;
				}

				output.Write (outputBytes, 0, decoded);
				output.Flush ();
				output.Seek (0, SeekOrigin.Begin);
			} finally {
				bytePool.Return (inputBytes);
				if (outputBytes != null) {
					bytePool.Return (outputBytes);
				}
			}

			return true;
		}

		void EnsureStoreDataAvailable ()
		{
			if (storeData != null) {
				return;
			}

			throw new InvalidOperationException ("Store data not available. AssemblyStore/AssemblyStoreExplorer must be instantiated with the `keepStoreInMemory` argument set to `true`");
		}

		public bool HasIdenticalContent (AssemblyStoreReader other)
		{
			return
				other.Version == Version &&
				other.LocalEntryCount == LocalEntryCount &&
				other.GlobalEntryCount == GlobalEntryCount &&
				other.StoreID == StoreID &&
				other.Assemblies.Count == Assemblies.Count &&
				other.GlobalIndex32.Count == GlobalIndex32.Count &&
				other.GlobalIndex64.Count == GlobalIndex64.Count;
		}

		void ReadHeader (BinaryReader reader)
		{
			uint magic = reader.ReadUInt32 ();
			if (magic != ASSEMBLY_STORE_MAGIC) {
				throw new InvalidOperationException ("Invalid header magic number");
			}

			Version = reader.ReadUInt32 ();
			if (Version == 0) {
				throw new InvalidOperationException ("Invalid version number: 0");
			}

			if (Version > ASSEMBLY_STORE_FORMAT_VERSION) {
				throw new InvalidOperationException ($"Store format version {Version} is higher than the one understood by this reader, {ASSEMBLY_STORE_FORMAT_VERSION}");
			}

			LocalEntryCount = reader.ReadUInt32 ();
			GlobalEntryCount = reader.ReadUInt32 ();
			StoreID = reader.ReadUInt32 ();
		}

		void ReadLocalEntries (BinaryReader reader, List<AssemblyStoreAssembly> assemblies)
		{
			for (uint i = 0; i < LocalEntryCount; i++) {
				assemblies.Add (new AssemblyStoreAssembly (reader, this));
			}
		}

		void ReadGlobalIndex (BinaryReader reader, List<AssemblyStoreHashEntry> index32, List<AssemblyStoreHashEntry> index64)
		{
			ReadIndex (true, index32);
			ReadIndex (false, index64);

			void ReadIndex (bool is32Bit, List<AssemblyStoreHashEntry> index) {
				for (uint i = 0; i < GlobalEntryCount; i++) {
					index.Add (new AssemblyStoreHashEntry (reader, is32Bit));
				}
			}
		}
	}
}
