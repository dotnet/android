using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class CompressedAssembliesNativeAssemblyGenerator : NativeAssemblyComposer
	{

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh CompressedAssemblyDescriptor structure
		sealed class CompressedAssemblyDescriptor
		{
			public uint   uncompressed_file_size;
			public bool   loaded;

			[NativeAssemblerString (AssemblerStringFormat.PointerToSymbol)]
			public string data = String.Empty;
		};

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh CompressedAssemblies structure
		sealed class CompressedAssemblies
		{
			public uint count;

			[NativeAssemblerString (AssemblerStringFormat.PointerToSymbol)]
			public string descriptors;
		};

		const string CompressedAssembliesField = "compressed_assemblies";
		const string DescriptorsField = "compressed_assembly_descriptors";

		IDictionary<string, CompressedAssemblyInfo> assemblies;
		string dataIncludeFile;

		public CompressedAssembliesNativeAssemblyGenerator (AndroidTargetArch arch, IDictionary<string, CompressedAssemblyInfo> assemblies, string baseFilePath)
			: base (arch)
		{
			this.assemblies = assemblies;
			dataIncludeFile = $"{baseFilePath}-data.inc";
		}

		protected override void Write (NativeAssemblyGenerator generator)
		{
			if (assemblies == null || assemblies.Count == 0) {
				WriteCompressedAssembliesStructure (generator, 0, null);
				return;
			}

			generator.WriteInclude (Path.GetFileName (dataIncludeFile));

			string label;
			using (var dataOutput = MemoryStreamPool.Shared.CreateStreamWriter (generator.Output.Encoding)) {
				generator.WriteDataSection ();

				var descriptor = new CompressedAssemblyDescriptor {
					loaded = false,
				};

				NativeAssemblyGenerator.StructureWriteContext descriptorArray = generator.StartStructureArray ();
				foreach (var kvp in assemblies) {
					string assemblyName = kvp.Key;
					CompressedAssemblyInfo info = kvp.Value;

					NativeAssemblyGenerator.LabeledSymbol dataLabel = generator.WriteCommSymbol (dataOutput, "compressed_assembly_data", info.FileSize, alignment: 16);
					descriptor.uncompressed_file_size = info.FileSize;
					descriptor.data = dataLabel.Label;

					NativeAssemblyGenerator.StructureWriteContext descriptorStruct = generator.AddStructureArrayElement (descriptorArray);
					generator.WriteStructure (descriptorStruct, descriptor);
				}
				label = generator.WriteSymbol (descriptorArray, DescriptorsField);

				dataOutput.Flush ();
				Files.CopyIfStreamChanged (dataOutput.BaseStream, dataIncludeFile);
			}

			WriteCompressedAssembliesStructure (generator, (uint)assemblies.Count, label);
		}

		void WriteCompressedAssembliesStructure (NativeAssemblyGenerator generator, uint count, string descriptorsLabel)
		{
			generator.WriteDataSection ();
			var compressed_assemblies = new CompressedAssemblies {
				count = count,
				descriptors = descriptorsLabel,
			};

			NativeAssemblyGenerator.StructureWriteContext compressedAssemblies = generator.StartStructure ();
			generator.WriteStructure (compressedAssemblies, compressed_assemblies);
			generator.WriteSymbol (compressedAssemblies, CompressedAssembliesField, local: false);
		}
	}
}
