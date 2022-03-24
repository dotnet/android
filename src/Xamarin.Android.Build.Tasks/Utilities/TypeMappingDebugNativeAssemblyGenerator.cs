
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tools;

using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks
{
	class LlvmTypeMappingDebugNativeAssemblyGenerator : LlvmTypeMappingAssemblyGenerator
	{
		const string JavaToManagedSymbol = "map_java_to_managed";
		const string ManagedToJavaSymbol = "map_managed_to_java";
		const string TypeMapSymbol = "type_map"; // MUST match src/monodroid/xamarin-app.hh

		sealed class TypeMapContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var map_module = EnsureType<TypeMap> (data);

				if (String.Compare ("assembly_name", fieldName, StringComparison.Ordinal) == 0) {
					return "assembly_name (unused in this mode)";
				}

				if (String.Compare ("data", fieldName, StringComparison.Ordinal) == 0) {
					return "data (unused in this mode)";
				}

				return String.Empty;
			}

			public override ulong GetBufferSize (object data, string fieldName)
			{
				var map_module = EnsureType<TypeMap> (data);
				if (String.Compare ("java_to_managed", fieldName, StringComparison.Ordinal) == 0 ||
				    String.Compare ("managed_to_java", fieldName, StringComparison.Ordinal) == 0) {
					return map_module.entry_count;
				}

				return 0;
			}

			public override string GetPointedToSymbolName (object data, string fieldName)
			{
				var map_module = EnsureType<TypeMap> (data);

				if (String.Compare ("java_to_managed", fieldName, StringComparison.Ordinal) == 0) {
					return map_module.JavaToManagedCount == 0 ? null : JavaToManagedSymbol;
				}

				if (String.Compare ("managed_to_java", fieldName, StringComparison.Ordinal) == 0) {
					return map_module.ManagedToJavaCount == 0 ? null : ManagedToJavaSymbol;
				}

				return base.GetPointedToSymbolName (data, fieldName);
			}
		}

		sealed class TypeMapEntryContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var entry = EnsureType<TypeMapEntry> (data);

				if (String.Compare ("from", fieldName, StringComparison.Ordinal) == 0) {
					return $"from: entry.from";
				}

				if (String.Compare ("to", fieldName, StringComparison.Ordinal) == 0) {
					return $"to: entry.to";
				}

				return String.Empty;
			}
		}

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh TypeMapEntry structure
		[NativeAssemblerStructContextDataProvider (typeof (TypeMapEntryContextDataProvider))]
		sealed class TypeMapEntry
		{
			public string from;
			public string to;
		};

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh TypeMap structure
		[NativeAssemblerStructContextDataProvider (typeof (TypeMapContextDataProvider))]
		sealed class TypeMap
		{
			[NativeAssembler (Ignore = true)]
			public int    JavaToManagedCount;

			[NativeAssembler (Ignore = true)]
			public int    ManagedToJavaCount;

			public uint   entry_count;

			[NativeAssembler (UsesDataProvider = true), NativePointer (IsNull = true)]
			public string? assembly_name = null; // unused in Debug mode

			[NativeAssembler (UsesDataProvider = true), NativePointer (IsNull = true)]
			public byte   data = 0; // unused in Debug mode

			[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToSymbol = "")]
			public TypeMapEntry? java_to_managed = null;

			[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToSymbol = "")]
			public TypeMapEntry? managed_to_java = null;
		};

		readonly TypeMapGenerator.ModuleDebugData data;

		StructureInfo<TypeMapEntry> typeMapEntryStructureInfo;
		StructureInfo<TypeMap> typeMapStructureInfo;
		List<StructureInstance<TypeMapEntry>> javaToManagedMap;
		List<StructureInstance<TypeMapEntry>> managedToJavaMap;
		StructureInstance<TypeMap> type_map;

		public LlvmTypeMappingDebugNativeAssemblyGenerator (TypeMapGenerator.ModuleDebugData data)
		{
			this.data = data;

			javaToManagedMap = new List<StructureInstance<TypeMapEntry>> ();
			managedToJavaMap = new List<StructureInstance<TypeMapEntry>> ();
		}

		public override void Init ()
		{
			if (data.ManagedToJavaMap != null && data.ManagedToJavaMap.Count > 0) {
				foreach (TypeMapGenerator.TypeMapDebugEntry entry in data.ManagedToJavaMap) {
					var m2j = new TypeMapEntry {
						from = entry.ManagedName,
						to = entry.JavaName,
					};
					managedToJavaMap.Add (new StructureInstance<TypeMapEntry> (m2j));
				}
			}

			if (data.JavaToManagedMap != null && data.JavaToManagedMap.Count > 0) {
				foreach (TypeMapGenerator.TypeMapDebugEntry entry in data.JavaToManagedMap) {
					TypeMapGenerator.TypeMapDebugEntry managedEntry = entry.DuplicateForJavaToManaged != null ? entry.DuplicateForJavaToManaged : entry;

					var j2m = new TypeMapEntry {
						from = entry.JavaName,
						to = managedEntry.SkipInJavaToManaged ? null : managedEntry.ManagedName,
					};
					javaToManagedMap.Add (new StructureInstance<TypeMapEntry> (j2m));
				}
			}

			var map = new TypeMap {
				JavaToManagedCount = data.JavaToManagedMap == null ? 0 : data.JavaToManagedMap.Count,
				ManagedToJavaCount = data.ManagedToJavaMap == null ? 0 : data.ManagedToJavaMap.Count,

				entry_count = data.EntryCount,
			};
			type_map = new StructureInstance<TypeMap> (map);
		}

		protected override void MapStructures (LlvmIrGenerator generator)
		{
			typeMapEntryStructureInfo = generator.MapStructure<TypeMapEntry> ();
			typeMapStructureInfo = generator.MapStructure<TypeMap> ();
		}

		protected override void Write (LlvmIrGenerator generator)
		{
			if (managedToJavaMap.Count > 0) {
				generator.WriteStructureArray (typeMapEntryStructureInfo, managedToJavaMap, LlvmIrVariableOptions.LocalConstant, ManagedToJavaSymbol);
			}

			if (javaToManagedMap.Count > 0) {
				generator.WriteStructureArray (typeMapEntryStructureInfo, javaToManagedMap, LlvmIrVariableOptions.LocalConstant, JavaToManagedSymbol);
			}

			generator.WriteStructure (typeMapStructureInfo, type_map, LlvmIrVariableOptions.GlobalConstant, TypeMapSymbol);
		}
	}

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
