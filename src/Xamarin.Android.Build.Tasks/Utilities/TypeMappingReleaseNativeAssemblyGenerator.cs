using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks
{
	class TypeMappingReleaseNativeAssemblyGenerator : TypeMappingAssemblyGenerator
	{
		sealed class TypeMapModuleContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var map_module = EnsureType<TypeMapModule> (data);

				if (String.Compare ("module_uuid", fieldName, StringComparison.Ordinal) == 0) {
					return $"module_uuid: {map_module.MVID}";
				}

				if (String.Compare ("assembly_name", fieldName, StringComparison.Ordinal) == 0) {
					return $"assembly_name: {map_module.assembly_name}";
				}

				return String.Empty;
			}

			public override string? GetPointedToSymbolName (object data, string fieldName)
			{
				var map_module = EnsureType<TypeMapModule> (data);

				if (String.Compare ("map", fieldName, StringComparison.Ordinal) == 0) {
					return map_module.MapSymbolName;
				}

				if (String.Compare ("duplicate_map", fieldName, StringComparison.Ordinal) == 0) {
					return map_module.DuplicateMapSymbolName;
				}

				return null;
			}

			public override ulong GetBufferSize (object data, string fieldName)
			{
				var map_module = EnsureType<TypeMapModule> (data);

				if (String.Compare ("map", fieldName, StringComparison.Ordinal) == 0) {
					return map_module.entry_count;
				}

				if (String.Compare ("duplicate_map", fieldName, StringComparison.Ordinal) == 0) {
					return map_module.duplicate_count;
				}

				return base.GetBufferSize (data, fieldName);
			}
		}

		sealed class TypeMapJavaContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override uint GetMaxInlineWidth (object data, string fieldName)
			{
				if (String.Compare ("java_name", fieldName, StringComparison.Ordinal) == 0) {
					// Using a static field for this is **very** clunky, but it works in our case since we will
					// set that field only once per build session and it allows us to query the array size while
					// generating the structure declarations (as required by LLVM IR)
					return TypeMapJava.MaxJavaNameLength;
				}

				return 0;
			}
		}

		// This is here only to generate strongly-typed IR
		sealed class MonoImage
		{}

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh TypeMapModuleEntry structure
		sealed class TypeMapModuleEntry
		{
			public uint type_token_id;
			public uint java_map_index;
		}

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh TypeMapModule structure
		[NativeAssemblerStructContextDataProvider (typeof (TypeMapModuleContextDataProvider))]
		sealed class TypeMapModule
		{
			[NativeAssembler (Ignore = true)]
			public Guid    MVID;

			[NativeAssembler (Ignore = true)]
			public string? MapSymbolName;

			[NativeAssembler (Ignore = true)]
			public string? DuplicateMapSymbolName;

			[NativeAssembler (UsesDataProvider = true, InlineArray = true, InlineArraySize = 16)]
			public byte[]  module_uuid;
			public uint    entry_count;
			public uint    duplicate_count;

			[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToSymbol = "")]
			public TypeMapModuleEntry map;

			[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToSymbol = "")]
			public TypeMapModuleEntry duplicate_map;

			[NativeAssembler (UsesDataProvider = true)]
			public string assembly_name;

			[NativePointer (IsNull = true)]
			public MonoImage image;
			public uint   java_name_width;

			[NativePointer (IsNull = true)]
			public byte java_map;
		}

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh TypeMapJava structure
		[NativeAssemblerStructContextDataProvider (typeof (TypeMapJavaContextDataProvider))]
		sealed class TypeMapJava
		{
			[NativeAssembler (Ignore = true)]
			public static uint MaxJavaNameLength;

			public uint module_index;
			public uint type_token_id;

			[NativeAssembler (UsesDataProvider = true, InlineArray = true, NeedsPadding = true)]
			public byte[] java_name;
		}

		sealed class ModuleMapData
		{
			public string SymbolLabel { get; }
			public List<StructureInstance<TypeMapModuleEntry>> Entries { get; }

			public ModuleMapData (string symbolLabel)
			{
				SymbolLabel = symbolLabel;
				Entries = new List<StructureInstance<TypeMapModuleEntry>> ();
			}
		}

		readonly NativeTypeMappingData mappingData;
		StructureInfo<TypeMapJava> typeMapJavaStructureInfo;
		StructureInfo<TypeMapModule> typeMapModuleStructureInfo;
		StructureInfo<TypeMapModuleEntry> typeMapModuleEntryStructureInfo;
		List<ModuleMapData> mapModulesData;
		List<StructureInstance<TypeMapModule>> mapModules;
		List<StructureInstance<TypeMapJava>> javaMap;

		ulong moduleCounter = 0;

		public TypeMappingReleaseNativeAssemblyGenerator (NativeTypeMappingData mappingData)
		{
			this.mappingData = mappingData ?? throw new ArgumentNullException (nameof (mappingData));
			mapModulesData = new List<ModuleMapData> ();
			mapModules = new List<StructureInstance<TypeMapModule>> ();
			javaMap = new List<StructureInstance<TypeMapJava>> ();
		}

		public override void Init ()
		{
			TypeMapJava.MaxJavaNameLength = mappingData.JavaNameWidth;
			InitMapModules ();
			InitJavaMap ();
		}

		void InitJavaMap ()
		{
			foreach (TypeMapGenerator.TypeMapReleaseEntry entry in mappingData.JavaTypes) {
				var map_entry = new TypeMapJava {
					module_index = (uint)entry.ModuleIndex,
					type_token_id = entry.SkipInJavaToManaged ? 0 : entry.Token,
					java_name = Encoding.UTF8.GetBytes (entry.JavaName),
				};

				javaMap.Add (new StructureInstance<TypeMapJava> (map_entry));
			}
		}

		void InitMapModules ()
		{
			foreach (TypeMapGenerator.ModuleReleaseData data in mappingData.Modules) {
				string mapName = $"module{moduleCounter++}_managed_to_java";
				string duplicateMapName;

				if (data.DuplicateTypes.Count == 0)
					duplicateMapName = String.Empty;
				else
					duplicateMapName = $"{mapName}_duplicates";

				var map_module = new TypeMapModule {
					MVID = data.Mvid,
					MapSymbolName = mapName,
					DuplicateMapSymbolName = duplicateMapName.Length == 0 ? null : duplicateMapName,

					module_uuid = data.MvidBytes,
					entry_count = (uint)data.Types.Length,
					duplicate_count = (uint)data.DuplicateTypes.Count,
					assembly_name = data.AssemblyName,
					java_name_width = 0,
				};

				InitMapModuleData (mapName, data.Types, mapModulesData);
				if (data.DuplicateTypes.Count > 0) {
					InitMapModuleData (duplicateMapName, data.DuplicateTypes.Values, mapModulesData);
				}

				mapModules.Add (new StructureInstance<TypeMapModule> (map_module));
			}
		}

		void InitMapModuleData (string moduleDataSymbolLabel, IEnumerable<TypeMapGenerator.TypeMapReleaseEntry> moduleEntries, List<ModuleMapData> allModulesData)
		{
			var tokens = new Dictionary<uint, uint> ();
			foreach (TypeMapGenerator.TypeMapReleaseEntry entry in moduleEntries) {
				int idx = Array.BinarySearch (mappingData.JavaTypeNames, entry.JavaName, StringComparer.Ordinal);
				if (idx < 0)
					throw new InvalidOperationException ($"Could not map entry '{entry.JavaName}' to array index");

				tokens[entry.Token] = (uint)idx;
			}

			var sortedTokens = tokens.Keys.ToArray ();
			Array.Sort (sortedTokens);

			var moduleData = new ModuleMapData (moduleDataSymbolLabel);
			foreach (uint token in sortedTokens) {
				var map_entry = new TypeMapModuleEntry {
					type_token_id = token,
					java_map_index = tokens[token],
				};

				moduleData.Entries.Add (new StructureInstance<TypeMapModuleEntry> (map_entry));
			}

			allModulesData.Add (moduleData);
		}

		protected override void MapStructures (LlvmIrGenerator generator)
		{
			generator.MapStructure<MonoImage> ();
			typeMapJavaStructureInfo = generator.MapStructure<TypeMapJava> ();
			typeMapModuleStructureInfo = generator.MapStructure<TypeMapModule> ();
			typeMapModuleEntryStructureInfo = generator.MapStructure<TypeMapModuleEntry> ();
		}

		protected override void Write (LlvmIrGenerator generator)
		{
			generator.WriteVariable ("map_module_count", mappingData.MapModuleCount);
			generator.WriteVariable ("java_type_count", mappingData.JavaTypeCount);
			generator.WriteVariable ("java_name_width", mappingData.JavaNameWidth);

			WriteMapModules (generator);
			WriteJavaMap (generator);
		}

		void WriteJavaMap (LlvmIrGenerator generator)
		{
			generator.WriteEOL ();
			generator.WriteEOL ("Java to managed map");
			generator.WritePackedStructureArray (
				typeMapJavaStructureInfo,
				javaMap,
				LlvmIrVariableOptions.GlobalConstant,
				"map_java"
			);
		}

		void WriteMapModules (LlvmIrGenerator generator)
		{
			if (mapModules.Count == 0) {
				return;
			}

			generator.WriteEOL ();
			generator.WriteEOL ("Map modules data");

			foreach (ModuleMapData mmd in mapModulesData) {
				generator.WriteStructureArray (
					typeMapModuleEntryStructureInfo,
					mmd.Entries,
					LlvmIrVariableOptions.LocalConstant,
					mmd.SymbolLabel
				);
			}

			generator.WriteEOL ("Map modules");
			generator.WriteStructureArray (
				typeMapModuleStructureInfo,
				mapModules,
				LlvmIrVariableOptions.GlobalWritable,
				"map_modules"
			);
		}
	}
}
