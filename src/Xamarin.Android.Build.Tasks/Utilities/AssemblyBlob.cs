using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	abstract class AssemblyBlob
	{
		// The two constants below must match their counterparts in src/monodroid/jni/xamarin-app.hh
		const uint BlobMagic = 0x41424158; // 'XABA', little-endian, must match the BUNDLED_ASSEMBLIES_BLOB_MAGIC native constant
		const uint BlobVersion = 1; // Must match the BUNDLED_ASSEMBLIES_BLOB_VERSION native constant

		// MUST be equal to the size of the BlobBundledAssembly struct in src/monodroid/jni/xamarin-app.hh
		const uint BlobBundledAssemblyNativeStructSize = 6 * sizeof (uint);

		// MUST be equal to the size of the BlobHashEntry struct in src/monodroid/jni/xamarin-app.hh
		const uint BlobHashEntryNativeStructSize = sizeof (ulong) + (3 * sizeof (uint));

		// MUST be equal to the size of the BundledAssemblyBlobHeader struct in src/monodroid/jni/xamarin-app.hh
		const uint BlobHeaderNativeStructSize = sizeof (uint) * 4;

		protected const string BlobPrefix = "assemblies";
		protected const string BlobExtension = ".blob";

		static readonly ArrayPool<byte> bytePool = ArrayPool<byte>.Shared;
		static uint id = 0;

		protected static uint globalAssemblyIndex = 0;

		string archiveAssembliesPrefix;
		string indexBlobPath;

		protected string ApkName { get; }
		protected TaskLoggingHelper Log { get; }

		public uint ID { get; }
		public bool IsIndexBlob => ID == 0;

		protected AssemblyBlob (string apkName, string archiveAssembliesPrefix, TaskLoggingHelper log)
		{
			if (String.IsNullOrEmpty (archiveAssembliesPrefix)) {
				throw new ArgumentException ("must not be null or empty", nameof (archiveAssembliesPrefix));
			}

			if (String.IsNullOrEmpty (apkName)) {
				throw new ArgumentException ("must not be null or empty", nameof (apkName));
			}

			// NOTE: NOT thread safe, if we ever have parallel runs of BuildApk this operation must either be atomic or protected with a lock
			ID = id++;

			this.archiveAssembliesPrefix = archiveAssembliesPrefix;
			ApkName = apkName;
			Log = log;
		}

		public abstract void Add (BlobAssemblyInfo blobAssembly);
		public abstract void Generate (string outputDirectory, List<AssemblyBlobIndexEntry> globalIndex, List<string> blobPaths);

		public virtual string WriteIndex (List<AssemblyBlobIndexEntry> globalIndex)
		{
			if (!IsIndexBlob) {
				throw new InvalidOperationException ("Assembly index may be written only to blob with index 0");
			}

			if (String.IsNullOrEmpty (indexBlobPath)) {
				throw new InvalidOperationException ("Index blob path not set, was Generate called properly?");
			}

			if (globalIndex == null) {
				throw new ArgumentNullException (nameof (globalIndex));
			}

			string indexBlobHeaderPath = $"{indexBlobPath}.hdr";
			string indexBlobManifestPath = Path.ChangeExtension (indexBlobPath, "manifest");

			using (var hfs = File.Open (indexBlobHeaderPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
				using (var writer = new BinaryWriter (hfs, Encoding.UTF8, leaveOpen: true)) {
					WriteIndex (writer, indexBlobManifestPath, globalIndex);
					writer.Flush ();
				}

				using (var ifs = File.Open (indexBlobPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					ifs.CopyTo (hfs);
					hfs.Flush ();
				}
			}

			File.Delete (indexBlobPath);
			File.Move (indexBlobHeaderPath, indexBlobPath);

			return indexBlobManifestPath;
		}

		void WriteIndex (BinaryWriter blobWriter, string manifestPath, List<AssemblyBlobIndexEntry> globalIndex)
		{
			using (var manifest = File.Open (manifestPath, FileMode.Create, FileAccess.Write)) {
				using (var manifestWriter = new StreamWriter (manifest, new UTF8Encoding (false))) {
					WriteIndex (blobWriter, manifestWriter, globalIndex);
					manifestWriter.Flush ();
				}
			}
		}

		void WriteIndex (BinaryWriter blobWriter, StreamWriter manifestWriter, List<AssemblyBlobIndexEntry> globalIndex)
		{
			uint localEntryCount = 0;
			var localAssemblies = new List<AssemblyBlobIndexEntry> ();

			manifestWriter.WriteLine ("Hash 32     Hash 64             Blob ID  Blob idx  Name");
			// TODO: check if there are no duplicates here
			foreach (AssemblyBlobIndexEntry assembly in globalIndex) {
				if (assembly.BlobID == ID) {
					localEntryCount++;
					localAssemblies.Add (assembly);
				}

				manifestWriter.WriteLine ($"0x{assembly.NameHash32:x08}  0x{assembly.NameHash64:x016}  {assembly.BlobID:d03}      {assembly.LocalBlobIndex:d04}      {assembly.Name}");
			}

			uint globalAssemblyCount = (uint)globalIndex.Count;

			Log.LogMessage (MessageImportance.Low, $"Index blob, writing header (local assemblies: {localAssemblies.Count}; global assemblies: {globalIndex.Count})");
			blobWriter.Seek (0, SeekOrigin.Begin);
			WriteBlobHeader (blobWriter, localEntryCount, globalAssemblyCount);

			// Header and two tables of the same size, each for 32 and 64-bit hashes
			uint offsetFixup = BlobHeaderNativeStructSize + (BlobHashEntryNativeStructSize * globalAssemblyCount * 2);

			Log.LogMessage (MessageImportance.Low, "Index blob, writing assembly descriptors");
			WriteAssemblyDescriptors (blobWriter, localAssemblies, CalculateOffsetFixup ((uint)localAssemblies.Count, offsetFixup));

			Log.LogMessage (MessageImportance.Low, $"Index blob, writing hash tables ({globalAssemblyCount * 2} entries, {offsetFixup} bytes");
			var sortedIndex = new List<AssemblyBlobIndexEntry> (globalIndex);
			sortedIndex.Sort ((AssemblyBlobIndexEntry a, AssemblyBlobIndexEntry b) => a.NameHash32.CompareTo (b.NameHash32));
			foreach (AssemblyBlobIndexEntry entry in sortedIndex) {
				WriteHash (entry, entry.NameHash32);
			}

			sortedIndex.Sort ((AssemblyBlobIndexEntry a, AssemblyBlobIndexEntry b) => a.NameHash64.CompareTo (b.NameHash64));
			foreach (AssemblyBlobIndexEntry entry in sortedIndex) {
				WriteHash (entry, entry.NameHash64);
			}

			void WriteHash (AssemblyBlobIndexEntry entry, ulong hash)
			{
				blobWriter.Write (hash);
				blobWriter.Write (entry.MappingIndex);
				blobWriter.Write (entry.LocalBlobIndex);
				blobWriter.Write (entry.BlobID);
			}
		}

		protected string GetAssemblyName (BlobAssemblyInfo assembly)
		{
			string assemblyName = Path.GetFileNameWithoutExtension (assembly.FilesystemAssemblyPath);
			if (assemblyName.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
				assemblyName = Path.GetFileNameWithoutExtension (assemblyName);
			}

			return assemblyName;
		}

		protected void Generate (string outputFilePath, List<BlobAssemblyInfo> assemblies, List<AssemblyBlobIndexEntry> globalIndex, List<string> blobPaths, bool addToGlobalIndex = true)
		{
			if (globalIndex == null) {
				throw new ArgumentNullException (nameof (globalIndex));
			}

			if (blobPaths == null) {
				throw new ArgumentNullException (nameof (blobPaths));
			}

			if (IsIndexBlob) {
				indexBlobPath = outputFilePath;
			}

			blobPaths.Add (outputFilePath);
			Log.LogMessage (MessageImportance.Low, $"AssemblyBlobGenerator: generating blob: {outputFilePath}");
			// TODO: test with satellite assemblies, their name must include the culture prefix

			using (var fs = File.Open (outputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)) {
				using (var writer = new BinaryWriter (fs, Encoding.UTF8)) {
					Generate (writer, assemblies, globalIndex, addToGlobalIndex);
					writer.Flush ();
				}
			}
		}

		void Generate (BinaryWriter writer, List<BlobAssemblyInfo> assemblies, List<AssemblyBlobIndexEntry> globalIndex, bool addToGlobalIndex)
		{
			var localAssemblies = new List<AssemblyBlobIndexEntry> ();

			if (!IsIndexBlob) {
				// Index blob's header and data before the assemblies is handled in WriteIndex in a slightly different
				// way.
				uint nbytes = BlobHeaderNativeStructSize + (BlobBundledAssemblyNativeStructSize * (uint)assemblies.Count);
				var zeros = bytePool.Rent ((int)nbytes);
				writer.Write (zeros, 0, (int)nbytes);
				bytePool.Return (zeros);
			}

			foreach (BlobAssemblyInfo assembly in assemblies) {
				Log.LogMessage (MessageImportance.Low, $"AssemblyBlobGenerator: assembly hfs path == '{assembly.FilesystemAssemblyPath}'; assembly archive path == '{assembly.ArchiveAssemblyPath}'");
				string assemblyName = GetAssemblyName (assembly);
				string archivePath = assembly.ArchiveAssemblyPath;
				if (archivePath.StartsWith (archiveAssembliesPrefix, StringComparison.OrdinalIgnoreCase)) {
					archivePath = archivePath.Substring (archiveAssembliesPrefix.Length);
				}

				if (!String.IsNullOrEmpty (assembly.Abi)) {
					string abiPath = $"{assembly.Abi}/";
					if (archivePath.StartsWith (abiPath, StringComparison.Ordinal)) {
						archivePath = archivePath.Substring (abiPath.Length);
					}
				}

				if (!String.IsNullOrEmpty (archivePath)) {
					assemblyName = $"{archivePath}/{assemblyName}";
				}

				AssemblyBlobIndexEntry entry = WriteAssembly (writer, assembly, assemblyName, (uint)localAssemblies.Count);
				Log.LogMessage (MessageImportance.Low, $"   => assemblyName == '{entry.Name}'; dataOffset == {entry.DataOffset}");
				if (addToGlobalIndex) {
					globalIndex.Add (entry);
				}
				localAssemblies.Add (entry);
			}

			writer.Flush ();

			if (IsIndexBlob) {
				return;
			}

			Log.LogMessage (MessageImportance.Low, $"Not an index blob, writing header (local assemblies: {localAssemblies.Count})");
			writer.Seek (0, SeekOrigin.Begin);
			WriteBlobHeader (writer, (uint)localAssemblies.Count);

			Log.LogMessage (MessageImportance.Low, "Not an index blob, writing assembly descriptors");
			WriteAssemblyDescriptors (writer, localAssemblies);
		}

		uint CalculateOffsetFixup (uint localAssemblyCount, uint extraOffset = 0)
		{
			return (BlobBundledAssemblyNativeStructSize * (uint)localAssemblyCount) + extraOffset;
		}

		void WriteBlobHeader (BinaryWriter writer, uint localEntryCount, uint globalEntryCount = 0)
		{
			// Header, must be identical to the BundledAssemblyBlobHeader structure in src/monodroid/jni/xamarin-app.hh
			writer.Write (BlobMagic);               // magic
			writer.Write (BlobVersion);             // version
			writer.Write (localEntryCount);         // local_entry_count
			writer.Write (globalEntryCount);        // global_entry_count
			writer.Write ((uint)ID);                // blob_id
		}

		void WriteAssemblyDescriptors (BinaryWriter writer, List<AssemblyBlobIndexEntry> assemblies, uint offsetFixup = 0)
		{
			// Each assembly must be identical to the BlobBundledAssembly structure in src/monodroid/jni/xamarin-app.hh

			foreach (AssemblyBlobIndexEntry assembly in assemblies) {
				Log.LogMessage (MessageImportance.Low, $"  => {assembly.Name} before adjustment: data offset == {assembly.DataOffset}");
				AdjustOffsets (assembly, offsetFixup);
				Log.LogMessage (MessageImportance.Low, $"  => {assembly.Name} after adjustment: data offset == {assembly.DataOffset}");

				writer.Write (assembly.DataOffset);
				writer.Write (assembly.DataSize);

				writer.Write (assembly.DebugDataOffset);
				writer.Write (assembly.DebugDataSize);

				writer.Write (assembly.ConfigDataOffset);
				writer.Write (assembly.ConfigDataSize);
			}
		}

		void AdjustOffsets (AssemblyBlobIndexEntry assembly, uint offsetFixup)
		{
			if (offsetFixup == 0) {
				return;
			}

			assembly.DataOffset += offsetFixup;

			if (assembly.DebugDataOffset > 0) {
				assembly.DebugDataOffset += offsetFixup;
			}

			if (assembly.ConfigDataOffset > 0) {
				assembly.ConfigDataOffset += offsetFixup;
			}
		}

		AssemblyBlobIndexEntry WriteAssembly (BinaryWriter writer, BlobAssemblyInfo assembly, string assemblyName, uint localBlobIndex)
		{
			uint offset;
			uint size;

			(offset, size) = WriteFile (assembly.FilesystemAssemblyPath, true);

			// NOTE: globalAssemblIndex++ is not thread safe but it **must** increase monotonically (see also ArchAssemblyBlob.Generate for a special case)
			var ret = new AssemblyBlobIndexEntry (assemblyName, ID, globalAssemblyIndex++, localBlobIndex) {
				DataOffset = offset,
				DataSize = size,
			};

			(offset, size) = WriteFile (assembly.DebugInfoPath, false);
			if (offset != 0 && size != 0) {
				ret.DebugDataOffset = offset;
				ret.DebugDataSize = size;
			}

			(offset, size) = WriteFile (assembly.ConfigPath, false);
			if (offset != 0 && size != 0) {
				ret.ConfigDataOffset = offset;
				ret.ConfigDataSize = size;
			}

			return ret;

			(uint offset, uint size) WriteFile (string filePath, bool required)
			{
				if (!File.Exists (filePath)) {
					if (required) {
						throw new InvalidOperationException ($"Required file '{filePath}' not found");
					}

					return (0, 0);
				}

				var fi = new FileInfo (filePath);
				if (fi.Length == 0) {
					return (0, 0);
				}

				if (fi.Length > UInt32.MaxValue || writer.BaseStream.Position + fi.Length > UInt32.MaxValue) {
					throw new InvalidOperationException ($"Writing assembly '{filePath}' to assembly blob would exceed the maximum allowed data size.");
				}

				uint offset = (uint)writer.BaseStream.Position;
				using (var fs = File.Open (filePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					fs.CopyTo (writer.BaseStream);
				}

				return (offset, (uint)fi.Length);
			}
		}
	}
}
