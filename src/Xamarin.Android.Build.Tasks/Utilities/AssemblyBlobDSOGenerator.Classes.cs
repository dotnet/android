using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

partial class AssemblyBlobDSOGenerator
{
	// Native structures and constants

	// Must be identical to the like-named constants in src/monodroid/jni/xamarin-app.hh
	const uint AssemblyEntry_IsCompressed = 1 << 0;
	const uint AssemblyEntry_HasConfig    = 1 << 1;

	class AssemblyIndexEntryBase<T>
	{
		[NativeAssembler (Ignore = true)]
		public string Name;

		[NativeAssembler (NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal)]
		public T     name_hash;
		public uint  input_data_offset;
		public uint  input_data_size;
		public uint  output_data_offset;
		public uint  output_data_size;
		public uint  info_index;
		public uint  flags;
	}

	sealed class AssemblyIndexEntry32 : AssemblyIndexEntryBase<uint>
        {}

        sealed class AssemblyIndexEntry64 : AssemblyIndexEntryBase<ulong>
        {}

	struct AssembliesConfig
	{
		public uint assembly_blob_size;
		public uint assembly_name_length;
		public uint assembly_count;
		public uint assembly_index_size;
	}

		// Generator support
	public sealed class BlobAssemblyInfo
	{
		public bool IsCompressed  { get; set; }
		public ulong OffsetInBlob { get; set; }
		public ulong SizeInBlob   { get; set; }
		public ulong Size         { get; set; }
		public ulong Offset       { get; set; }
		public string? Config     { get; set; }
		public ITaskItem Item     { get; }
		public string Name        { get; }
		public byte[] NameBytes   { get; }

		public BlobAssemblyInfo (ITaskItem item)
		{
			Item = item;
			Name = Path.GetFileName (item.ItemSpec);
			NameBytes = LlvmIrComposer.StringToBytes (Name);
		}
	}

	sealed class ArchState
	{
		public ulong BlobSize = 0;
		public ulong DataSize = 0;
		public ulong AssemblyNameLength = 0;
		public ulong AssemblyCount = 0;

		public readonly bool Is64Bit;
		public readonly List<StructureInstance<AssemblyIndexEntry32>> Index32 = new List<StructureInstance<AssemblyIndexEntry32>> ();
		public readonly List<StructureInstance<AssemblyIndexEntry64>> Index64 = new List<StructureInstance<AssemblyIndexEntry64>> ();
		public readonly List<byte[]> AssemblyNames = new List<byte[]> ();
		public readonly List<BlobAssemblyInfo> Assemblies;

		public ArchState (List<BlobAssemblyInfo> assemblies, AndroidTargetArch arch)
		{
			Assemblies = assemblies;
			Is64Bit = arch switch {
				AndroidTargetArch.Arm => false,
				AndroidTargetArch.X86 => false,
				AndroidTargetArch.Arm64 => true,
				AndroidTargetArch.X86_64 => true,
				_ => throw new NotSupportedException ($"Architecture '{arch}' is not supported")
			};
		}
	}
}
