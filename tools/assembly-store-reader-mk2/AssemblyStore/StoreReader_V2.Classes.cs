using System.Collections.Generic;

using Xamarin.Android.Tools;

namespace Xamarin.Android.AssemblyStore;

partial class StoreReader_V2
{
	sealed class Header
	{
		public readonly uint magic;
		public readonly uint version;
		public readonly uint entry_count;
		public readonly uint index_entry_count;

		// Index size in bytes
		public readonly uint index_size;
		public readonly ulong content_id;

		public uint NativeSize => (uint)(5 * sizeof (uint) + ((version & ASSEMBLY_STORE_FORMAT_NUMBER_MASK) >= 4 ? sizeof (ulong) : 0));

		public Header (uint magic, uint version, uint entry_count, uint index_entry_count, uint index_size, ulong content_id)
		{
			this.magic = magic;
			this.version = version;
			this.entry_count = entry_count;
			this.index_entry_count = index_entry_count;
			this.index_size = index_size;
			this.content_id = content_id;
		}
	}

	sealed class IndexEntry
	{
		// We treat `bool` as `byte` here, since that's what gets written to the binary.
		public const uint NativeSize32 = 2 * sizeof (uint) + sizeof (byte);
		public const uint NativeSize64 = sizeof (ulong) + sizeof (uint) + sizeof (byte);

		public readonly ulong name_hash;
		public readonly uint  descriptor_index;
		public readonly bool  ignore;

		public IndexEntry (ulong name_hash, uint descriptor_index, bool ignore)
		{
			this.name_hash = name_hash;
			this.descriptor_index = descriptor_index;
			this.ignore = ignore;
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
		public StoreItem_V2 (AndroidTargetArch targetArch, string name, bool is64Bit, List<IndexEntry> indexEntries, EntryDescriptor descriptor, bool ignore)
			: base (name, is64Bit, IndexToHashes (indexEntries), ignore)
		{
			DataOffset = descriptor.data_offset;
			DataSize = descriptor.data_size;
			DebugOffset = descriptor.debug_data_offset;
			DebugSize = descriptor.debug_data_size;
			ConfigOffset = descriptor.config_data_offset;
			ConfigSize = descriptor.config_data_size;
			TargetArch = targetArch;
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
		public readonly bool Ignored;

		public TemporaryItem (string name, EntryDescriptor descriptor, bool ignored)
		{
			Name = name;
			Descriptor = descriptor;
			Ignored = ignored;
		}
	}
}
