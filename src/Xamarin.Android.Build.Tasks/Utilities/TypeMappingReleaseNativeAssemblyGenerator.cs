using System;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Linq;
using System.Text;

using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

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

		sealed class JavaNameHashComparer : IComparer<StructureInstance<TypeMapJava>>
		{
			public int Compare (StructureInstance<TypeMapJava> a, StructureInstance<TypeMapJava> b)
			{
				return a.Obj.JavaNameHash.CompareTo (b.Obj.JavaNameHash);
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

		sealed class ArchGenerationState
		{
			public readonly List<StructureInstance<TypeMapModule>> MapModules;
			public readonly List<StructureInstance<TypeMapJava>> JavaMap;
			public readonly Dictionary<string, TypeMapJava> JavaTypesByName;
			public readonly List<string> JavaNames;
			public readonly NativeTypeMappingData MappingData;
			public ulong ModuleCounter = 0;

			public ArchGenerationState (NativeTypeMappingData mappingData)
			{
				MapModules = new List<StructureInstance<TypeMapModule>> ();
				JavaMap = new List<StructureInstance<TypeMapJava>> ();
				JavaTypesByName = new Dictionary<string, TypeMapJava> (StringComparer.Ordinal);
				JavaNames = new List<string> ();
				MappingData = mappingData;
			}
		}

		StructureInfo<TypeMapJava> typeMapJavaStructureInfo;
		StructureInfo<TypeMapModule> typeMapModuleStructureInfo;
		StructureInfo<TypeMapModuleEntry> typeMapModuleEntryStructureInfo;
		Dictionary<AndroidTargetArch, ArchGenerationState> archState;

		JavaNameHashComparer javaNameHashComparer;

		public TypeMappingReleaseNativeAssemblyGenerator (Dictionary<AndroidTargetArch, NativeTypeMappingData> mappingData)
		{
			if (mappingData == null) {
				throw new ArgumentNullException (nameof (mappingData));
			}

			javaNameHashComparer = new JavaNameHashComparer ();
			archState = new Dictionary<AndroidTargetArch, ArchGenerationState> (mappingData.Count);

			foreach (var kvp in mappingData) {
				if (kvp.Value == null) {
					throw new ArgumentException ("must not contain null values", nameof (mappingData));
				}

				archState.Add (kvp.Key, new ArchGenerationState (kvp.Value));
			}
		}

		public override void Init ()
		{
			foreach (var kvp in archState) {
				InitMapModules (kvp.Value);
				InitJavaMap (kvp.Value);
			}
		}

		void InitJavaMap (ArchGenerationState state)
		{
			TypeMapJava map_entry;
			foreach (TypeMapGenerator.TypeMapReleaseEntry entry in state.MappingData.JavaTypes) {
				state.JavaNames.Add (entry.JavaName);

				map_entry = new TypeMapJava {
					module_index = (uint)entry.ModuleIndex, // UInt32.MaxValue,
					type_token_id = entry.SkipInJavaToManaged ? 0 : entry.Token,
					java_name_index = (uint)(state.JavaNames.Count - 1),
					JavaName = entry.JavaName,
				};

				state.JavaMap.Add (new StructureInstance<TypeMapJava> (map_entry));
				state.JavaTypesByName.Add (map_entry.JavaName, map_entry);
			}
		}

		void InitMapModules (ArchGenerationState state)
		{
			foreach (TypeMapGenerator.ModuleReleaseData data in state.MappingData.Modules) {
				string mapName = $"module{state.ModuleCounter++}_managed_to_java";
				string duplicateMapName;

				if (data.DuplicateTypes.Count == 0)
					duplicateMapName = String.Empty;
				else
					duplicateMapName = $"{mapName}_duplicates";

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

				state.MapModules.Add (new StructureInstance<TypeMapModule> (map_module));
			}
		}

		protected override void MapStructures (LlvmIrGenerator generator)
		{
			generator.MapStructure<MonoImage> ();
			typeMapJavaStructureInfo = generator.MapStructure<TypeMapJava> ();
			typeMapModuleStructureInfo = generator.MapStructure<TypeMapModule> ();
			typeMapModuleEntryStructureInfo = generator.MapStructure<TypeMapModuleEntry> ();
		}

		// Prepare module map entries by sorting them on the managed token, and then mapping each entry to its corresponding Java type map index.
		// Requires that `javaMap` is sorted on the type name hash.
		void PrepareMapModuleData (ArchGenerationState state, string moduleDataSymbolLabel, IEnumerable<TypeMapGenerator.TypeMapReleaseEntry> moduleEntries, List<ModuleMapData> allModulesData)
		{
			var mapModuleEntries = new List<StructureInstance<TypeMapModuleEntry>> ();
			foreach (TypeMapGenerator.TypeMapReleaseEntry entry in moduleEntries) {
				var map_entry = new TypeMapModuleEntry {
					type_token_id = entry.Token,
					java_map_index = GetJavaEntryIndex (entry.JavaName),
				};
				mapModuleEntries.Add (new StructureInstance<TypeMapModuleEntry> (map_entry));
			}

			mapModuleEntries.Sort ((StructureInstance<TypeMapModuleEntry> a, StructureInstance<TypeMapModuleEntry> b) => a.Obj.type_token_id.CompareTo (b.Obj.type_token_id));
			allModulesData.Add (new ModuleMapData (moduleDataSymbolLabel, mapModuleEntries));

			uint GetJavaEntryIndex (string javaTypeName)
			{
				if (!state.JavaTypesByName.TryGetValue (javaTypeName, out TypeMapJava javaType)) {
					throw new InvalidOperationException ($"INTERNAL ERROR: Java type '{javaTypeName}' not found in cache");
				}

				var key = new StructureInstance<TypeMapJava> (javaType);
				int idx = state.JavaMap.BinarySearch (key, javaNameHashComparer);
				if (idx < 0) {
					throw new InvalidOperationException ($"Could not map entry '{javaTypeName}' to array index");
				}

				return (uint)idx;
			}
		}

		// Generate hashes for all Java type names, then sort javaMap on the name hash.  This has to be done in the writing phase because hashes
		// will depend on architecture (or, actually, on its bitness) and may differ between architectures (they will be the same for all architectures
		// with the same bitness)
		(List<ModuleMapData> allMapModulesData, List<ulong> javaMapHashes) PrepareMapsForWriting (ArchGenerationState state, LlvmIrGenerator generator)
		{
			bool is64Bit = generator.Is64Bit;

			// Generate Java type name hashes...
			for (int i = 0; i < state.JavaMap.Count; i++) {
				TypeMapJava entry = state.JavaMap[i].Obj;
				entry.JavaNameHash = HashName (entry.JavaName);
			}

			// ...sort them...
			state.JavaMap.Sort ((StructureInstance<TypeMapJava> a, StructureInstance<TypeMapJava> b) => a.Obj.JavaNameHash.CompareTo (b.Obj.JavaNameHash));

			var allMapModulesData = new List<ModuleMapData> ();

			// ...and match managed types to Java...
			foreach (StructureInstance<TypeMapModule> moduleInstance in state.MapModules) {
				TypeMapModule module = moduleInstance.Obj;
				PrepareMapModuleData (state, module.MapSymbolName, module.Data.Types, allMapModulesData);
				if (module.Data.DuplicateTypes.Count > 0) {
					PrepareMapModuleData (state, module.DuplicateMapSymbolName, module.Data.DuplicateTypes, allMapModulesData);
				}
			}

			var javaMapHashes = new HashSet<ulong> ();
			foreach (StructureInstance<TypeMapJava> entry in state.JavaMap) {
				javaMapHashes.Add (entry.Obj.JavaNameHash);
			}

			return (allMapModulesData, javaMapHashes.ToList ());

			ulong HashName (string name)
			{
				if (name.Length == 0) {
					return UInt64.MaxValue;
				}

				// Native code (EmbeddedAssemblies::typemap_java_to_managed in embedded-assemblies.cc) will operate on wchar_t cast to a byte array, we need to do
				// the same
				return HashBytes (Encoding.Unicode.GetBytes (name));
			}

			ulong HashBytes (byte[] bytes)
			{
				if (is64Bit) {
					return XxHash64.HashToUInt64 (bytes);
				}

				return (ulong)XxHash32.HashToUInt32 (bytes);
			}
		}

		protected override void Write (LlvmIrGenerator generator)
		{
			ArchGenerationState state;

			try {
				state = archState[generator.TargetArch];
			} catch (KeyNotFoundException ex) {
				throw new InvalidOperationException ($"Internal error: architecture {generator.TargetArch} has not been prepared for writing.", ex);
			}

			generator.WriteVariable ("map_module_count", state.MappingData.MapModuleCount);
			generator.WriteVariable ("java_type_count", state.JavaMap.Count); // must include the padding item, if any

			(List<ModuleMapData> allMapModulesData, List<ulong> javaMapHashes) = PrepareMapsForWriting (state, generator);
			WriteMapModules (state, generator, allMapModulesData);
			WriteJavaMap (state, generator, javaMapHashes);
		}

		void WriteJavaMap (ArchGenerationState state, LlvmIrGenerator generator, List<ulong> javaMapHashes)
		{
			generator.WriteEOL ();
			generator.WriteEOL ("Java to managed map");

			generator.WriteStructureArray (
				typeMapJavaStructureInfo,
				state.JavaMap,
				LlvmIrVariableOptions.GlobalConstant,
				"map_java"
			);

			if (generator.Is64Bit) {
				WriteHashes (javaMapHashes);
			} else {
				// A bit ugly, but simple. We know that hashes are really 32-bit, so we can cast without
				// worrying.
				var hashes = new List<uint> (javaMapHashes.Count);
				foreach (ulong hash in javaMapHashes) {
					hashes.Add ((uint)hash);
				}
				WriteHashes (hashes);
			}

			generator.WriteArray (state.JavaNames, "java_type_names");

			void WriteHashes<T> (List<T> hashes) where T: struct
			{
				generator.WriteArray<T> (
					hashes,
					LlvmIrVariableOptions.GlobalConstant,
					"map_java_hashes",
					(int idx, T value) => $"{idx}: 0x{value:x} => {state.JavaMap[idx].Obj.JavaName}"
				);
			}
		}

		void WriteMapModules (ArchGenerationState state, LlvmIrGenerator generator, List<ModuleMapData> mapModulesData)
		{
			if (state.MapModules.Count == 0) {
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
				state.MapModules,
				LlvmIrVariableOptions.GlobalWritable,
				"map_modules"
			);
		}
	}
}
