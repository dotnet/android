using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tools;
using Legacy = Xamarin.Android.AssemblyStore.V1;

namespace Xamarin.Android.AssemblyStore;

class StoreReader_V1 : AssemblyStoreReader
{
	public override string Description => "Assembly store v1";
	public override bool NeedsExtensionInName => false;

	readonly Dictionary<AssemblyStoreItem, Legacy.AssemblyStoreAssembly> sourceAssemblies = new Dictionary<AssemblyStoreItem, Legacy.AssemblyStoreAssembly> ();

	StoreReader_V1 (IList<Legacy.AssemblyStoreReader> stores, AndroidTargetArch targetArch, string storePath)
		: base (Stream.Null, storePath)
	{
		TargetArch = targetArch;
		Is64Bit = TargetArch == AndroidTargetArch.Arm64 || TargetArch == AndroidTargetArch.X86_64;

		var items = new List<AssemblyStoreItem> ();
		uint indexEntryCount = 0;
		foreach (Legacy.AssemblyStoreReader store in stores) {
			indexEntryCount += (uint)(store.GlobalIndex32.Count + store.GlobalIndex64.Count);
			foreach (Legacy.AssemblyStoreAssembly assembly in store.Assemblies) {
				var item = new StoreItem_V1 (assembly, TargetArch, Is64Bit);
				items.Add (item);
				sourceAssemblies.Add (item, assembly);
			}
		}

		Assemblies = items.AsReadOnly ();
		AssemblyCount = (uint)items.Count;
		IndexEntryCount = indexEntryCount;
	}

	public static (IList<StoreReader_V1>? readers, string? errorMessage) Open (string inputFile)
	{
		var errors = new List<string> ();
		var explorer = new Legacy.AssemblyStoreExplorer (
			inputFile,
			(level, message) => {
				if (level == Legacy.AssemblyStoreExplorerLogLevel.Error) {
					errors.Add (message);
				}
			},
			keepStoreInMemory: true
		);

		if (errors.Count > 0) {
			return (null, String.Join (Environment.NewLine, errors));
		}

		var commonStores = new List<Legacy.AssemblyStoreReader> ();
		var storesByArch = new Dictionary<AndroidTargetArch, List<Legacy.AssemblyStoreReader>> ();
		foreach (var storeGroup in explorer.Stores) {
			foreach (Legacy.AssemblyStoreReader store in storeGroup.Value) {
				AndroidTargetArch targetArch = GetTargetArch (store.Arch);
				if (targetArch == AndroidTargetArch.None) {
					commonStores.Add (store);
				} else {
					if (!storesByArch.TryGetValue (targetArch, out List<Legacy.AssemblyStoreReader>? stores)) {
						stores = new List<Legacy.AssemblyStoreReader> ();
						storesByArch.Add (targetArch, stores);
					}
					stores.Add (store);
				}
			}
		}

		var readers = new List<StoreReader_V1> ();
		foreach (var archStores in storesByArch) {
			var stores = new List<Legacy.AssemblyStoreReader> (commonStores);
			stores.AddRange (archStores.Value);
			readers.Add (new StoreReader_V1 (stores, archStores.Key, $"{explorer.StorePath}!{archStores.Key}"));
		}

		if (storesByArch.Count == 0 && HasAssemblies (commonStores)) {
			readers.Add (new StoreReader_V1 (commonStores, AndroidTargetArch.None, $"{explorer.StorePath}!shared"));
		}

		if (readers.Count == 0) {
			return (null, null);
		}

		return (readers.AsReadOnly (), null);

		static bool HasAssemblies (List<Legacy.AssemblyStoreReader> stores)
		{
			foreach (Legacy.AssemblyStoreReader store in stores) {
				if (store.Assemblies.Count > 0) {
					return true;
				}
			}
			return false;
		}
	}

	protected override bool IsSupported ()
	{
		return true;
	}

	protected override void Prepare ()
	{
	}

	protected override ulong GetStoreStartDataOffset () => 0;

	public override Stream ReadEntryImageData (AssemblyStoreItem entry, bool uncompressIfNeeded = false)
	{
		if (!sourceAssemblies.TryGetValue (entry, out Legacy.AssemblyStoreAssembly? assembly)) {
			throw new ArgumentException ($"Assembly '{entry.Name}' does not belong to store '{StorePath}'", nameof (entry));
		}

		var stream = new MemoryStream ();
		assembly.ExtractImage (stream);
		stream.Seek (0, SeekOrigin.Begin);
		return UncompressIfNeeded (stream, uncompressIfNeeded);
	}

	static AndroidTargetArch GetTargetArch (string arch)
	{
		return arch.Replace ('-', '_').ToLowerInvariant () switch {
			""            => AndroidTargetArch.None,
			"arm64_v8a"   => AndroidTargetArch.Arm64,
			"armeabi_v7a" => AndroidTargetArch.Arm,
			"x86_64"      => AndroidTargetArch.X86_64,
			"x86"         => AndroidTargetArch.X86,
			_             => AndroidTargetArch.Other,
		};
	}

	sealed class StoreItem_V1 : AssemblyStoreItem
	{
		public StoreItem_V1 (Legacy.AssemblyStoreAssembly assembly, AndroidTargetArch targetArch, bool is64Bit)
			: base (GetName (assembly), is64Bit, GetHashes (assembly), ignore: false)
		{
			DataOffset = assembly.DataOffset;
			DataSize = assembly.DataSize;
			DebugOffset = assembly.DebugDataOffset;
			DebugSize = assembly.DebugDataSize;
			ConfigOffset = assembly.ConfigDataOffset;
			ConfigSize = assembly.ConfigDataSize;
			TargetArch = targetArch;
		}

		static string GetName (Legacy.AssemblyStoreAssembly assembly)
		{
			if (!String.IsNullOrEmpty (assembly.Name)) {
				return assembly.Name;
			}

			return $"{assembly.Store.StoreID}_{assembly.DataOffset:x}_{assembly.Hash32:x8}_{assembly.Hash64:x16}";
		}

		static List<ulong> GetHashes (Legacy.AssemblyStoreAssembly assembly)
		{
			var hashes = new List<ulong> ();
			if (assembly.Hash32 != 0) {
				hashes.Add (assembly.Hash32);
			}
			if (assembly.Hash64 != 0) {
				hashes.Add (assembly.Hash64);
			}
			return hashes;
		}
	}
}
