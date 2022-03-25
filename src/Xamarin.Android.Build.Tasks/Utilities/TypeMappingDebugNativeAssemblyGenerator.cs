using System;
using System.Collections.Generic;

using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks
{
	class TypeMappingDebugNativeAssemblyGenerator : TypeMappingAssemblyGenerator
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

		public TypeMappingDebugNativeAssemblyGenerator (TypeMapGenerator.ModuleDebugData data)
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
}
