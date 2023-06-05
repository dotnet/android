using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Xamarin.Android.Tasks.LLVM.IR;

namespace Xamarin.Android.Tasks.New
{
	// TODO: remove these aliases once the refactoring is done
	using NativePointerAttribute = LLVMIR.NativePointerAttribute;

	partial class TypeMappingReleaseNativeAssemblyGenerator : LlvmIrComposer
	{
		sealed class TypeMapModuleContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var map_module = EnsureType<TypeMapModule> (data);

				if (String.Compare ("module_uuid", fieldName, StringComparison.Ordinal) == 0) {
					return $" module_uuid: {map_module.MVID}";
				}

				if (String.Compare ("assembly_name", fieldName, StringComparison.Ordinal) == 0) {
					return $" assembly_name: {map_module.assembly_name}";
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

		sealed class JavaNameHashComparer : IComparer<StructureInstance<TypeMapJava>>
		{
			public int Compare (StructureInstance<TypeMapJava> a, StructureInstance<TypeMapJava> b)
			{
				return a.Instance.JavaNameHash.CompareTo (b.Instance.JavaNameHash);
			}
		}

		// This is here only to generate strongly-typed IR
		internal sealed class MonoImage
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

			[NativeAssembler (Ignore = true)]
			public TypeMapGenerator.ModuleReleaseData Data;

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
		sealed class TypeMapJava
		{
			[NativeAssembler (Ignore = true)]
			public string JavaName;

			[NativeAssembler (Ignore = true)]
			public ulong JavaNameHash;

			public uint module_index;
			public uint type_token_id;
			public uint java_name_index;
		}

		sealed class ModuleMapData
		{
			public string SymbolLabel { get; }
			public List<StructureInstance<TypeMapModuleEntry>> Entries { get; }

			public ModuleMapData (string symbolLabel, List<StructureInstance<TypeMapModuleEntry>> entries)
			{
				SymbolLabel = symbolLabel;
				Entries = entries;
			}
		}

		sealed class ConstructionState
		{
			public List<StructureInstance<TypeMapModule>> mapModules;
			public Dictionary<string, TypeMapJava> javaTypesByName;
			public List<string> javaNames;
			public List<StructureInstance<TypeMapJava>> javaMap;
		}

		readonly NativeTypeMappingData mappingData;
		StructureInfo typeMapJavaStructureInfo;
		StructureInfo typeMapModuleStructureInfo;
		StructureInfo typeMapModuleEntryStructureInfo;
		JavaNameHashComparer javaNameHashComparer;

		ulong moduleCounter = 0;

		public TypeMappingReleaseNativeAssemblyGenerator (NativeTypeMappingData mappingData)
		{
			this.mappingData = mappingData ?? throw new ArgumentNullException (nameof (mappingData));
			javaNameHashComparer = new JavaNameHashComparer ();
		}

		protected override void Construct (LlvmIrModule module)
		{
			MapStructures (module);

			var cs = new ConstructionState ();
			cs.javaTypesByName = new Dictionary<string, TypeMapJava> (StringComparer.Ordinal);
			cs.javaNames = new List<string> ();
			InitJavaMap (cs);
			InitMapModules (cs);

			module.AddGlobalVariable ("map_module_count", mappingData.MapModuleCount);
			module.AddGlobalVariable ("java_type_count", cs.javaMap.Count);

			var map_modules = new LlvmIrGlobalVariable (cs.mapModules, "map_modules", LLVMIR.LlvmIrVariableOptions.GlobalWritable) {
				Comment = " Managed modules map",
			};
			module.Add (map_modules);
		}

		void InitJavaMap (ConstructionState cs)
		{
			cs.javaMap = new List<StructureInstance<TypeMapJava>> ();
			TypeMapJava map_entry;
			foreach (TypeMapGenerator.TypeMapReleaseEntry entry in mappingData.JavaTypes) {
				cs.javaNames.Add (entry.JavaName);

				map_entry = new TypeMapJava {
					module_index = (uint)entry.ModuleIndex, // UInt32.MaxValue,
					type_token_id = entry.SkipInJavaToManaged ? 0 : entry.Token,
					java_name_index = (uint)(cs.javaNames.Count - 1),
					JavaName = entry.JavaName,
				};

				cs.javaMap.Add (new StructureInstance<TypeMapJava> (typeMapJavaStructureInfo, map_entry));
				cs.javaTypesByName.Add (map_entry.JavaName, map_entry);
			}
		}

		void InitMapModules (ConstructionState cs)
		{
			cs.mapModules = new List<StructureInstance<TypeMapModule>> ();
			foreach (TypeMapGenerator.ModuleReleaseData data in mappingData.Modules) {
				string mapName = $"module{moduleCounter++}_managed_to_java";
				string duplicateMapName;

				if (data.DuplicateTypes.Count == 0) {
					duplicateMapName = String.Empty;
				} else {
					duplicateMapName = $"{mapName}_duplicates";
				}

				var map_module = new TypeMapModule {
					MVID = data.Mvid,
					MapSymbolName = mapName,
					DuplicateMapSymbolName = duplicateMapName.Length == 0 ? null : duplicateMapName,
					Data = data,

					module_uuid = data.MvidBytes,
					entry_count = (uint)data.Types.Length,
					duplicate_count = (uint)data.DuplicateTypes.Count,
					assembly_name = data.AssemblyName,
					java_name_width = 0,
				};

				cs.mapModules.Add (new StructureInstance<TypeMapModule> (typeMapModuleStructureInfo, map_module));
			}
		}

		void MapStructures (LlvmIrModule module)
		{
			typeMapJavaStructureInfo = module.MapStructure<TypeMapJava> ();
			typeMapModuleStructureInfo = module.MapStructure<TypeMapModule> ();
			typeMapModuleEntryStructureInfo = module.MapStructure<TypeMapModuleEntry> ();
		}

		// Prepare module map entries by sorting them on the managed token, and then mapping each entry to its corresponding Java type map index.
		// Requires that `javaMap` is sorted on the type name hash.
		void PrepareMapModuleData (string moduleDataSymbolLabel, IEnumerable<TypeMapGenerator.TypeMapReleaseEntry> moduleEntries, List<ModuleMapData> allModulesData, ConstructionState cs)
		{
			var mapModuleEntries = new List<StructureInstance<TypeMapModuleEntry>> ();
			foreach (TypeMapGenerator.TypeMapReleaseEntry entry in moduleEntries) {
				var map_entry = new TypeMapModuleEntry {
					type_token_id = entry.Token,
					java_map_index = GetJavaEntryIndex (entry.JavaName),
				};
				mapModuleEntries.Add (new StructureInstance<TypeMapModuleEntry> (typeMapModuleEntryStructureInfo, map_entry));
			}

			mapModuleEntries.Sort ((StructureInstance<TypeMapModuleEntry> a, StructureInstance<TypeMapModuleEntry> b) => a.Instance.type_token_id.CompareTo (b.Instance.type_token_id));
			allModulesData.Add (new ModuleMapData (moduleDataSymbolLabel, mapModuleEntries));

			uint GetJavaEntryIndex (string javaTypeName)
			{
				if (!cs.javaTypesByName.TryGetValue (javaTypeName, out TypeMapJava javaType)) {
					throw new InvalidOperationException ($"INTERNAL ERROR: Java type '{javaTypeName}' not found in cache");
				}

				var key = new StructureInstance<TypeMapJava> (typeMapJavaStructureInfo, javaType);
				int idx = cs.javaMap.BinarySearch (key, javaNameHashComparer);
				if (idx < 0) {
					throw new InvalidOperationException ($"Could not map entry '{javaTypeName}' to array index");
				}

				return (uint)idx;
			}
		}

		// Generate hashes for all Java type names, then sort javaMap on the name hash.  This has to be done in the writing phase because hashes
		// will depend on architecture (or, actually, on its bitness) and may differ between architectures (they will be the same for all architectures
		// with the same bitness)
		(List<ModuleMapData> allMapModulesData, List<ulong> javaMapHashes) PrepareMapsForWriting (LlvmIrModuleTarget target, ConstructionState cs)
		{
			bool is64Bit = target.Is64Bit;

			// Generate Java type name hashes...
			for (int i = 0; i < cs.javaMap.Count; i++) {
				TypeMapJava entry = cs.javaMap[i].Instance;
				entry.JavaNameHash = HashName (entry.JavaName);
			}

			// ...sort them...
			cs.javaMap.Sort ((StructureInstance<TypeMapJava> a, StructureInstance<TypeMapJava> b) => a.Instance.JavaNameHash.CompareTo (b.Instance.JavaNameHash));

			var allMapModulesData = new List<ModuleMapData> ();

			// ...and match managed types to Java...
			foreach (StructureInstance<TypeMapModule> moduleInstance in cs.mapModules) {
				TypeMapModule module = moduleInstance.Instance;
				PrepareMapModuleData (module.MapSymbolName, module.Data.Types, allMapModulesData, cs);
				if (module.Data.DuplicateTypes.Count > 0) {
					PrepareMapModuleData (module.DuplicateMapSymbolName, module.Data.DuplicateTypes, allMapModulesData, cs);
				}
			}

			var javaMapHashes = new HashSet<ulong> ();
			foreach (StructureInstance<TypeMapJava> entry in cs.javaMap) {
				javaMapHashes.Add (entry.Instance.JavaNameHash);
			}

			return (allMapModulesData, javaMapHashes.ToList ());

			ulong HashName (string name)
			{
				if (name.Length == 0) {
					return UInt64.MaxValue;
				}

				// Native code (EmbeddedAssemblies::typemap_java_to_managed in embedded-assemblies.cc) will operate on wchar_t cast to a byte array, we need to do
				// the same
				return GetXxHash (name, is64Bit);
			}
		}
	}
}
