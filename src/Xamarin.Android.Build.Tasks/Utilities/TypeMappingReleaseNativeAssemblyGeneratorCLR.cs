#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Build.Utilities;

using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks
{
	partial class TypeMappingReleaseNativeAssemblyGeneratorCLR : LlvmIrComposer
	{
		sealed class TypeMapModuleContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var map_module = EnsureType<TypeMapModule> (data);

				if (String.Compare ("module_uuid", fieldName, StringComparison.Ordinal) == 0) {
					return $" module_uuid: {map_module.MVID}";
				}

				if (String.Compare ("assembly_name_index", fieldName, StringComparison.Ordinal) == 0) {
					return $" assembly_name: {map_module.AssemblyName}";
				}

				return String.Empty;
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
			public override string GetComment (object data, string fieldName)
			{
				var java_map_entry = EnsureType<TypeMapJava> (data);

				if (String.Compare ("managed_type_name_index", fieldName, StringComparison.Ordinal) == 0) {
					return $" managed type name: {java_map_entry.ManagedTypeName}";
				}

				if (String.Compare ("java_name_index", fieldName, StringComparison.Ordinal) == 0) {
					return $" Java type name: {java_map_entry.JavaName}";
				}

				return String.Empty;
			}
		};

		sealed class TypeMapModuleEntryContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var module_map_entry = EnsureType<TypeMapModuleEntry> (data);

				if (String.Compare ("managed_type_name_hash_32", fieldName, StringComparison.Ordinal) == 0 ||
				    String.Compare ("managed_type_name_hash_64", fieldName, StringComparison.Ordinal) == 0) {
					return $" managed type name: {module_map_entry.ManagedTypeName}";
				}

				if (String.Compare ("java_map_index", fieldName, StringComparison.Ordinal) == 0) {
					return $" Java type name: {module_map_entry.JavaTypeMapEntry.JavaName}";
				}

				return String.Empty;
			}
		};

		// Order of fields and their type must correspond *exactly* to that in
		// src/native/clr/include/xamarin-app.hh TypeMapModuleEntry structure
		[NativeAssemblerStructContextDataProvider (typeof (TypeMapModuleEntryContextDataProvider))]
		sealed class TypeMapModuleEntry
		{
			[NativeAssembler (Ignore = true)]
			public TypeMapJava JavaTypeMapEntry;

			[NativeAssembler (Ignore = true)]
			public string ManagedTypeName;

			[NativeAssembler (UsesDataProvider = true, ValidTarget = NativeAssemblerValidTarget.ThirtyTwoBit, MemberName = "managed_type_name_hash", NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal)]
			public uint managed_type_name_hash_32;

			[NativeAssembler (UsesDataProvider = true, ValidTarget = NativeAssemblerValidTarget.SixtyFourBit, MemberName = "managed_type_name_hash", NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal)]
			public ulong managed_type_name_hash_64;

			[NativeAssembler (UsesDataProvider = true)]
			public uint java_map_index;
		}

		// Order of fields and their type must correspond *exactly* to that in
		// src/native/clr/include/xamarin-app.hh TypeMapModule structure
		[NativeAssemblerStructContextDataProvider (typeof (TypeMapModuleContextDataProvider))]
		sealed class TypeMapModule
		{
			[NativeAssembler (Ignore = true)]
			public Guid    MVID;

			[NativeAssembler (Ignore = true)]
			public TypeMapGenerator.ModuleReleaseData Data;

			[NativeAssembler (Ignore = true)]
			public string AssemblyName;

			[NativeAssembler (UsesDataProvider = true, InlineArray = true, InlineArraySize = 16)]
			public byte[]  module_uuid;
			public uint    entry_count;
			public uint    duplicate_count;

			[NativeAssembler (UsesDataProvider = true)]
			public uint    assembly_name_index;
			public uint    assembly_name_length;

			[NativeAssembler (UsesDataProvider = true)]
			public uint map_index;

			[NativeAssembler (UsesDataProvider = true)]
			public uint duplicate_map_index;
		}

		// Order of fields and their type must correspond *exactly* to that in
		// src/native/clr/include/xamarin-app.hh TypeMapJava structure
		[NativeAssemblerStructContextDataProvider (typeof (TypeMapJavaContextDataProvider))]
		sealed class TypeMapJava
		{
			[NativeAssembler (Ignore = true)]
			public string JavaName;

			[NativeAssembler (Ignore = true)]
			public string ManagedTypeName;

			[NativeAssembler (Ignore = true)]
			public uint JavaNameHash32;

			[NativeAssembler (Ignore = true)]
			public ulong JavaNameHash64;

			public uint module_index;

			[NativeAssembler (UsesDataProvider = true)]
			public uint managed_type_name_index;
			public uint managed_type_name_length;

			[NativeAssembler (NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal)]
			public uint managed_type_token_id;

			[NativeAssembler (UsesDataProvider = true)]
			public uint java_name_index;
			public uint java_name_length;
		}

		sealed class ModuleMapData
		{
			public List<StructureInstance<TypeMapModuleEntry>> Entries { get; }

			public ModuleMapData (List<StructureInstance<TypeMapModuleEntry>> entries)
			{
				Entries = entries;
			}
		}

		sealed class JavaNameHash32Comparer : IComparer<StructureInstance<TypeMapJava>>
		{
			public int Compare (StructureInstance<TypeMapJava> a, StructureInstance<TypeMapJava> b)
			{
				return a.Instance.JavaNameHash32.CompareTo (b.Instance.JavaNameHash32);
			}
		}

		sealed class JavaNameHash64Comparer : IComparer<StructureInstance<TypeMapJava>>
		{
			public int Compare (StructureInstance<TypeMapJava> a, StructureInstance<TypeMapJava> b)
			{
				return a.Instance.JavaNameHash64.CompareTo (b.Instance.JavaNameHash64);
			}
		}

		sealed class ConstructionState
		{
			public List<StructureInstance<TypeMapModule>> MapModules;
			public Dictionary<string, TypeMapJava> JavaTypesByName;
			public List<StructureInstance<TypeMapJava>> JavaMap;
			public List<ModuleMapData> AllModulesData;
			public List<List<StructureInstance<TypeMapModuleEntry>>> AllModulesMaps;
			public List<List<StructureInstance<TypeMapModuleEntry>>> AllModulesDuplicates;
			public LlvmIrStringBlob AssemblyNamesBlob;
			public LlvmIrStringBlob JavaTypeNamesBlob;
			public LlvmIrStringBlob ManagedTypeNamesBlob;
		}

		readonly NativeTypeMappingData mappingData;
		StructureInfo typeMapJavaStructureInfo;
		StructureInfo typeMapModuleStructureInfo;
		StructureInfo typeMapModuleEntryStructureInfo;
		JavaNameHash32Comparer javaNameHash32Comparer;
		JavaNameHash64Comparer javaNameHash64Comparer;

		ulong moduleCounter = 0;

		public TypeMappingReleaseNativeAssemblyGeneratorCLR (TaskLoggingHelper log, NativeTypeMappingData mappingData)
			: base (log)
		{
			this.mappingData = mappingData ?? throw new ArgumentNullException (nameof (mappingData));
			javaNameHash32Comparer = new JavaNameHash32Comparer ();
			javaNameHash64Comparer = new JavaNameHash64Comparer ();

			// Unfortunate, but we have to fix this up before proceeding
			foreach (TypeMapGenerator.ModuleReleaseData module in mappingData.Modules) {
				foreach (TypeMapGenerator.TypeMapReleaseEntry entry in module.Types) {
					if (entry.ManagedTypeName.IndexOf ('/') < 0) {
						continue;
					}

					// CoreCLR will request for subtypes with the `+` level separator instead of
					// the `/` one.
					entry.ManagedTypeName = entry.ManagedTypeName.Replace ('/', '+');
				}
			}
		}

		protected override void Construct (LlvmIrModule module)
		{
			module.DefaultStringGroup = "tmr";

			MapStructures (module);

			var cs = new ConstructionState ();
			cs.JavaTypesByName = new Dictionary<string, TypeMapJava> (StringComparer.Ordinal);
			InitJavaMap (cs);
			InitMapModules (cs);
			HashJavaNames (cs);
			PrepareModules (cs);

			module.AddGlobalVariable ("managed_to_java_map_module_count", mappingData.MapModuleCount);
			module.AddGlobalVariable ("java_type_count", cs.JavaMap.Count);

			var managed_to_java_map = new LlvmIrGlobalVariable (cs.MapModules, "managed_to_java_map", LlvmIrVariableOptions.GlobalWritable) {
				Comment = " Managed modules map",
			};
			module.Add (managed_to_java_map);

			// Java hashes are output bafore Java type map **and** managed modules, because they will also sort the Java map for us.
			// This is not strictly necessary, as we could do the sorting in the java map BeforeWriteCallback, but this way we save
			// time sorting only once.
			var java_to_managed_hashes = new LlvmIrGlobalVariable (typeof(List<ulong>), "java_to_managed_hashes") {
				Comment = " Java types name hashes",
				BeforeWriteCallback = GenerateAndSortJavaHashes,
				BeforeWriteCallbackCallerState = cs,
				GetArrayItemCommentCallback = GetJavaHashesItemComment,
				GetArrayItemCommentCallbackCallerState = cs,
				NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal,
			};
			java_to_managed_hashes.WriteOptions &= ~LlvmIrVariableWriteOptions.ArrayWriteIndexComments;
			module.Add (java_to_managed_hashes);

			// foreach (ModuleMapData mmd in cs.AllModulesData) {
			// 	var mmdVar = new LlvmIrGlobalVariable (mmd.Entries, mmd.SymbolLabel, LlvmIrVariableOptions.LocalConstant) {
			// 		BeforeWriteCallback = SortEntriesAndUpdateJavaIndexes,
			// 		BeforeWriteCallbackCallerState = cs,
			// 	};
			// 	module.Add (mmdVar);
			// }

			var modulesMapData = new LlvmIrGlobalVariable (cs.AllModulesMaps, "modules_map_data", LlvmIrVariableOptions.GlobalConstant) {
				BeforeWriteCallback = SortEntriesAndUpdateJavaIndexes,
				BeforeWriteCallbackCallerState = cs,
			};
			module.Add (modulesMapData);

			var modulesDuplicatesData = new LlvmIrGlobalVariable (cs.AllModulesMaps, "modules_duplicates_data", LlvmIrVariableOptions.GlobalConstant) {
				BeforeWriteCallback = SortEntriesAndUpdateJavaIndexes,
				BeforeWriteCallbackCallerState = cs,
			};
			module.Add (modulesMapData);

			module.AddGlobalVariable ("java_to_managed_map", cs.JavaMap, LlvmIrVariableOptions.GlobalConstant, " Java to managed map");
			module.AddGlobalVariable ("java_type_names", cs.JavaTypeNamesBlob, LlvmIrVariableOptions.GlobalConstant, " Java type names");
			module.AddGlobalVariable ("java_type_names_size", (ulong)cs.JavaTypeNamesBlob.Size, LlvmIrVariableOptions.GlobalConstant, " Java type names blob size");
			module.AddGlobalVariable ("managed_type_names", cs.ManagedTypeNamesBlob, LlvmIrVariableOptions.GlobalConstant, " Managed type names");
			module.AddGlobalVariable ("managed_assembly_names", cs.AssemblyNamesBlob, LlvmIrVariableOptions.GlobalConstant, " Managed assembly names");
		}

		void SortEntriesAndUpdateJavaIndexes (LlvmIrVariable variable, LlvmIrModuleTarget target, object? callerState)
		{
			ConstructionState cs = EnsureConstructionState (callerState);
			LlvmIrGlobalVariable gv = EnsureGlobalVariable (variable);
			IComparer<StructureInstance<TypeMapJava>> hashComparer = target.Is64Bit ? javaNameHash64Comparer : javaNameHash32Comparer;

			var entries = (List<StructureInstance<TypeMapModuleEntry>>)variable.Value;
			if (target.Is64Bit) {
				entries.Sort (
					(StructureInstance<TypeMapModuleEntry> a, StructureInstance<TypeMapModuleEntry> b) => a.Instance.managed_type_name_hash_64.CompareTo (b.Instance.managed_type_name_hash_64)
				);
			} else {
				entries.Sort (
					(StructureInstance<TypeMapModuleEntry> a, StructureInstance<TypeMapModuleEntry> b) => a.Instance.managed_type_name_hash_32.CompareTo (b.Instance.managed_type_name_hash_32)
				);
			}

			foreach (StructureInstance<TypeMapModuleEntry> entry in entries) {
				entry.Instance.java_map_index = GetJavaEntryIndex (entry.Instance.JavaTypeMapEntry);
			}

			uint GetJavaEntryIndex (TypeMapJava javaEntry)
			{
				var key = new StructureInstance<TypeMapJava> (typeMapJavaStructureInfo, javaEntry);
				int idx = cs.JavaMap.BinarySearch (key, hashComparer);
				if (idx < 0) {
					throw new InvalidOperationException ($"Could not map entry '{javaEntry.JavaName}' to array index");
				}

				return (uint)idx;
			}
		}

		string? GetJavaHashesItemComment (LlvmIrVariable v, LlvmIrModuleTarget target, ulong index, object? value, object? callerState)
		{
			var cs = callerState as ConstructionState;
			if (cs == null) {
				throw new InvalidOperationException ("Internal error: construction state expected but not found");
			}

			return $" {index} => {cs.JavaMap[(int)index].Instance.JavaName}";
		}

		void GenerateAndSortJavaHashes (LlvmIrVariable variable, LlvmIrModuleTarget target, object? callerState)
		{
			ConstructionState cs = EnsureConstructionState (callerState);
			LlvmIrGlobalVariable gv = EnsureGlobalVariable (variable);
			Type listType;
			IList hashes;
			if (target.Is64Bit) {
				listType = typeof(List<ulong>);
				cs.JavaMap.Sort ((StructureInstance<TypeMapJava> a, StructureInstance<TypeMapJava> b) => a.Instance.JavaNameHash64.CompareTo (b.Instance.JavaNameHash64));

				var list = new List<ulong> ();
				foreach (StructureInstance<TypeMapJava> si in cs.JavaMap) {
					list.Add (si.Instance.JavaNameHash64);
				}
				hashes = list;
			} else {
				listType = typeof(List<uint>);
				cs.JavaMap.Sort ((StructureInstance<TypeMapJava> a, StructureInstance<TypeMapJava> b) => a.Instance.JavaNameHash32.CompareTo (b.Instance.JavaNameHash32));

				var list = new List<uint> ();
				foreach (StructureInstance<TypeMapJava> si in cs.JavaMap) {
					list.Add (si.Instance.JavaNameHash32);
				}
				hashes = list;
			}

			gv.OverrideTypeAndValue (listType, hashes);
		}

		ConstructionState EnsureConstructionState (object? callerState)
		{
			var cs = callerState as ConstructionState;
			if (cs == null) {
				throw new InvalidOperationException ("Internal error: construction state expected but not found");
			}

			return cs;
		}

		uint GetEntryIndex (string entryValue, Dictionary<string, uint> seenCache, List<string> entryList)
		{
			if (!seenCache.TryGetValue (entryValue, out uint assemblyNameIndex)) {
				entryList.Add (entryValue);
				assemblyNameIndex = (uint)(entryList.Count - 1);
				seenCache.Add (entryValue, assemblyNameIndex);
			}

			return assemblyNameIndex;
		}

		void InitJavaMap (ConstructionState cs)
		{
			var seenManagedTypeNames = new Dictionary<string, uint> (StringComparer.Ordinal);
			cs.JavaMap = new List<StructureInstance<TypeMapJava>> ();
			cs.ManagedTypeNamesBlob = new ();
			cs.JavaTypeNamesBlob = new ();

			TypeMapJava map_entry;
			foreach (TypeMapGenerator.TypeMapReleaseEntry entry in mappingData.JavaTypes) {
				string assemblyName = mappingData.Modules[entry.ModuleIndex].AssemblyName;
				(int managedTypeNameIndex, int managedTypeNameLength) = cs.ManagedTypeNamesBlob.Add (entry.ManagedTypeName);
				(int javaTypeNameIndex, int javaTypeNameLength) = cs.JavaTypeNamesBlob.Add (entry.JavaName);

				map_entry = new TypeMapJava {
					ManagedTypeName = entry.ManagedTypeName,

					module_index = (uint)entry.ModuleIndex, // UInt32.MaxValue,
					managed_type_name_index = (uint)managedTypeNameIndex,
					managed_type_name_length = (uint)managedTypeNameLength,
					managed_type_token_id = entry.Token,
					java_name_index = (uint)javaTypeNameIndex,
					java_name_length = (uint)javaTypeNameLength,
					JavaName = entry.JavaName,
				};

				cs.JavaMap.Add (new StructureInstance<TypeMapJava> (typeMapJavaStructureInfo, map_entry));
				cs.JavaTypesByName.Add (map_entry.JavaName, map_entry);
			}
		}

		void InitMapModules (ConstructionState cs)
		{
			var seenAssemblyNames = new Dictionary<string, uint> (StringComparer.OrdinalIgnoreCase);

			cs.MapModules = new List<StructureInstance<TypeMapModule>> ();
			cs.AssemblyNamesBlob = new ();
			cs.AllModulesMaps = new ();
			cs.AllModulesDuplicates = new ();

			uint map_start_index = 0;
			uint duplicates_start_index = 0;
			foreach (TypeMapGenerator.ModuleReleaseData data in mappingData.Modules) {
				bool haveDuplicates = data.DuplicateTypes.Count > 0;
				(int assemblyNameIndex, int assemblyNameLength) = cs.AssemblyNamesBlob.Add (data.AssemblyName);

				var map_module = new TypeMapModule {
					MVID = data.Mvid,
					Data = data,
					AssemblyName = data.AssemblyName,

					module_uuid = data.MvidBytes,
					entry_count = (uint)data.Types.Length,
					duplicate_count = (uint)data.DuplicateTypes.Count,
					assembly_name_index = (uint)assemblyNameIndex,
					assembly_name_length = (uint)assemblyNameLength,
					map_index = map_start_index,
					duplicate_map_index = haveDuplicates ? duplicates_start_index : 0,
				};

				map_start_index += map_module.entry_count;
				duplicates_start_index += map_module.duplicate_count;

				cs.MapModules.Add (new StructureInstance<TypeMapModule> (typeMapModuleStructureInfo, map_module));
			}
		}

		void MapStructures (LlvmIrModule module)
		{
			typeMapJavaStructureInfo = module.MapStructure<TypeMapJava> ();
			typeMapModuleStructureInfo = module.MapStructure<TypeMapModule> ();
			typeMapModuleEntryStructureInfo = module.MapStructure<TypeMapModuleEntry> ();
		}

		void PrepareMapModuleData (IEnumerable<TypeMapGenerator.TypeMapReleaseEntry> moduleEntries, List<List<StructureInstance<TypeMapModuleEntry>>> destCollection, string sectionComment, ConstructionState cs)
		{
			var mapModuleEntries = new List<StructureInstance<TypeMapModuleEntry>> ();
			foreach (TypeMapGenerator.TypeMapReleaseEntry entry in moduleEntries) {
				if (!cs.JavaTypesByName.TryGetValue (entry.JavaName, out TypeMapJava javaType)) {
					throw new InvalidOperationException ($"Internal error: Java type '{entry.JavaName}' not found in cache");
				}

				mapModuleEntries.Add (new StructureInstance<TypeMapModuleEntry> (typeMapModuleEntryStructureInfo, sectionComment));
				var map_entry = new TypeMapModuleEntry {
					JavaTypeMapEntry = javaType,
					ManagedTypeName = entry.ManagedTypeName,

					managed_type_name_hash_32 = (uint)MonoAndroidHelper.GetXxHash (entry.ManagedTypeName, is64Bit: false),
					managed_type_name_hash_64 = MonoAndroidHelper.GetXxHash (entry.ManagedTypeName, is64Bit: true),
					java_map_index = UInt32.MaxValue, // will be set later, when the target is known
				};
				mapModuleEntries.Add (new StructureInstance<TypeMapModuleEntry> (typeMapModuleEntryStructureInfo, map_entry));
			}
			destCollection.Add (mapModuleEntries);
		}

		void PrepareModules (ConstructionState cs)
		{
			cs.AllModulesData = new List<ModuleMapData> ();
			foreach (StructureInstance<TypeMapModule> moduleInstance in cs.MapModules) {
				TypeMapModule module = moduleInstance.Instance;
				PrepareMapModuleData (
					module.Data.Types,
					cs.AllModulesMaps,
					$"Module: {module.AssemblyName}; MVID: {module.MVID}; number of entries: {module.Data.Types.Length}",
					cs
				);
				if (module.Data.DuplicateTypes.Count > 0) {
					PrepareMapModuleData (
						module.Data.DuplicateTypes,
						cs.AllModulesDuplicates,
						$"Module: {module.AssemblyName}; MVID: {module.MVID}; number of entries: {module.Data.DuplicateTypes.Count}",
						cs
					);
				}
			}
		}

		void HashJavaNames (ConstructionState cs)
		{
			// We generate both 32-bit and 64-bit hashes at the construction time.  Which set will be used depends on the target.
			// Java map list will also be sorted when the target is known
			var hashes32 = new HashSet<uint> ();
			var hashes64 = new HashSet<ulong> ();

			// Generate Java type name hashes...
			for (int i = 0; i < cs.JavaMap.Count; i++) {
				TypeMapJava entry = cs.JavaMap[i].Instance;

				// The cast is safe, xxHash will return a 32-bit value which (for convenience) was upcast to 64-bit
				entry.JavaNameHash32 = (uint)TypeMapHelper.HashJavaNameForCLR (entry.JavaName, is64Bit: false);
				hashes32.Add (entry.JavaNameHash32);

				entry.JavaNameHash64 = TypeMapHelper.HashJavaNameForCLR (entry.JavaName, is64Bit: true);
				hashes64.Add (entry.JavaNameHash64);
			}
		}
	}
}
