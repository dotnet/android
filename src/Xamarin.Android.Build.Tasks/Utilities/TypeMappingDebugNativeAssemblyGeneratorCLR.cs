using System;
using System.Collections.Generic;

using Microsoft.Build.Utilities;

using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks;

class TypeMappingDebugNativeAssemblyGeneratorCLR : LlvmIrComposer
{
	const string JavaToManagedSymbol = "map_java_to_managed";
	const string ManagedToJavaSymbol = "map_managed_to_java";

	// These names MUST match src/native/clr/include/xamarin-app.hh
	const string TypeMapSymbol = "type_map";
	const string UniqueAssembliesSymbol = "type_map_unique_assemblies";
	const string AssemblyNamesBlobSymbol = "type_map_assembly_names";

	sealed class TypeMapContextDataProvider : NativeAssemblerStructContextDataProvider
	{
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
				return $" from: '{entry.From}'";
			}

			if (String.Compare ("to", fieldName, StringComparison.Ordinal) == 0) {
				return $" to: '{entry.To}'";
			}

			return String.Empty;
		}
	}

	sealed class TypeMapAssemblyContextDataProvider : NativeAssemblerStructContextDataProvider
	{
		public override string GetComment (object data, string fieldName)
		{
			var entry = EnsureType<TypeMapAssembly> (data);

			if (String.Compare ("mvid_hash", fieldName, StringComparison.Ordinal) == 0) {
				return $" MVID: {entry.MVID}";
			}

			if (String.Compare ("name_offset", fieldName, StringComparison.Ordinal) == 0) {
				return $" {entry.Name}";
			}

			return String.Empty;
		}
	}

	sealed class TypeMapManagedTypeInfoContextDataProvider : NativeAssemblerStructContextDataProvider
	{
		public override string GetComment (object data, string fieldName)
		{
			var entry = EnsureType<TypeMapManagedTypeInfo> (data);

			if (String.Compare ("assembly_name_index", fieldName, StringComparison.Ordinal) == 0) {
				return $" '{entry.AssemblyName}'";
			}

			if (String.Compare ("managed_type_token_id", fieldName, StringComparison.Ordinal) == 0) {
				return $" '{entry.ManagedTypeName}'";
			}

			return String.Empty;
		}
	}

	// Order of fields and their type must correspond *exactly* to that in
	// src/native/clr/include/xamarin-app.hh TypeMapEntry structure
	[NativeAssemblerStructContextDataProvider (typeof (TypeMapEntryContextDataProvider))]
	sealed class TypeMapEntry
	{
		[NativeAssembler (UsesDataProvider = true)]
		public string from;

		[NativeAssembler (UsesDataProvider = true)]
		public string to;
	};

	// Order of fields and their type must correspond *exactly* to that in
	// src/native/clr/include/xamarin-app.hh TypeMap structure
	[NativeAssemblerStructContextDataProvider (typeof (TypeMapContextDataProvider))]
	sealed class TypeMap
	{
		[NativeAssembler (Ignore = true)]
		public int    JavaToManagedCount;

		[NativeAssembler (Ignore = true)]
		public int    ManagedToJavaCount;

		public uint   entry_count;
		public ulong  unique_assemblies_count;

		[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToSymbol = "")]
		public TypeMapEntry? java_to_managed = null;

		[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToSymbol = "")]
		public TypeMapEntry? managed_to_java = null;
	};

	// Order of fields and their type must correspond *exactly* to that in
	// src/native/clr/include/xamarin-app.hh TypeMapAssembly structure
	[NativeAssemblerStructContextDataProvider (typeof (TypeMapAssemblyContextDataProvider))]
	sealed class TypeMapAssembly
	{
		[NativeAssembler (Ignore = true)]
		public string Name;

		[NativeAssembler (Ignore = true)]
		public Guid MVID;

		[NativeAssembler (UsesDataProvider = true, NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal)]
		public ulong mvid_hash;
		public ulong name_length;

		[NativeAssembler (UsesDataProvider = true)]
		public ulong name_offset;
	}

	readonly TypeMapGenerator.ModuleDebugData data;
	StructureInfo typeMapEntryStructureInfo;
	StructureInfo typeMapStructureInfo;
	StructureInfo typeMapAssemblyStructureInfo;
	List<StructureInstance<TypeMapEntry>> javaToManagedMap;
	List<StructureInstance<TypeMapEntry>> managedToJavaMap;
	List<StructureInstance<TypeMapAssembly>> uniqueAssemblies;
	StructureInstance<TypeMap> type_map;

	public TypeMappingDebugNativeAssemblyGeneratorCLR (TaskLoggingHelper log, TypeMapGenerator.ModuleDebugData data)
		: base (log)
	{
		if (data.UniqueAssemblies == null || data.UniqueAssemblies.Count == 0) {
			throw new InvalidOperationException ("Internal error: set of unique assemblies must be provided.");
		}

		this.data = data;

		javaToManagedMap = new ();
		managedToJavaMap = new ();
		uniqueAssemblies = new ();
		managedTypeInfos = new ();
	}

	protected override void Construct (LlvmIrModule module)
	{
		module.DefaultStringGroup = "tmd";

		MapStructures (module);

		var managedTypeNames = new LlvmIrStringBlob ();
		var javaTypeNames = new LlvmIrStringBlob ();

		// CoreCLR supports only 64-bit targets, so we can make things simpler by hashing all the things here instead of
		// in a callback during code generation
		var assemblyNamesBlob = new LlvmIrStringBlob ();
		foreach (TypeMapGenerator.TypeMapDebugAssembly asm in data.UniqueAssemblies) {
			(int assemblyNameOffset, int assemblyNameLength) = assemblyNamesBlob.Add (asm.Name);
			var entry = new TypeMapAssembly {
				Name = asm.Name,
				MVID = asm.MVID,

				mvid_hash = MonoAndroidHelper.GetXxHash (asm.MVIDBytes, is64Bit: true),
				name_length = (ulong)assemblyNameLength, // without the trailing NUL
				name_offset = (ulong)assemblyNameOffset,
			};
			uniqueAssemblies.Add (new StructureInstance<TypeMapAssembly> (typeMapAssemblyStructureInfo, entry));
		}
		uniqueAssemblies.Sort ((StructureInstance<TypeMapAssembly> a, StructureInstance<TypeMapAssembly> b) => a.Instance.mvid_hash.CompareTo (b.Instance.mvid_hash));

		var managedTypeInfos = new List<StructureInstance<TypeMapManagedTypeInfo>> ();
		// Java-to-managed maps don't use hashes since many mappings have multiple instances
		foreach (TypeMapGenerator.TypeMapDebugEntry entry in data.JavaToManagedMap) {
			TypeMapGenerator.TypeMapDebugEntry managedEntry = entry.DuplicateForJavaToManaged != null ? entry.DuplicateForJavaToManaged : entry;
			(int managedTypeNameOffset, int _) = managedTypeNames.Add (entry.ManagedName);
			(int javaTypeNameOffset, int _) = javaTypeNames.Add (entry.JavaName);

			var j2m = new TypeMapEntry {
				From = entry.JavaName,
				To = managedEntry.SkipInJavaToManaged ? String.Empty : entry.ManagedName,

				from = (uint)javaTypeNameOffset,
				from_hash = 0,
				to = managedEntry.SkipInJavaToManaged ? uint.MaxValue : (uint)managedTypeNameOffset,
			};
			javaToManagedMap.Add (new StructureInstance<TypeMapEntry> (typeMapEntryStructureInfo, j2m));

			int assemblyNameOffset = assemblyNamesBlob.GetIndexOf (entry.AssemblyName);
			if (assemblyNameOffset < 0) {
				throw new InvalidOperationException ($"Internal error: assembly name '{entry.AssemblyName}' not found in the assembly names blob.");
			}

			var typeInfo = new TypeMapManagedTypeInfo {
				AssemblyName = entry.AssemblyName,
				ManagedTypeName = entry.ManagedName,

				assembly_name_index = (uint)assemblyNameOffset,
				managed_type_token_id = entry.ManagedTypeTokenId,
			};
			managedTypeInfos.Add (new StructureInstance<TypeMapManagedTypeInfo> (typeMapManagedTypeInfoStructureInfo, typeInfo));
		}

		var map = new TypeMap {
			JavaToManagedCount = data.JavaToManagedMap == null ? 0 : data.JavaToManagedMap.Count,
			ManagedToJavaCount = data.ManagedToJavaMap == null ? 0 : data.ManagedToJavaMap.Count,

			entry_count = data.EntryCount,
			unique_assemblies_count = (ulong)data.UniqueAssemblies.Count,
			assembly_names_blob_size = (ulong)assemblyNamesBlob.Size,
		};
		type_map = new StructureInstance<TypeMap> (typeMapStructureInfo, map);

		module.AddGlobalVariable (TypeMapSymbol, type_map, LlvmIrVariableOptions.GlobalConstant);
		module.AddGlobalVariable (ManagedToJavaSymbol, managedToJavaMap, LlvmIrVariableOptions.LocalConstant);
		module.AddGlobalVariable (JavaToManagedSymbol, javaToManagedMap, LlvmIrVariableOptions.LocalConstant);
		module.AddGlobalVariable (TypeMapManagedTypeInfoSymbol, managedTypeInfos, LlvmIrVariableOptions.GlobalConstant);
		module.AddGlobalVariable (TypeMapUsesHashesSymbol, typemap_uses_hashes, LlvmIrVariableOptions.GlobalConstant);
		module.AddGlobalVariable (UniqueAssembliesSymbol, uniqueAssemblies, LlvmIrVariableOptions.GlobalConstant);
		module.AddGlobalVariable (AssemblyNamesBlobSymbol, assemblyNamesBlob, LlvmIrVariableOptions.GlobalConstant);
		module.AddGlobalVariable (ManagedTypeNamesBlobSymbol, managedTypeNames, LlvmIrVariableOptions.GlobalConstant);
		module.AddGlobalVariable (JavaTypeNamesBlobSymbol, javaTypeNames, LlvmIrVariableOptions.GlobalConstant);
	}

	void MapStructures (LlvmIrModule module)
	{
		typeMapAssemblyStructureInfo = module.MapStructure<TypeMapAssembly> ();
		typeMapEntryStructureInfo = module.MapStructure<TypeMapEntry> ();
		typeMapStructureInfo = module.MapStructure<TypeMap> ();
		typeMapManagedTypeInfoStructureInfo = module.MapStructure<TypeMapManagedTypeInfo> ();
	}
}
