using System;
using System.Collections.Generic;

using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

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
			public uint Index;

			[NativeAssembler (Ignore = true)]
			public string BufferSymbolName;

			[NativeAssembler (Ignore = true)]
			public string AssemblyName;

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
		Dictionary<AndroidTargetArch, List<StructureInstance<CompressedAssemblyDescriptor>>> archData = new Dictionary<AndroidTargetArch, List<StructureInstance<CompressedAssemblyDescriptor>>> ();

		public CompressedAssembliesNativeAssemblyGenerator (IDictionary<string, CompressedAssemblyInfo> assemblies)
		{
			this.assemblies = assemblies;
		}

		void InitCompressedAssemblies (out StructureInstance<CompressedAssemblies>? compressedAssemblies,
		                               out List<LlvmIrGlobalVariable>? buffers)
		{
			if (assemblies == null || assemblies.Count == 0) {
				compressedAssemblies = null;
				buffers = null;
				return;
			}

			AndroidTargetArch? firstArch = null;
			foreach (var kvp in assemblies) {
				CompressedAssemblyInfo info = kvp.Value;

				if (firstArch == null) {
					firstArch = info.TargetArch;
				}

				if (!archData.TryGetValue (info.TargetArch, out List<StructureInstance<CompressedAssemblyDescriptor>> descriptors)) {
					descriptors = new List<StructureInstance<CompressedAssemblyDescriptor>> ();
					archData.Add (info.TargetArch, descriptors);
				}

				string bufferName = $"__compressedAssemblyData_{info.DescriptorIndex}";
				var descriptor = new CompressedAssemblyDescriptor {
					Index = info.DescriptorIndex,
					BufferSymbolName = bufferName,
					AssemblyName = info.AssemblyName,
					uncompressed_file_size = info.FileSize,
					loaded = false,
					data = 0
				};

				descriptors.Add (new StructureInstance<CompressedAssemblyDescriptor> (compressedAssemblyDescriptorStructureInfo, descriptor));
			}

			buffers = new List<LlvmIrGlobalVariable> ();
			uint assembliesCount = 0;
			foreach (var kvp in archData) {
				List<StructureInstance<CompressedAssemblyDescriptor>> descriptors = kvp.Value;
				descriptors.Sort ((StructureInstance<CompressedAssemblyDescriptor> a, StructureInstance<CompressedAssemblyDescriptor> b) => a.Instance.Index.CompareTo (b.Instance.Index));
				if (buffers.Count > 0) {
					continue;
				}

				assembliesCount = (uint)descriptors.Count;
				foreach (StructureInstance<CompressedAssemblyDescriptor> desc in descriptors) {
					var buffer = new LlvmIrGlobalVariable (typeof(List<byte>), desc.Instance.BufferSymbolName, LlvmIrVariableOptions.LocalWritable) {
						BeforeWriteCallback = BufferBeforeWrite,
						BeforeWriteCallbackCallerState = desc.Instance.Index,
						ZeroInitializeArray = true,
					};
					buffers.Add (buffer);
				}
			}

			compressedAssemblies = new StructureInstance<CompressedAssemblies> (compressedAssembliesStructureInfo, new CompressedAssemblies { count = assembliesCount });
		}

		protected override void Construct (LlvmIrModule module)
		{
			MapStructures (module);

			StructureInstance<CompressedAssemblies>? compressedAssemblies;
			List<LlvmIrGlobalVariable>? buffers;

			InitCompressedAssemblies (out compressedAssemblies, out buffers);

			if (archData.Count == 0) {
				module.AddGlobalVariable (
					typeof(StructureInstance<CompressedAssemblies>),
					CompressedAssembliesSymbolName,
					new StructureInstance<CompressedAssemblies> (compressedAssembliesStructureInfo, new CompressedAssemblies ()) { IsZeroInitialized = true },
					LlvmIrVariableOptions.GlobalWritable
				);
				return;
			}

			module.AddGlobalVariable (CompressedAssembliesSymbolName, compressedAssemblies, LlvmIrVariableOptions.GlobalWritable);

			var compressedAssemblyDescriptors = new LlvmIrGlobalVariable (typeof(List<StructureInstance<CompressedAssemblyDescriptor>>), DescriptorsArraySymbolName) {
				BeforeWriteCallback = CompressedAssemblyDescriptorsBeforeWrite,
				GetArrayItemCommentCallback = GetCompressedAssemblyDescriptorsItemComment,
				Options = LlvmIrVariableOptions.LocalWritable,
			};
			module.Add (compressedAssemblyDescriptors);

			module.Add (new LlvmIrGroupDelimiterVariable ());
			module.Add (buffers);
			module.Add (new LlvmIrGroupDelimiterVariable ());
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

		void CompressedAssemblyDescriptorsBeforeWrite (LlvmIrVariable variable, LlvmIrModuleTarget target, object? state)
		{
			List<StructureInstance<CompressedAssemblyDescriptor>> descriptors = GetArchDescriptors (target);
			var gv = (LlvmIrGlobalVariable)variable;
			gv.OverrideTypeAndValue (gv.Type, descriptors);
		}

		void BufferBeforeWrite (LlvmIrVariable variable, LlvmIrModuleTarget target, object? state)
		{
			List<StructureInstance<CompressedAssemblyDescriptor>> descriptors = GetArchDescriptors (target);
			uint descriptorIndex = (uint)state;
			StructureInstance<CompressedAssemblyDescriptor> desc = descriptors[(int)descriptorIndex];
			var gv = (LlvmIrGlobalVariable)variable;

			gv.ArrayItemCount = desc.Instance.uncompressed_file_size;
			gv.OverrideName (desc.Instance.BufferSymbolName);
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
			compressedAssembliesStructureInfo = module.MapStructure<CompressedAssemblies> ();
		}
	}
}
