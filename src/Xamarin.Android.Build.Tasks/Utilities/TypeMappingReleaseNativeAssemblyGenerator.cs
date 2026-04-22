#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Build.Utilities;

using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks
{
	partial class TypeMappingReleaseNativeAssemblyGenerator : LlvmIrComposer
	{
		sealed class TypeMapModuleContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var map_module = EnsureType<TypeMapModule> (data);

				if (MonoAndroidHelper.StringEquals ("module_uuid", fieldName)) {
					return $" module_uuid: {map_module.MVID}";
				}

				if (MonoAndroidHelper.StringEquals ("assembly_name", fieldName)) {
					return $" assembly_name: {map_module.assembly_name}";
				}

				return String.Empty;
			}

			public override string? GetPointedToSymbolName (object data, string fieldName)
			{
				var map_module = EnsureType<TypeMapModule> (data);

				if (MonoAndroidHelper.StringEquals ("map", fieldName)) {
					return map_module.MapSymbolName;
				}

				if (MonoAndroidHelper.StringEquals ("duplicate_map", fieldName)) {
					return map_module.DuplicateMapSymbolName;
				}

				return null;
			}

			public override ulong GetBufferSize (object data, string fieldName)
			{
				var map_module = EnsureType<TypeMapModule> (data);

				if (MonoAndroidHelper.StringEquals ("map", fieldName)) {
					return map_module.entry_count;
				}

				if (MonoAndroidHelper.StringEquals ("duplicate_map", fieldName)) {
					return map_module.duplicate_count;
				}

				return base.GetBufferSize (data, fieldName);
			}
		}

		// This is here only to generate strongly-typed IR
		internal sealed class MonoImage
		{}

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh TypeMapModuleEntry structure
		sealed class TypeMapModuleEntry
		{
			[NativeAssembler (Ignore = true)]
			public TypeMapJava JavaTypeMapEntry;

			[NativeAssembler (NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal)]
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
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value - populated during native code generation
			public TypeMapModuleEntry map;
#pragma warning restore CS0649

			[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToSymbol = "")]
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value - populated during native code generation
			public TypeMapModuleEntry duplicate_map;
#pragma warning restore CS0649

			[NativeAssembler (UsesDataProvider = true)]
			public string assembly_name;

			[NativePointer (IsNull = true)]
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value - populated during native code generation
			public MonoImage image;
#pragma warning restore CS0649
			public uint   java_name_width;

			[NativePointer (IsNull = true)]
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value - populated during native code generation
			public byte java_map;
#pragma warning restore CS0649
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

			[NativeAssembler (NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal)]
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

		sealed class JavaNameHashComparer : IComparer<StructureInstance<TypeMapJava>>
		{
			public int Compare (StructureInstance<TypeMapJava> a, StructureInstance<TypeMapJava> b)
			{
				return a.Instance.JavaNameHash.CompareTo (b.Instance.JavaNameHash);
			}
		}

		sealed class ConstructionState
		{
			public List<StructureInstance<TypeMapModule>> MapModules;
			public Dictionary<string, TypeMapJava> JavaTypesByName;
			public List<string> JavaNames;
			public List<StructureInstance<TypeMapJava>> JavaMap;
			public List<ModuleMapData> AllModulesData;
		}

		readonly NativeTypeMappingData mappingData;
		StructureInfo typeMapJavaStructureInfo;
		StructureInfo typeMapModuleStructureInfo;
		StructureInfo typeMapModuleEntryStructureInfo;
		JavaNameHashComparer javaNameHashComparer;

		ulong moduleCounter = 0;

		public TypeMappingReleaseNativeAssemblyGenerator (TaskLoggingHelper log, NativeTypeMappingData mappingData)
			: base (log)
		{
			this.mappingData = mappingData ?? throw new ArgumentNullException (nameof (mappingData));
			javaNameHashComparer = new JavaNameHashComparer ();
		}

		protected override void Construct (LlvmIrModule module)
		{
			module.DefaultStringGroup = "tmr";

			MapStructures (module);

			var cs = new ConstructionState ();
			cs.JavaTypesByName = new Dictionary<string, TypeMapJava> (StringComparer.Ordinal);
			cs.JavaNames = new List<string> ();
			InitJavaMap (cs);
			InitMapModules (cs);
			PrepareModules (cs);

			module.AddGlobalVariable ("map_module_count", mappingData.MapModuleCount);
			module.AddGlobalVariable ("java_type_count", cs.JavaMap.Count);

			var map_modules = new LlvmIrGlobalVariable (cs.MapModules, "map_modules", LlvmIrVariableOptions.GlobalWritable) {
				Comment = " Managed modules map",
			};
			module.Add (map_modules);

			// Java hashes are output bafore Java type map **and** managed modules, because they will also sort the Java map for us.
			// This is not strictly necessary, as we could do the sorting in the java map BeforeWriteCallback, but this way we save
			// time sorting only once.
			var map_java_hashes = new LlvmIrGlobalVariable (typeof(List<ulong>), "map_java_hashes") {
				Comment = " Java types name hashes",
				BeforeWriteCallback = GenerateAndSortJavaHashes,
				BeforeWriteCallbackCallerState = cs,
				GetArrayItemCommentCallback = GetJavaHashesItemComment,
				GetArrayItemCommentCallbackCallerState = cs,
				NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal,
			};
			map_java_hashes.WriteOptions &= ~LlvmIrVariableWriteOptions.ArrayWriteIndexComments;
			module.Add (map_java_hashes);

			foreach (ModuleMapData mmd in cs.AllModulesData) {
				var mmdVar = new LlvmIrGlobalVariable (mmd.Entries, mmd.SymbolLabel, LlvmIrVariableOptions.LocalConstant) {
					BeforeWriteCallback = UpdateJavaIndexes,
					BeforeWriteCallbackCallerState = cs,
				};
				module.Add (mmdVar);
			}

			module.AddGlobalVariable ("map_java", cs.JavaMap, LlvmIrVariableOptions.GlobalConstant, " Java to managed map");
			module.AddGlobalVariable ("java_type_names", cs.JavaNames, LlvmIrVariableOptions.GlobalConstant, " Java type names");
		}

		void UpdateJavaIndexes (LlvmIrVariable variable, LlvmIrModuleTarget target, object? callerState)
		{
			ConstructionState cs = EnsureConstructionState (callerState);
			LlvmIrGlobalVariable gv = EnsureGlobalVariable (variable);

			var entries = (List<StructureInstance<TypeMapModuleEntry>>)variable.Value;
			foreach (StructureInstance<TypeMapModuleEntry> entry in entries) {
				entry.Instance.java_map_index = GetJavaEntryIndex (entry.Instance.JavaTypeMapEntry);
			}

			uint GetJavaEntryIndex (TypeMapJava javaEntry)
			{
				var key = new StructureInstance<TypeMapJava> (typeMapJavaStructureInfo, javaEntry);
				int idx = cs.JavaMap.BinarySearch (key, javaNameHashComparer);
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

			for (int i = 0; i < cs.JavaMap.Count; i++) {
				TypeMapJava entry = cs.JavaMap[i].Instance;
				entry.JavaNameHash = TypeMapHelper.HashJavaName (entry.JavaName, target.Is64Bit);
			}

			cs.JavaMap.Sort ((StructureInstance<TypeMapJava> a, StructureInstance<TypeMapJava> b) => a.Instance.JavaNameHash.CompareTo (b.Instance.JavaNameHash));

			Type listType;
			IList hashes;
			if (target.Is64Bit) {
				listType = typeof(List<ulong>);
				var list = new List<ulong> ();
				foreach (StructureInstance<TypeMapJava> si in cs.JavaMap) {
					list.Add (si.Instance.JavaNameHash);
				}
				hashes = list;
			} else {
				listType = typeof(List<uint>);
				var list = new List<uint> ();
				foreach (StructureInstance<TypeMapJava> si in cs.JavaMap) {
					list.Add ((uint)si.Instance.JavaNameHash);
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

		void InitJavaMap (ConstructionState cs)
		{
			cs.JavaMap = new List<StructureInstance<TypeMapJava>> ();
			TypeMapJava map_entry;
			foreach (TypeMapGenerator.TypeMapReleaseEntry entry in mappingData.JavaTypes) {
				cs.JavaNames.Add (entry.JavaName);

				map_entry = new TypeMapJava {
					module_index = (uint)entry.ModuleIndex, // UInt32.MaxValue,
					type_token_id = entry.SkipInJavaToManaged ? 0 : entry.Token,
					java_name_index = (uint)(cs.JavaNames.Count - 1),
					JavaName = entry.JavaName,
				};

				cs.JavaMap.Add (new StructureInstance<TypeMapJava> (typeMapJavaStructureInfo, map_entry));
				cs.JavaTypesByName.Add (map_entry.JavaName, map_entry);
			}
		}

		void InitMapModules (ConstructionState cs)
		{
			cs.MapModules = new List<StructureInstance<TypeMapModule>> ();
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

				cs.MapModules.Add (new StructureInstance<TypeMapModule> (typeMapModuleStructureInfo, map_module));
			}
		}

		void MapStructures (LlvmIrModule module)
		{
			typeMapJavaStructureInfo = module.MapStructure<TypeMapJava> ();
			typeMapModuleStructureInfo = module.MapStructure<TypeMapModule> ();
			typeMapModuleEntryStructureInfo = module.MapStructure<TypeMapModuleEntry> ();
		}

		void PrepareMapModuleData (string moduleDataSymbolLabel, IEnumerable<TypeMapGenerator.TypeMapReleaseEntry> moduleEntries, ConstructionState cs)
		{
			var mapModuleEntries = new List<StructureInstance<TypeMapModuleEntry>> ();
			foreach (TypeMapGenerator.TypeMapReleaseEntry entry in moduleEntries) {
				if (!cs.JavaTypesByName.TryGetValue (entry.JavaName, out TypeMapJava javaType)) {
					throw new InvalidOperationException ($"Internal error: Java type '{entry.JavaName}' not found in cache");
				}

				var map_entry = new TypeMapModuleEntry {
					JavaTypeMapEntry = javaType,
					type_token_id = entry.Token,
					java_map_index = UInt32.MaxValue, // will be set later, when the target is known
				};
				mapModuleEntries.Add (new StructureInstance<TypeMapModuleEntry> (typeMapModuleEntryStructureInfo, map_entry));
			}

			mapModuleEntries.Sort ((StructureInstance<TypeMapModuleEntry> a, StructureInstance<TypeMapModuleEntry> b) => a.Instance.type_token_id.CompareTo (b.Instance.type_token_id));
			cs.AllModulesData.Add (new ModuleMapData (moduleDataSymbolLabel, mapModuleEntries));
		}

		void PrepareModules (ConstructionState cs)
		{
			cs.AllModulesData = new List<ModuleMapData> ();
			foreach (StructureInstance<TypeMapModule> moduleInstance in cs.MapModules) {
				TypeMapModule module = moduleInstance.Instance;
				PrepareMapModuleData (module.MapSymbolName, module.Data.Types, cs);
				if (module.Data.DuplicateTypes.Count > 0) {
					PrepareMapModuleData (module.DuplicateMapSymbolName, module.Data.DuplicateTypes, cs);
				}
			}
		}

	}
}
