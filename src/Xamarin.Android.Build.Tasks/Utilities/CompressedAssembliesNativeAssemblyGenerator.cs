using System;
using System.Collections.Generic;

using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks
{
	partial class CompressedAssembliesNativeAssemblyGenerator : LlvmIrComposer
	{
		const string DescriptorsArraySymbolName = "compressed_assembly_descriptors";
		const string CompressedAssembliesSymbolName = "compressed_assemblies";

		sealed class CompressedAssemblyDescriptorContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override ulong GetBufferSize (object data, string fieldName)
			{
				if (String.Compare ("data", fieldName, StringComparison.Ordinal) != 0) {
					return 0;
				}

				var descriptor = EnsureType<CompressedAssemblyDescriptor> (data);
				return descriptor.uncompressed_file_size;
			}
		}

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh CompressedAssemblyDescriptor structure
		[NativeAssemblerStructContextDataProvider (typeof (CompressedAssemblyDescriptorContextDataProvider))]
		sealed class CompressedAssemblyDescriptor
		{
			public uint   uncompressed_file_size;
			public bool   loaded;

			[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToPreAllocatedBuffer = true)]
			public byte data;
		};

		sealed class CompressedAssembliesContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override ulong GetBufferSize (object data, string fieldName)
			{
				if (String.Compare ("descriptors", fieldName, StringComparison.Ordinal) != 0) {
					return 0;
				}

				var cas = EnsureType<CompressedAssemblies> (data);
				return cas.count;
			}
		}

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh CompressedAssemblies structure
		[NativeAssemblerStructContextDataProvider (typeof (CompressedAssembliesContextDataProvider))]
		sealed class CompressedAssemblies
		{
			public uint count;

			[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToSymbol = DescriptorsArraySymbolName)]
			public CompressedAssemblyDescriptor descriptors;
		};

		IDictionary<string, CompressedAssemblyInfo> assemblies;
		StructureInfo<CompressedAssemblyDescriptor> compressedAssemblyDescriptorStructureInfo;
		StructureInfo<CompressedAssemblies> compressedAssembliesStructureInfo;
		List<StructureInstance<CompressedAssemblyDescriptor>>? compressedAssemblyDescriptors;
		StructureInstance<CompressedAssemblies> compressedAssemblies;

		public CompressedAssembliesNativeAssemblyGenerator (IDictionary<string, CompressedAssemblyInfo> assemblies)
		{
			this.assemblies = assemblies;
		}

		public override void Init ()
		{
			if (assemblies == null || assemblies.Count == 0) {
				return;
			}

			compressedAssemblyDescriptors = new List<StructureInstance<CompressedAssemblyDescriptor>> (assemblies.Count);
			foreach (var kvp in assemblies) {
				string assemblyName = kvp.Key;
				CompressedAssemblyInfo info = kvp.Value;

				var descriptor = new CompressedAssemblyDescriptor {
					uncompressed_file_size = info.FileSize,
					loaded = false,
					data = 0
				};

				compressedAssemblyDescriptors.Add (new StructureInstance<CompressedAssemblyDescriptor> (descriptor));
			}

			compressedAssemblies = new StructureInstance<CompressedAssemblies> (new CompressedAssemblies { count = (uint)assemblies.Count });
		}

		protected override void MapStructures (LlvmIrGenerator generator)
		{
			compressedAssemblyDescriptorStructureInfo = generator.MapStructure<CompressedAssemblyDescriptor> ();
			compressedAssembliesStructureInfo = generator.MapStructure<CompressedAssemblies> ();
		}

		protected override void Write (LlvmIrGenerator generator)
		{
			if (compressedAssemblyDescriptors == null) {
				generator.WriteStructure (compressedAssembliesStructureInfo, null, CompressedAssembliesSymbolName);
				return;
			}

			generator.WriteStructureArray<CompressedAssemblyDescriptor> (
				compressedAssemblyDescriptorStructureInfo,
				compressedAssemblyDescriptors,
				LlvmIrVariableOptions.LocalWritable,
				DescriptorsArraySymbolName,
				initialComment: "Compressed assembly data storage"
			);
			generator.WriteStructure (compressedAssembliesStructureInfo, compressedAssemblies, CompressedAssembliesSymbolName);
		}
	}
}
