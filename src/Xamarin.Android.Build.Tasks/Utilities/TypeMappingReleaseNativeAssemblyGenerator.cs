using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Text;

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
			public uint JavaNameHash32;

			[NativeAssembler (Ignore = true)]
			public ulong JavaNameHash64;

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
			public List<string> JavaNames;
			public List<StructureInstance<TypeMapJava>> JavaMap;
			public List<ModuleMapData> AllModulesData;
		}

		readonly NativeTypeMappingData mappingData;
		StructureInfo typeMapJavaStructureInfo;
		StructureInfo typeMapModuleStructureInfo;
		StructureInfo typeMapModuleEntryStructureInfo;
		JavaNameHash32Comparer javaNameHash32Comparer;
		JavaNameHash64Comparer javaNameHash64Comparer;

		ulong moduleCounter = 0;

		public TypeMappingReleaseNativeAssemblyGenerator (TaskLoggingHelper log, NativeTypeMappingData mappingData)
			: base (log)
		{
			this.mappingData = mappingData ?? throw new ArgumentNullException (nameof (mappingData));
			javaNameHash32Comparer = new JavaNameHash32Comparer ();
			javaNameHash64Comparer = new JavaNameHash64Comparer ();
		}

		protected override void Construct (LlvmIrModule module)
		{
			MapStructures (module);

			var cs = new ConstructionState ();
			cs.JavaTypesByName = new Dictionary<string, TypeMapJava> (StringComparer.Ordinal);
			cs.JavaNames = new List<string> ();
			InitJavaMap (cs);
			InitMapModules (cs);
			HashJavaNames (cs);
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
			IComparer<StructureInstance<TypeMapJava>> hashComparer = target.Is64Bit ? javaNameHash64Comparer : javaNameHash32Comparer;

			var entries = (List<StructureInstance<TypeMapModuleEntry>>)variable.Value;
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
				entry.JavaNameHash32 = (uint)HashName (entry.JavaName, is64Bit: false);
				hashes32.Add (entry.JavaNameHash32);

				entry.JavaNameHash64 = HashName (entry.JavaName, is64Bit: true);
				hashes64.Add (entry.JavaNameHash64);
			}

			ulong HashName (string name, bool is64Bit)
			{
				if (name.Length == 0) {
					return UInt64.MaxValue;
				}

				// Native code (EmbeddedAssemblies::typemap_java_to_managed in embedded-assemblies.cc) will operate on wchar_t cast to a byte array, we need to do
				// the same
				return HashBytes (Encoding.Unicode.GetBytes (name), is64Bit);
			}

			ulong HashBytes (byte[] bytes, bool is64Bit)
			{
				if (is64Bit) {
					return XxHash64.HashToUInt64 (bytes);
				}

				return (ulong)XxHash32.HashToUInt32 (bytes);
			}
		}
	}
}
