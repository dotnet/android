using System;
using System.Collections.Generic;

using Microsoft.Build.Utilities;

using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks
{
	partial class CompressedAssembliesNativeAssemblyGenerator : LlvmIrComposer
	{
		const string DescriptorsArraySymbolName = "compressed_assembly_descriptors";
		const string CompressedAssembliesSymbolName = "compressed_assemblies";

		sealed class CompressedAssemblyDescriptorContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string? GetPointedToSymbolName (object data, string fieldName)
			{
				if (String.Compare ("data", fieldName, StringComparison.Ordinal) != 0) {
					return null;
				}

				var descriptor = EnsureType<CompressedAssemblyDescriptor> (data);
				return descriptor.BufferSymbolName;
			}
		}

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh CompressedAssemblyDescriptor structure
		[NativeAssemblerStructContextDataProvider (typeof (CompressedAssemblyDescriptorContextDataProvider))]
		sealed class CompressedAssemblyDescriptor
		{
			[NativeAssembler (Ignore = true)]
			public string BufferSymbolName;

			public uint   uncompressed_file_size;
			public bool   loaded;

			[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToSymbol = "")]
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
		StructureInfo compressedAssemblyDescriptorStructureInfo;
		StructureInfo compressedAssembliesStructureInfo;

		public CompressedAssembliesNativeAssemblyGenerator (TaskLoggingHelper log, IDictionary<string, CompressedAssemblyInfo> assemblies)
			: base (log)
		{
			this.assemblies = assemblies;
		}

		void InitCompressedAssemblies (out List<StructureInstance<CompressedAssemblyDescriptor>>? compressedAssemblyDescriptors,
		                               out StructureInstance<CompressedAssemblies>? compressedAssemblies,
		                               out List<LlvmIrGlobalVariable>? buffers)
		{
			if (assemblies == null || assemblies.Count == 0) {
				compressedAssemblyDescriptors = null;
				compressedAssemblies = null;
				buffers = null;
				return;
			}

			ulong counter = 0;
			compressedAssemblyDescriptors = new List<StructureInstance<CompressedAssemblyDescriptor>> (assemblies.Count);
			buffers = new List<LlvmIrGlobalVariable> (assemblies.Count);
			foreach (var kvp in assemblies) {
				string assemblyName = kvp.Key;
				CompressedAssemblyInfo info = kvp.Value;

				string bufferName = $"__compressedAssemblyData_{counter++}";
				var descriptor = new CompressedAssemblyDescriptor {
					BufferSymbolName = bufferName,
					uncompressed_file_size = info.FileSize,
					loaded = false,
					data = 0
				};

				var bufferVar = new LlvmIrGlobalVariable (typeof(List<byte>), bufferName, LlvmIrVariableOptions.LocalWritable) {
					ZeroInitializeArray = true,
					ArrayItemCount = descriptor.uncompressed_file_size,
				};
				buffers.Add (bufferVar);

				compressedAssemblyDescriptors.Add (new StructureInstance<CompressedAssemblyDescriptor> (compressedAssemblyDescriptorStructureInfo, descriptor));
			}

			compressedAssemblies = new StructureInstance<CompressedAssemblies> (compressedAssembliesStructureInfo, new CompressedAssemblies { count = (uint)assemblies.Count });
		}

		protected override void Construct (LlvmIrModule module)
		{
			MapStructures (module);

			List<StructureInstance<CompressedAssemblyDescriptor>>? compressedAssemblyDescriptors;
			StructureInstance<CompressedAssemblies>? compressedAssemblies;
			List<LlvmIrGlobalVariable>? buffers;

			InitCompressedAssemblies (out compressedAssemblyDescriptors, out compressedAssemblies, out buffers);

			if (compressedAssemblyDescriptors == null) {
				module.AddGlobalVariable (
					typeof(StructureInstance<CompressedAssemblies>),
					CompressedAssembliesSymbolName,
					new StructureInstance<CompressedAssemblies> (compressedAssembliesStructureInfo, new CompressedAssemblies ()) { IsZeroInitialized = true },
					LlvmIrVariableOptions.GlobalWritable
				);
				return;
			}

			module.AddGlobalVariable (CompressedAssembliesSymbolName, compressedAssemblies, LlvmIrVariableOptions.GlobalWritable);
			module.AddGlobalVariable (DescriptorsArraySymbolName, compressedAssemblyDescriptors, LlvmIrVariableOptions.LocalWritable);

			module.Add (new LlvmIrGroupDelimiterVariable ());
			module.Add (buffers);
			module.Add (new LlvmIrGroupDelimiterVariable ());
		}

		void MapStructures (LlvmIrModule module)
		{
			compressedAssemblyDescriptorStructureInfo = module.MapStructure<CompressedAssemblyDescriptor> ();
			compressedAssembliesStructureInfo = module.MapStructure<CompressedAssemblies> ();
		}
	}
}
