
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class TypeMappingDebugNativeAssemblyGenerator : TypeMappingAssemblyGenerator
	{
		sealed class TypeMapContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var map_module = data as TypeMap;
				if (map_module == null) {
					throw new InvalidOperationException ("Invalid data type, expected an instance of TypeMap");
				}

				if (String.Compare ("assembly_name", fieldName, StringComparison.Ordinal) == 0) {
					return "assembly_name (unused in this mode)";
				}

				if (String.Compare ("data", fieldName, StringComparison.Ordinal) == 0) {
					return "data (unused in this mode)";
				}

				return String.Empty;
			}
		}

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh TypeMapEntry structure
		sealed class TypeMapEntry
		{
			[NativeAssemblerString (AssemblerStringFormat.PointerToSymbol)]
			public string from;

			[NativeAssemblerString (AssemblerStringFormat.PointerToSymbol)]
			public string to;
		};

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh TypeMap structure
		[NativeAssemblerStructContextDataProvider (typeof (TypeMapContextDataProvider))]
		sealed class TypeMap
		{
			public uint  entry_count;

			[NativeAssembler (UsesDataProvider = true)]
			public IntPtr assembly_name = IntPtr.Zero; // unused in Debug mode

			[NativeAssembler (UsesDataProvider = true)]
			public IntPtr data = IntPtr.Zero; // unused in Debug mode

			[NativeAssemblerString (AssemblerStringFormat.PointerToSymbol)]
			public string        java_to_managed;

			[NativeAssemblerString (AssemblerStringFormat.PointerToSymbol)]
			public string        managed_to_java;
		};

		const string JavaToManagedSymbol = "map_java_to_managed";
		const string ManagedToJavaSymbol = "map_managed_to_java";
		const string TypeMapSymbol = "type_map"; // MUST match src/monodroid/xamarin-app.hh

		readonly string baseFileName;
		readonly bool sharedBitsWritten;
		readonly TypeMapGenerator.ModuleDebugData data;

		public TypeMappingDebugNativeAssemblyGenerator (AndroidTargetArch arch, TypeMapGenerator.ModuleDebugData data, string baseFileName, bool sharedBitsWritten, bool sharedIncludeUsesAbiPrefix = false)
			: base (arch, baseFileName, sharedIncludeUsesAbiPrefix)
		{
			if (String.IsNullOrEmpty (baseFileName))
				throw new ArgumentException("must not be null or empty", nameof (baseFileName));
			this.data = data ?? throw new ArgumentNullException (nameof (data));

			this.baseFileName = baseFileName;
			this.sharedBitsWritten = sharedBitsWritten;
		}

		protected override void Write (NativeAssemblyGenerator generator)
		{
			bool haveJavaToManaged = data.JavaToManagedMap != null && data.JavaToManagedMap.Count > 0;
			bool haveManagedToJava = data.ManagedToJavaMap != null && data.ManagedToJavaMap.Count > 0;

			using (var sharedOutput = MemoryStreamPool.Shared.CreateStreamWriter (generator.Output.Encoding)) {
				WriteSharedBits (generator, sharedOutput, haveJavaToManaged, haveManagedToJava);
				sharedOutput.Flush ();
				Files.CopyIfStreamChanged (sharedOutput.BaseStream, SharedIncludeFile);
			}

			if (haveJavaToManaged || haveManagedToJava) {
				generator.WriteInclude (Path.GetFileName (SharedIncludeFile));
			}

			string managedToJavaSymbolName;
			generator.WriteCommentLine ("Managed to java map: START");
			generator.WriteDataSection ($"rel.{ManagedToJavaSymbol}");

			if (haveManagedToJava) {
				NativeAssemblyGenerator.StructureWriteContext mapArray = generator.StartStructureArray ();
				var map_entry = new TypeMapEntry ();
				foreach (TypeMapGenerator.TypeMapDebugEntry entry in data.ManagedToJavaMap) {
					map_entry.from = entry.ManagedLabel;
					map_entry.to = entry.JavaLabel;

					NativeAssemblyGenerator.StructureWriteContext mapEntryStruct = generator.AddStructureArrayElement (mapArray);
					generator.WriteStructure (mapEntryStruct, map_entry);
				}
				managedToJavaSymbolName = generator.WriteSymbol (mapArray, ManagedToJavaSymbol);
			} else {
				managedToJavaSymbolName = generator.WriteEmptySymbol (SymbolType.Object, ManagedToJavaSymbol);
			}
			generator.WriteCommentLine ("Managed to java map: END");

			string javaToManagedSymbolName;
			generator.WriteCommentLine ("Java to managed map: START");
			generator.WriteDataSection ($"rel.{JavaToManagedSymbol}");

			if (haveJavaToManaged) {
				NativeAssemblyGenerator.StructureWriteContext mapArray = generator.StartStructureArray ();
				var map_entry = new TypeMapEntry ();
				foreach (TypeMapGenerator.TypeMapDebugEntry entry in data.JavaToManagedMap) {
					map_entry.from = entry.JavaLabel;

					TypeMapGenerator.TypeMapDebugEntry managedEntry = entry.DuplicateForJavaToManaged != null ? entry.DuplicateForJavaToManaged : entry;
					map_entry.to = managedEntry.SkipInJavaToManaged ? null : managedEntry.ManagedLabel;

					NativeAssemblyGenerator.StructureWriteContext mapEntryStruct = generator.AddStructureArrayElement (mapArray);
					generator.WriteStructure (mapEntryStruct, map_entry);
				}

				javaToManagedSymbolName = generator.WriteSymbol (mapArray, JavaToManagedSymbol);
			} else {
				javaToManagedSymbolName = generator.WriteEmptySymbol (SymbolType.Object, JavaToManagedSymbol);
			}
			generator.WriteCommentLine ("Java to managed map: END");

			generator.WriteCommentLine ("TypeMap structure");
			generator.WriteDataSection ($"rel.ro.{TypeMapSymbol}");

			var type_map = new TypeMap {
				entry_count = (uint)data.EntryCount,
				java_to_managed = javaToManagedSymbolName,
				managed_to_java = managedToJavaSymbolName,
			};

			NativeAssemblyGenerator.StructureWriteContext typeMapStruct = generator.StartStructure ();
			generator.WriteStructure (typeMapStruct, type_map);
			generator.WriteSymbol (typeMapStruct, TypeMapSymbol, local: false);
		}

		void WriteSharedBits (NativeAssemblyGenerator generator, StreamWriter output, bool haveJavaToManaged, bool haveManagedToJava)
		{
			if (haveJavaToManaged) {
				generator.WriteStringSection (output, "java_type_names");
				generator.WriteCommentLine (output, "Java type names: START", useBlockComment: true);
				foreach (TypeMapGenerator.TypeMapDebugEntry entry in data.JavaToManagedMap) {
					entry.JavaLabel = generator.MakeLocalLabel ("java_type_name");
					generator.WriteStringSymbol (output, entry.JavaLabel, entry.JavaName, global: false);
				}
				generator.WriteCommentLine (output, "Java type names: END", useBlockComment: true);
			}

			if (haveManagedToJava) {
				generator.WriteStringSection (output, "managed_type_names");
				generator.WriteCommentLine (output, "Managed type names: START", useBlockComment: true);
				foreach (TypeMapGenerator.TypeMapDebugEntry entry in data.ManagedToJavaMap) {
					entry.ManagedLabel = generator.MakeLocalLabel ("managed_type_name");
					generator.WriteStringSymbol (output, entry.ManagedLabel, entry.ManagedName, global: false);
				}
				generator.WriteCommentLine (output, "Managed type names: END", useBlockComment: true);
			}
		}
	}
}
