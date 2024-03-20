namespace Xamarin.Android.Tasks;

partial class AssemblyStoreGenerator
{
	sealed class AssemblyStoreHeader
	{
		public const uint NativeSize = 5 * sizeof (uint);

		public readonly uint magic = ASSEMBLY_STORE_MAGIC;
		public readonly uint version;
		public readonly uint entry_count;
		public readonly uint index_entry_count;

		// Index size in bytes
		public readonly uint index_size;

		public AssemblyStoreHeader (uint version, uint entry_count, uint index_entry_count, uint index_size)
		{
			this.version = version;
			this.entry_count = entry_count;
			this.index_entry_count = index_entry_count;
			this.index_size = index_size;
		}
#if XABT_TESTS
		public AssemblyStoreHeader (uint magic, uint version, uint entry_count, uint index_entry_count, uint index_size)
			: this (version, entry_count, index_entry_count, index_size)
		{
			this.magic = magic;
		}
#endif
	}

	sealed class AssemblyStoreIndexEntry
	{
		public const uint NativeSize32 = 2 * sizeof (uint);
		public const uint NativeSize64 = sizeof (ulong) + sizeof (uint);

		public readonly string name;
		public readonly ulong name_hash;
		public readonly uint  descriptor_index;

		public AssemblyStoreIndexEntry (string name, ulong name_hash, uint descriptor_index)
		{
			this.name = name;
			this.name_hash = name_hash;
			this.descriptor_index = descriptor_index;
		}
	}

	sealed class AssemblyStoreEntryDescriptor
	{
		public const uint NativeSize = 7 * sizeof (uint);

		public uint mapping_index;

		public uint data_offset;
		public uint data_size;

		public uint debug_data_offset;
		public uint debug_data_size;

		public uint config_data_offset;
		public uint config_data_size;
	}
}
