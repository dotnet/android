#nullable enable
using System.Collections.Generic;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks;

// This native object remains necessary until the assembly-store and compression state move to the runtime.
class AssemblyStoreNativeAssemblyGenerator : LlvmIrComposer
{
#pragma warning disable CS0649
	sealed class AssemblyStoreEntryDescriptor
	{
		public uint mapping_index;
		public uint data_offset;
		public uint data_size;
		public uint debug_data_offset;
		public uint debug_data_size;
		public uint config_data_offset;
		public uint config_data_size;
	}

	sealed class AssemblyStoreSingleAssemblyRuntimeData
	{
		[NativePointer]
		public byte image_data;
		[NativePointer]
		public byte debug_info_data;
		[NativePointer]
		public byte config_data;
		[NativePointer]
		public AssemblyStoreEntryDescriptor? descriptor;
	}

	sealed class AssemblyStoreRuntimeData
	{
		[NativePointer (IsNull = true)]
		public byte data_start;
		public uint assembly_count;
		public uint index_entry_count;
		[NativePointer (IsNull = true)]
		public AssemblyStoreEntryDescriptor? assemblies;
	}
#pragma warning restore CS0649

	const ulong FORMAT_TAG = 0x00045E6972616D58;

	public int NumberOfAssembliesInApk { get; set; }

	public AssemblyStoreNativeAssemblyGenerator (TaskLoggingHelper log) : base (log)
	{
	}

	protected override void Construct (LlvmIrModule module)
	{
		module.MapStructure<AssemblyStoreEntryDescriptor> ();
		module.MapStructure<AssemblyStoreSingleAssemblyRuntimeData> ();
		var storeInfo = module.MapStructure<AssemblyStoreRuntimeData> ();

		module.AddGlobalVariable ("format_tag", FORMAT_TAG, comment: $" 0x{FORMAT_TAG:x}");
		module.Add (new LlvmIrGlobalVariable (typeof (List<StructureInstance<AssemblyStoreSingleAssemblyRuntimeData>>),
			"assembly_store_bundled_assemblies", LlvmIrVariableOptions.GlobalWritable) {
			ZeroInitializeArray = true,
			ArrayItemCount = (ulong)NumberOfAssembliesInApk,
		});
		module.Add (new LlvmIrGlobalVariable (
			new StructureInstance<AssemblyStoreRuntimeData> (storeInfo, new AssemblyStoreRuntimeData ()),
			"assembly_store", LlvmIrVariableOptions.GlobalWritable
		));
	}
}
