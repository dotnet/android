using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

partial class AssemblyBlobDSOGenerator
{
	public sealed class BlobAssemblyInfo
	{
		public bool IsCompressed  { get; set; }
		public ulong OffsetInBlob { get; set; }
		public ulong SizeInBlob   { get; set; }
		public ulong Size         { get; set; }
		public ITaskItem Item     { get; }

		public BlobAssemblyInfo (ITaskItem item)
		{
			Item = item;
		}
	}

	class AssemblyIndexEntryBase<T>
	{
		public T     name_hash;
		public uint  input_data_offset;
		public uint  input_data_size;
		public uint  output_data_offset;
		public uint  output_data_size;
		public uint  info_index;
		public bool  is_compressed;
	};

	sealed class AssemblyIndexEntry32 : AssemblyIndexEntryBase<uint>
        {}

        sealed class AssemblyIndexEntry64 : AssemblyIndexEntryBase<ulong>
        {}

	struct AssembliesConfig
	{
		public uint assembly_blob_size;
		public uint assembly_name_length;
		public uint assembly_count;
	};
}
