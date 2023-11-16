using System.Collections.Generic;

namespace Xamarin.Android.AssemblyStore;

partial class StoreReader_V2
{
	sealed class Header
	{
		public const uint NativeSize = 5 * sizeof (uint);

		public readonly uint magic;
		public readonly uint version;
		public readonly uint entry_count;
		public readonly uint index_entry_count;

		// Index size in bytes
		public readonly uint index_size;

		public Header (uint magic, uint version, uint entry_count, uint index_entry_count, uint index_size)
		{
			this.magic = magic;
			this.version = version;
			this.entry_count = entry_count;
			this.index_entry_count = index_entry_count;
			this.index_size = index_size;
		}
	}

	sealed class IndexEntry
	{
		public string? name;
		public readonly ulong name_hash;
		public readonly uint  descriptor_index;

		public IndexEntry (ulong name_hash, uint descriptor_index)
		{
			this.name_hash = name_hash;
			this.descriptor_index = descriptor_index;
		}
	}

	sealed class EntryDescriptor
	{
		public uint mapping_index;

		public uint data_offset;
		public uint data_size;

		public uint debug_data_offset;
		public uint debug_data_size;

		public uint config_data_offset;
		public uint config_data_size;
	}

	sealed class StoreItem_V2 : AssemblyStoreItem
	{
		public StoreItem_V2 (string name, bool is64Bit, List<IndexEntry> indexEntries, EntryDescriptor descriptor)
			: base (name, is64Bit, IndexToHashes (indexEntries))
		{
			DataOffset = descriptor.data_offset;
			DataSize = descriptor.data_size;
			DebugOffset = descriptor.debug_data_offset;
			DebugSize = descriptor.debug_data_size;
			ConfigOffset = descriptor.config_data_offset;
			ConfigSize = descriptor.config_data_size;
		}

		static List<ulong> IndexToHashes (List<IndexEntry> indexEntries)
		{
			var ret = new List<ulong> ();
			foreach (IndexEntry ie in indexEntries) {
				ret.Add (ie.name_hash);
			}

			return ret;
		}
	}

	sealed class TemporaryItem
	{
		public readonly string Name;
		public readonly List<IndexEntry> IndexEntries = new List<IndexEntry> ();
		public readonly EntryDescriptor Descriptor;

		public TemporaryItem (string name, EntryDescriptor descriptor)
		{
			Name = name;
			Descriptor = descriptor;
		}
	}
}
