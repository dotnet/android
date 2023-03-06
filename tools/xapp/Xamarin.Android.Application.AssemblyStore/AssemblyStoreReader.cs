using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xamarin.Android.AssemblyStore
{
	class AssemblyStoreReader
	{
		// These two constants must be identical to the native ones in src/monodroid/jni/xamarin-app.hh
		const uint ASSEMBLY_STORE_MAGIC = 0x41424158; // 'XABA', little-endian
		const uint ASSEMBLY_STORE_FORMAT_VERSION = 1; // The highest format version this reader understands

		MemoryStream? storeData;

		public uint Version                               { get; private set; }
		public uint LocalEntryCount                       { get; private set; }
		public uint GlobalEntryCount                      { get; private set; }
		public uint StoreID                               { get; private set; }
		public List<AssemblyStoreAssembly> Assemblies     { get; }
		public List<AssemblyStoreHashEntry> GlobalIndex32 { get; } = new List<AssemblyStoreHashEntry> ();
		public List<AssemblyStoreHashEntry> GlobalIndex64 { get; } = new List<AssemblyStoreHashEntry> ();
		public string Arch                                { get; }

		public bool HasGlobalIndex => StoreID == 0;

		public AssemblyStoreReader (Stream store, string? arch = null, bool keepStoreInMemory = false)
		{
			Arch = arch ?? String.Empty;

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
		}

		internal void ExtractAssemblyImage (AssemblyStoreAssembly assembly, string outputFilePath)
		{
			SaveDataToFile (outputFilePath, assembly.DataOffset, assembly.DataSize);
		}

		internal void ExtractAssemblyImage (AssemblyStoreAssembly assembly, Stream output)
		{
			SaveDataToStream (output, assembly.DataOffset, assembly.DataSize);
		}

		internal void ExtractAssemblyDebugData (AssemblyStoreAssembly assembly, string outputFilePath)
		{
			if (assembly.DebugDataOffset == 0 || assembly.DebugDataSize == 0) {
				return;
			}
			SaveDataToFile (outputFilePath, assembly.DebugDataOffset, assembly.DebugDataSize);
		}

		internal void ExtractAssemblyDebugData (AssemblyStoreAssembly assembly, Stream output)
		{
			if (assembly.DebugDataOffset == 0 || assembly.DebugDataSize == 0) {
				return;
			}
			SaveDataToStream (output, assembly.DebugDataOffset, assembly.DebugDataSize);
		}

		internal void ExtractAssemblyConfig (AssemblyStoreAssembly assembly, string outputFilePath)
		{
			if (assembly.ConfigDataOffset == 0 || assembly.ConfigDataSize == 0) {
				return;
			}

			SaveDataToFile (outputFilePath, assembly.ConfigDataOffset, assembly.ConfigDataSize);
		}

		internal void ExtractAssemblyConfig (AssemblyStoreAssembly assembly, Stream output)
		{
			if (assembly.ConfigDataOffset == 0 || assembly.ConfigDataSize == 0) {
				return;
			}
			SaveDataToStream (output, assembly.ConfigDataOffset, assembly.ConfigDataSize);
		}

		void SaveDataToFile (string outputFilePath, uint offset, uint size)
		{
			EnsureStoreDataAvailable ();
			using (var fs = File.Open (outputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)) {
				SaveDataToStream (fs, offset, size);
			}
		}

		void SaveDataToStream (Stream output, uint offset, uint size)
		{
			EnsureStoreDataAvailable ();
			ArrayPool<byte> pool = ArrayPool<byte>.Shared;

			storeData!.Seek (offset, SeekOrigin.Begin);
			byte[] buf = pool.Rent (16384);
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
			pool.Return (buf);
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
