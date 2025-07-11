#nullable disable

using System;
using System.Collections.Generic;

using Microsoft.Build.Utilities;

using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	partial class CompressedAssembliesNativeAssemblyGenerator : LlvmIrComposer
	{
		const string DescriptorsArraySymbolName = "compressed_assembly_descriptors";
		const string CompressedAssemblyCountSymbolName = "compressed_assembly_count";
		const string UncompressedAssembliesBufferSymbolName = "uncompressed_assemblies_data_buffer";
		const string UncompressedAssembliesBufferSizeSymbolName = "uncompressed_assemblies_data_size";

		// Order of fields and their type must correspond *exactly* to that in:
		//
		// src/native/mono/xamarin-app-stub/xamarin-app.hh CompressedAssemblyDescriptor structure
		// src/native/clr/include/xamarin-app.hh CompressedAssemblyDescriptor structure
		//
		//[NativeAssemblerStructContextDataProvider (typeof (CompressedAssemblyDescriptorContextDataProvider))]
		sealed class CompressedAssemblyDescriptor
		{
			[NativeAssembler (Ignore = true)]
			public uint Index;

			[NativeAssembler (Ignore = true)]
			public string AssemblyName;

			public uint   uncompressed_file_size;
			public bool   loaded;
			public uint   buffer_offset;
		};

		IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>? archAssemblies;
		StructureInfo compressedAssemblyDescriptorStructureInfo;
		Dictionary<AndroidTargetArch, List<StructureInstance<CompressedAssemblyDescriptor>>> archData = new Dictionary<AndroidTargetArch, List<StructureInstance<CompressedAssemblyDescriptor>>> ();

		public CompressedAssembliesNativeAssemblyGenerator (TaskLoggingHelper log, IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>? archAssemblies)
			: base (log)
		{
			this.archAssemblies = archAssemblies;
		}

		void InitCompressedAssemblies (out List<LlvmIrGlobalVariable>? compressedAssemblies,
		                               out List<LlvmIrGlobalVariable>? compressedAssemblyDescriptors,
		                               out List<LlvmIrGlobalVariable>? buffers)
		{
			if (archAssemblies == null || archAssemblies.Count == 0) {
				compressedAssemblies = null;
				compressedAssemblyDescriptors = null;
				buffers = null;
				return;
			}

			buffers = new ();
			foreach (var kvpArch in archAssemblies) {
				uint bufferSize = 0;

				foreach (var kvp in kvpArch.Value) {
					CompressedAssemblyInfo info = kvp.Value;

					if (!archData.TryGetValue (info.TargetArch, out List<StructureInstance<CompressedAssemblyDescriptor>> descriptors)) {
						descriptors = new List<StructureInstance<CompressedAssemblyDescriptor>> ();
						archData.Add (info.TargetArch, descriptors);
					}

					var descriptor = new CompressedAssemblyDescriptor {
						Index = info.DescriptorIndex,
						AssemblyName = info.AssemblyName,
						uncompressed_file_size = info.FileSize,
						loaded = false,
						buffer_offset = bufferSize,
					};
					bufferSize += info.FileSize;
					descriptors.Add (new StructureInstance<CompressedAssemblyDescriptor> (compressedAssemblyDescriptorStructureInfo, descriptor));
				}

				var variable = new LlvmIrGlobalVariable (typeof(uint), UncompressedAssembliesBufferSizeSymbolName) {
					Options = LlvmIrVariableOptions.GlobalConstant,
					TargetArch = kvpArch.Key,
					Value = bufferSize,
				};
				buffers.Add (variable);

				variable = new LlvmIrGlobalVariable (typeof(List<byte>), UncompressedAssembliesBufferSymbolName, LlvmIrVariableOptions.GlobalWritable) {
					ArrayItemCount = bufferSize,
					TargetArch = kvpArch.Key,
					ZeroInitializeArray = true,
				};
				buffers.Add (variable);
			}

			compressedAssemblies = new List<LlvmIrGlobalVariable> ();
			compressedAssemblyDescriptors = new List<LlvmIrGlobalVariable> ();
			foreach (var kvp in archData) {
				List<StructureInstance<CompressedAssemblyDescriptor>> descriptors = kvp.Value;
				descriptors.Sort ((StructureInstance<CompressedAssemblyDescriptor> a, StructureInstance<CompressedAssemblyDescriptor> b) => a.Instance.Index.CompareTo (b.Instance.Index));

				var variable = new LlvmIrGlobalVariable (typeof(uint), CompressedAssemblyCountSymbolName) {
					Options = LlvmIrVariableOptions.GlobalConstant,
					TargetArch = kvp.Key,
					Value = (uint)descriptors.Count,
				};
				compressedAssemblies.Add (variable);

				variable = new LlvmIrGlobalVariable (typeof(List<StructureInstance<CompressedAssemblyDescriptor>>), DescriptorsArraySymbolName) {
					GetArrayItemCommentCallback = GetCompressedAssemblyDescriptorsItemComment,
					Options = LlvmIrVariableOptions.GlobalWritable,
					TargetArch = kvp.Key,
					Value = descriptors,
				};
				compressedAssemblyDescriptors.Add (variable);
			}
		}

		protected override void Construct (LlvmIrModule module)
		{
			module.DefaultStringGroup = "cas";

			MapStructures (module);

			InitCompressedAssemblies (
				out List<LlvmIrGlobalVariable>? compressedAssemblies,
				out List<LlvmIrGlobalVariable>? compressedAssemblyDescriptors,
				out List<LlvmIrGlobalVariable>? buffers
			);

			if (archData.Count == 0) {
				var emptyBufferVar = new LlvmIrGlobalVariable (typeof(List<byte>), UncompressedAssembliesBufferSymbolName, LlvmIrVariableOptions.GlobalWritable) {
					ArrayItemCount = 0,
					ZeroInitializeArray = true,
				};
				module.Add (emptyBufferVar);
				return;
			}

			module.Add (compressedAssemblies);
			module.Add (compressedAssemblyDescriptors);
			module.Add (buffers);
		}

		string? GetCompressedAssemblyDescriptorsItemComment (LlvmIrVariable v, LlvmIrModuleTarget target, ulong index, object? value, object? callerState)
		{
			List<StructureInstance<CompressedAssemblyDescriptor>> descriptors = GetArchDescriptors (target);
			if ((int)index >= descriptors.Count) {
				throw new InvalidOperationException ($"Internal error: index {index} is too big for variable '{v.Name}'");
			}
			StructureInstance<CompressedAssemblyDescriptor> desc = descriptors[(int)index];

			return $" {index}: {desc.Instance.AssemblyName}";
		}

		List<StructureInstance<CompressedAssemblyDescriptor>> GetArchDescriptors (LlvmIrModuleTarget target)
		{
			if (!archData.TryGetValue (target.TargetArch, out List<StructureInstance<CompressedAssemblyDescriptor>> descriptors)) {
				throw new InvalidOperationException ($"Internal error: missing compressed descriptors data for architecture '{target.TargetArch}'");
			}

			return descriptors;
		}

		void MapStructures (LlvmIrModule module)
		{
			compressedAssemblyDescriptorStructureInfo = module.MapStructure<CompressedAssemblyDescriptor> ();
		}
	}
}
