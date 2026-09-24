#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class CompressedAssembliesNativeAssemblyGenerator
	{
		const string DescriptorsArraySymbolName = "compressed_assembly_descriptors";
		const string CompressedAssemblyCountSymbolName = "compressed_assembly_count";
		const string UncompressedAssembliesBufferSymbolName = "uncompressed_assemblies_data_buffer";
		const string UncompressedAssembliesBufferSizeSymbolName = "uncompressed_assemblies_data_size";

		// The %struct.CompressedAssemblyDescriptor declaration below must be identical to the
		// CompressedAssemblyDescriptor structure in src/native/clr/include/xamarin-app.hh
		// See LlvmIrTarget.GetAggregateAlignment for the meaning of the "data size"
		const ulong DescriptorDataSize = 9;
		const ulong DescriptorAlignment = 4;

		sealed class CompressedAssemblyDescriptor
		{
			public uint Index;
			public string AssemblyName = "";

			public uint uncompressed_file_size;
			public uint buffer_offset;
		}

		readonly Dictionary<AndroidTargetArch, List<CompressedAssemblyDescriptor>> archDescriptors = new ();
		readonly Dictionary<AndroidTargetArch, uint> archBufferSizes = new ();

		public CompressedAssembliesNativeAssemblyGenerator (TaskLoggingHelper log, IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>? archAssemblies)
		{
			if (log == null) {
				throw new ArgumentNullException (nameof (log));
			}

			if (archAssemblies == null || archAssemblies.Count == 0) {
				return;
			}

			foreach (var kvpArch in archAssemblies) {
				uint bufferSize = 0;

				foreach (var kvp in kvpArch.Value) {
					CompressedAssemblyInfo info = kvp.Value;

					if (!archDescriptors.TryGetValue (info.TargetArch, out List<CompressedAssemblyDescriptor>? descriptors)) {
						descriptors = new List<CompressedAssemblyDescriptor> ();
						archDescriptors.Add (info.TargetArch, descriptors);
					}

					descriptors.Add (new CompressedAssemblyDescriptor {
						Index = info.DescriptorIndex,
						AssemblyName = info.AssemblyName,
						uncompressed_file_size = info.FileSize,
						buffer_offset = bufferSize,
					});
					bufferSize += info.FileSize;
				}

				archBufferSizes [kvpArch.Key] = bufferSize;
			}

			foreach (var kvp in archDescriptors) {
				kvp.Value.Sort ((CompressedAssemblyDescriptor a, CompressedAssemblyDescriptor b) => a.Index.CompareTo (b.Index));
			}
		}

		public void Generate (AndroidTargetArch arch, TextWriter output, string fileName)
		{
			using var w = new LlvmIrWriter (output, LlvmIrTarget.Get (arch));
			w.WriteHeader (fileName);
			w.Write ($$"""

				%struct.CompressedAssemblyDescriptor = type {
					i32, ; uint32_t uncompressed_file_size
					i1, ; bool loaded
					i32 ; uint32_t buffer_offset
				}

				""");

			if (archDescriptors.Count == 0) {
				WriteCount (w, 0);
				WriteDescriptors (w, []);
				WriteBuffer (w, 0);
			} else {
				if (archDescriptors.TryGetValue (arch, out List<CompressedAssemblyDescriptor>? descriptors)) {
					WriteCount (w, (uint)descriptors.Count);
					WriteDescriptors (w, descriptors);
				}

				if (archBufferSizes.TryGetValue (arch, out uint bufferSize)) {
					WriteBuffer (w, bufferSize);
				}
			}

			w.WriteMetadata ();
			output.Flush ();
		}

		static void WriteCount (LlvmIrWriter w, uint count)
		{
			w.WriteGlobal (CompressedAssemblyCountSymbolName, LlvmIrWriter.GlobalConstant, "i32", count.ToString (), 4);
		}

		static void WriteDescriptors (LlvmIrWriter w, List<CompressedAssemblyDescriptor> descriptors)
		{
			var elements = new List<string> (descriptors.Count);
			foreach (CompressedAssemblyDescriptor d in descriptors) {
				elements.Add ($$"""
						%struct.CompressedAssemblyDescriptor {
							i32 {{d.uncompressed_file_size}}, ; uint32_t uncompressed_file_size
							i1 false, ; bool loaded
							i32 {{d.buffer_offset}}; uint32_t buffer_offset
						}
					""");
			}

			w.WriteGlobal (
				DescriptorsArraySymbolName,
				LlvmIrWriter.GlobalWritable,
				$"[{descriptors.Count} x %struct.CompressedAssemblyDescriptor]",
				w.ArrayValue (elements, i => $" {i}: {descriptors [i].AssemblyName}"),
				w.GetAggregateAlignment (DescriptorAlignment, (ulong)descriptors.Count * DescriptorDataSize)
			);
		}

		static void WriteBuffer (LlvmIrWriter w, uint bufferSize)
		{
			w.WriteGlobal (UncompressedAssembliesBufferSizeSymbolName, LlvmIrWriter.GlobalConstant, "i32", bufferSize.ToString (), 4);
			w.WriteGlobal (UncompressedAssembliesBufferSymbolName, LlvmIrWriter.GlobalWritable, $"[{bufferSize} x i8]", "zeroinitializer", w.GetAggregateAlignment (1, bufferSize));
		}
	}
}
