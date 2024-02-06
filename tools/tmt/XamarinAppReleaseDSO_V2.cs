using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Text;

using ELFSharp.ELF.Sections;
using Xamarin.Android.Tasks;

namespace tmt;

class XamarinAppReleaseDSO_V2 : XamarinAppReleaseDSO_Version
{
	protected override string LogTag => "ReleaseDSO_V2";

	// Field names correspond to: src/monodroid/jni/xamarin-app.hh (struct TypeMapModuleEntry)
	sealed class TypeMapModuleEntry
	{
		public uint type_token_id;
		public uint java_map_index;
	}

	// Field names correspond to: src/monodroid/jni/xamarin-app.hh (struct TypeMapModule)
	sealed class TypeMapModule
	{
		public Guid module_uuid;
		public uint entry_count;
		public uint duplicate_count;
		public List<TypeMapModuleEntry>? map;
		public List<TypeMapModuleEntry>? duplicate_map;
		public string assembly_name = String.Empty;

		// These three aren't used, listed for completeness
		public readonly object? image = null;
		public readonly uint java_name_width = 0;
		public readonly byte[]? java_map = null;
	}

	// Field names correspond to: src/monodroid/jni/xamarin-app.hh (struct TypeMapJava)
	sealed class TypeMapJava
	{
		public uint module_index;
		public uint type_token_id;
		public uint java_name_index;
	}

	const string MapModulesSymbolName    = "map_modules";
	const string ModuleCountSymbolName   = "map_module_count";
	const string JavaTypeCountSymbolName = "java_type_count";
	const string JavaTypeNamesSymbolName = "java_type_names";
	const string MapJavaSymbolName       = "map_java";
	const string MapJavaHashesSymbolName = "map_java_hashes";

	Map? map;
	List<TypeMapModule>? modules;
	List<TypeMapJava>? javaTypes;
	List<string>? javaTypeNames;
	List<ulong>? javaTypeNameHashes;

	public override string FormatVersion => "2";
	public override Map Map => map ?? throw new InvalidOperationException ("Data hasn't been loaded yet");

	public XamarinAppReleaseDSO_V2 (ManagedTypeResolver managedResolver, AnELF elf)
		: base (managedResolver, elf)
	{}

	public override bool CanLoad (AnELF elf)
	{
		return HasSymbol (elf, MapModulesSymbolName) &&
		       HasSymbol (elf, ModuleCountSymbolName) &&
		       HasSymbol (elf, JavaTypeCountSymbolName) &&
		       HasSymbol (elf, JavaTypeNamesSymbolName) &&
		       HasSymbol (elf, MapJavaSymbolName) &&
		       HasSymbol (elf, MapJavaHashesSymbolName);
	}

	protected override bool LoadMaps ()
	{
		try {
			string filePath = ELF.FilePath;
			modules = LoadMapModules (filePath);

			// Order in which the entries are loaded is important.  Farther loads use data gathered in the preceding ones
			javaTypes = LoadJavaTypes (filePath);
			javaTypeNameHashes = LoadJavaTypeNameHashes (filePath);
			javaTypeNames = LoadJavaTypeNames (filePath);
			return true;
		} catch (Exception ex) {
			Log.ExceptionError ($"{Description}: failed to load maps", ex);
			return false;
		}
	}

	protected override void SaveRaw (string baseOutputFilePath, string extension)
	{
		const string indent = "\t";

		if (modules == null || javaTypes == null) {
			Log.Warning ($"{Description}: cannot save raw report, no data");
			return;
		}

		string outputFilePath = Utilities.GetManagedOutputFileName (baseOutputFilePath, extension);
		Utilities.CreateFileDirectory (outputFilePath);
		using (var sw = new StreamWriter (outputFilePath, false, new UTF8Encoding (false))) {
			sw.WriteLine ("TYPE_TOKEN_DECIMAL (TYPE_TOKEN_HEXADECIMAL)\tJAVA_MAP_INDEX");
			uint index = 0;
			foreach (TypeMapModule module in modules) {
				sw.WriteLine ();
				sw.WriteLine ($"Module {index++:D04}: {module.assembly_name} (MVID: {module.module_uuid}; entries: {module.entry_count}; duplicates: {module.duplicate_count})");
				if (module.map == null) {
					sw.WriteLine ($"{indent}no map");
				} else {
					WriteManagedMap ("map", module.map, sw);
				}

				if (module.duplicate_map == null) {
					if (module.duplicate_count > 0)
					sw.WriteLine ($"{indent}no duplicate map, but there should be {module.duplicate_count} entries");
					else
					sw.WriteLine ($"{indent}no duplicates");
					continue;
				}

				WriteManagedMap ("duplicate map", module.duplicate_map, sw);
			}
			sw.Flush ();
		}

		outputFilePath = Utilities.GetJavaOutputFileName (baseOutputFilePath, extension);
		using (var sw = new StreamWriter (outputFilePath, false, new UTF8Encoding (false))) {
			sw.WriteLine ("MANAGED_MODULE_INDEX\tTYPE_TOKEN_DECIMAL (TYPE_TOKEN_HEXADECIMAL)\tJAVA_TYPE_NAME");

			foreach (TypeMapJava tmj in javaTypes) {
				sw.WriteLine ($"{indent}{tmj.module_index}\t{tmj.type_token_id:D08} ({tmj.type_token_id:X08})\t{GetJavaTypeName (tmj)}");
			}
			sw.Flush ();
		}

		void WriteManagedMap (string name, List<TypeMapModuleEntry> map, StreamWriter sw)
		{
			sw.WriteLine ($"{indent}{name}:");
			foreach (TypeMapModuleEntry entry in map) {
				sw.WriteLine ($"{indent}{indent}{entry.type_token_id} ({entry.type_token_id:X08})\t{entry.java_map_index}");
			}
		}
	}

	MapManagedType MakeManagedType (Guid mvid, uint tokenID, string assemblyName, string filePath, bool isGeneric, bool isDuplicate)
	{
		return new MapManagedType (mvid, tokenID, assemblyName, filePath) {
			IsGeneric = isGeneric,
			IsDuplicate = isDuplicate,
			TypeName = ManagedResolver.Lookup (assemblyName, mvid, tokenID)
		};
	}

	protected override bool Convert ()
	{
		try {
			DoConvert ();
		} catch (Exception ex) {
			Log.ExceptionError ($"{Description}: failed to convert loaded maps to common format", ex);
			return false;
		}
		return true;
	}

	bool DoConvert ()
	{
		const string Present = "present";
		const string Absent = "absent";

		if (modules == null || javaTypes == null || javaTypeNames == null) {
			Log.Warning (LogTag, $"cannot convert maps, missing data:");
			Log.Warning ($"  {MapModulesSymbolName}: {(modules == null ? Absent : Present)}");
			Log.Warning ($"  {MapJavaSymbolName}: {(javaTypes == null ? Absent : Present)}");
			Log.Warning ($"  {JavaTypeNamesSymbolName}: {(javaTypeNames == null ? Absent : Present)}");
			return false;
		}

		if (javaTypes.Count != javaTypeNames.Count) {
			Log.Warning (LogTag, $"Java types map has a different number of entries ({javaTypes.Count}) than the Java type names array ({javaTypeNames.Count})");
			return false;
		}

		string filePath = ELF.FilePath;
		var managedToJava = new List<MapEntry> ();
		uint index = 0;

		bool somethingFailed = false;
		foreach (TypeMapModule m in modules) {
			ConvertManagedMap (m, EnsureMap (m), isDuplicate: false);
			if (m.duplicate_map != null) {
				if (!ConvertManagedMap (m, m.duplicate_map, isDuplicate: true)) {
					somethingFailed = true;
				}
			}

			index++;
		}

		if (somethingFailed) {
			return false;
		}

		index = 0;
		var javaToManaged = new List<MapEntry> ();
		foreach (TypeMapJava tmj in javaTypes) {
			(bool success, TypeMapModule? module, bool isGeneric, bool isDuplicate) = FindManagedType (tmj.module_index, tmj.type_token_id);
			if (!success) {
				somethingFailed = true;
				continue;
			}

			if (module == null) {
				throw new InvalidOperationException ("module must not be null here");
			}

			javaToManaged.Add (
				new MapEntry (
					MakeManagedType (module.module_uuid, tmj.type_token_id, module.assembly_name, filePath, isGeneric, isDuplicate),
					new MapJavaType (GetJavaTypeName (tmj), filePath)
				)
			);
			index++;
		}

		map = MakeMap (managedToJava, javaToManaged);
		return true;

		bool ConvertManagedMap (TypeMapModule module, List<TypeMapModuleEntry> map, bool isDuplicate)
		{
			foreach (TypeMapModuleEntry entry in map) {
				TypeMapJava java;

				if ((uint)javaTypes.Count <= entry.java_map_index) {
					Log.Error ($"Managed type {entry.type_token_id} in module {module.assembly_name} ({module.module_uuid}) has invalid Java map index {entry.java_map_index}");
					return false;
				}
				java = javaTypes[(int)entry.java_map_index];
				managedToJava.Add (
					new MapEntry (
						MakeManagedType (module.module_uuid, entry.type_token_id, module.assembly_name, filePath, isGeneric: false, isDuplicate: isDuplicate),
						new MapJavaType (GetJavaTypeName (java), filePath)
					)
				);
			}
			return true;
		}

		(bool success, TypeMapModule? module, bool isGeneric, bool isDuplicate) FindManagedType (uint moduleIndex, uint tokenID)
		{
			if (moduleIndex >= (uint)modules.Count) {
				Log.Error ($"Invalid module index {moduleIndex} for type token ID {tokenID} at Java map index {index}");
				return (false, null, false, false);
			}

			TypeMapModule m = modules[(int)moduleIndex];
			if (tokenID == 0) {
				return (true, m, true, false);
			}

			foreach (TypeMapModuleEntry entry in EnsureMap (m)) {
				if (entry.type_token_id == tokenID) {
					return (true, m, false, false);
				}
			}

			if (m.duplicate_map != null) {
				foreach (TypeMapModuleEntry entry in m.duplicate_map) {
					if (entry.type_token_id == tokenID) {
						return (true, m, false, true);
					}
				}
			}

			Log.Error ($"Module {m.assembly_name} ({m.module_uuid}) at index {moduleIndex} doesn't contain an entry for managed type with token ID {tokenID}");
			return (false, null, false, false);
		}

		List<TypeMapModuleEntry> EnsureMap (TypeMapModule m)
		{
			if (m.map == null) {
				throw new InvalidOperationException ($"Module {m.module_uuid} ({m.assembly_name}) has no map?");
			}

			return m.map;
		}
	}

	List<ulong> LoadJavaTypeNameHashes (string filePath)
	{
		Log.Debug ();
		Log.Debug (LogTag, "Reading Java type name hashes");

		ulong size = 0;
		if (Is64Bit) {
			size += GetPaddedSize<ulong> (size); // hashes are 64-bit
		} else {
			size += GetPaddedSize<uint> (size); // hashes are 32-bit
		}

		(byte[] hashesData, ISymbolEntry? symbol) = ELF.GetData (MapJavaHashesSymbolName);
		if (hashesData.Length == 0 || symbol == null) {
			throw new InvalidOperationException ($"{filePath} doesn't have a valid '{MapJavaHashesSymbolName}' symbol");
		}

		var ret = new List<ulong> ();
		ulong offset = 0;
		for (ulong i = 0; i < (ulong)hashesData.Length / size; i++) {
			ulong hash;
			if (Is64Bit) {
				hash = ReadUInt64 (hashesData, ref offset);
			} else {
				hash = (ulong)ReadUInt32 (hashesData, ref offset);
			}

			Log.Debug (LogTag, $"  [{i}] 0x{HashToHexString(hash)}");
			ret.Add (hash);
		}

		Log.Debug ();
		Log.Debug (LogTag, $"Java type name hashes loaded (count: {ret.Count})");

		return ret;
	}

	List<string> LoadJavaTypeNames (string filePath)
	{
		Log.Debug ();
		Log.Debug (LogTag, "Reading Java type names");

		ulong size = 0;
		size += GetPaddedSize<string> (size); // pointers

		(byte[] namesData, ISymbolEntry? symbol) = ELF.GetData (JavaTypeNamesSymbolName);
		if (namesData.Length == 0 || symbol == null) {
			throw new InvalidOperationException ($"{filePath} doesn't have a valid '{JavaTypeNamesSymbolName}' symbol");
		}

		var ret = new List<string> ();
		ulong offset = 0;
		for (ulong i = 0; i < (ulong)namesData.Length / size; i++) {
			ulong pointer = ReadPointer (symbol, namesData, ref offset);
			string? name;

			if (pointer != 0) {
				name = ELF.GetASCIIZFromPointer (pointer);
			} else {
				name = null;
			}

			ret.Add (name ?? String.Empty);

			ulong hash = TypeMapHelper.HashJavaName (name ?? String.Empty, Is64Bit);
			int javaTypeIndex = javaTypeNameHashes!.IndexOf (hash);

			if (javaTypeIndex < 0) {
				Log.Warning (LogTag, $"Hash 0x{HashToHexString(hash)} for Java type name '{name}' not found in the '{MapJavaHashesSymbolName}' array");
			}

			Log.Debug (LogTag, $"  [{i}] {(name ?? String.Empty)} (hash: 0x{HashToHexString(hash)})");
		}

		Log.Debug ();
		Log.Debug (LogTag, $"Java type names loaded (count: {ret.Count})");

		return ret;
	}

	List<TypeMapJava> LoadJavaTypes (string filePath)
	{
		Log.Debug ();
		Log.Debug (LogTag, "Reading Java types");

		ulong javaTypeCount = (ulong)ELF.GetUInt32 (JavaTypeCountSymbolName);

		// MUST be kept in sync with: src/monodroid/jni/xamarin-app.hh (struct TypeMapJava)
		ulong size = 0;
		size += GetPaddedSize<uint> (size); // module_index
		size += GetPaddedSize<uint> (size); // type_token_id
		size += GetPaddedSize<uint> (size); // java_name_index

		(byte[] data, ISymbolEntry? symbol) = ELF.GetData (MapJavaSymbolName);
		if (data.Length == 0) {
			throw new InvalidOperationException ($"{filePath} doesn't have a valid '{MapJavaSymbolName}' symbol");
		}

		ulong calculatedJavaTypeCount = (ulong)data.LongLength / size;
		if (calculatedJavaTypeCount != javaTypeCount)
		throw new InvalidOperationException ($"{filePath} has invalid '{JavaTypeCountSymbolName}' symbol value ({javaTypeCount}), '{JavaTypeCountSymbolName}' size indicates there are {calculatedJavaTypeCount} managedToJava instead");

		var ret = new List<TypeMapJava> ();
		ulong offset = 0;
		for (ulong i = 0; i < javaTypeCount; i++) {
			var javaEntry = new TypeMapJava {
				module_index = ReadUInt32 (data, ref offset, packed: true),
				type_token_id = ReadUInt32 (data, ref offset, packed: true),
				java_name_index = ReadUInt32 (data, ref offset, packed: true),
			};

			ret.Add (javaEntry);
		}

		Log.Debug (LogTag, $"Java types loaded (count: {ret.Count})");
		Log.Debug ();

		return ret;
	}

	List<TypeMapModule> LoadMapModules (string filePath)
	{
		Log.Debug ();
		Log.Debug (LogTag, "Reading map modules");

		ulong moduleCount = (ulong)ELF.GetUInt32 (ModuleCountSymbolName);

		// MUST be kept in sync with: src/monodroid/jni/xamarin-app.hh (struct TypeMapModule)
		ulong size;

		size  = 16;                           // module_uuid
		size += GetPaddedSize<uint> (size);   // entry_count
		size += GetPaddedSize<uint> (size);   // duplicate_count
		size += GetPaddedSize<string> (size); // map (pointer)
		size += GetPaddedSize<string> (size); // duplicate_map (pointer)
		size += GetPaddedSize<string> (size); // assembly_name (pointer)
		size += GetPaddedSize<string> (size); // image (pointer)
		size += GetPaddedSize<uint> (size);   // java_name_width
		size += GetPaddedSize<string> (size); // java_map (pointer)

		(byte[] moduleData, ISymbolEntry? symbol) = ELF.GetData (MapModulesSymbolName);
		if (moduleData.Length == 0 || symbol == null) {
			throw new InvalidOperationException ($"{filePath} doesn't have a valid '{MapModulesSymbolName}' symbol");
		}

		ulong calculatedModuleCount = (ulong)moduleData.Length / size;
		if (calculatedModuleCount != moduleCount) {
			throw new InvalidOperationException ($"{filePath} has invalid '{ModuleCountSymbolName}' symbol value ({moduleCount}), '{MapModulesSymbolName}' size indicates there are {calculatedModuleCount} managedToJava instead");
		}

		var ret = new List<TypeMapModule> ();
		ulong offset = 0;
		for (ulong i = 0; i < moduleCount; i++) {
			Log.Debug ($"Module {i + 1}");
			var module = new TypeMapModule ();

			byte[] mvid = new byte[16];
			Array.Copy (moduleData, (int)offset, mvid, 0, mvid.Length);
			module.module_uuid = new Guid (mvid);
			offset += (ulong)mvid.Length;
			Log.Debug (LogTag, $"  module_uuid == {module.module_uuid} (offset: {offset})");

			module.entry_count = ReadUInt32 (moduleData, ref offset);
			Log.Debug (LogTag, $"  entry_count == {module.entry_count} (offset: {offset})");

			module.duplicate_count = ReadUInt32 (moduleData, ref offset);
			Log.Debug (LogTag, $"  duplicate_count == {module.duplicate_count} (offset: {offset})");

			// MUST be kept in sync with: src/monodroid/jni/xamarin-app.hh (struct TypeMapModuleEntry)
			ulong pointer = ReadPointer (symbol, moduleData, ref offset);
			Log.Debug (LogTag, $"  *map == 0x{pointer:x} (offset: {offset})");

			if (pointer == 0) {
				throw new InvalidOperationException ($"Broken typemap structure, map pointer for module {module.module_uuid} is null");
			}

			size  = 0;
			size += GetPaddedSize<uint> (size); // type_token_id
			size += GetPaddedSize<uint> (size); // java_map_index

			ulong mapSize = size * module.entry_count;
			byte[] data = ELF.GetDataFromPointer (pointer, mapSize);

			module.map = new List<TypeMapModuleEntry> ();
			ReadMapEntries (module.map, data, module.entry_count);

			// MUST be kept in sync with: src/monodroid/jni/xamarin-app.hh (struct TypeMapModuleEntry)
			pointer = ReadPointer (symbol, moduleData, ref offset);

			if (pointer != 0) {
				mapSize = size *  module.duplicate_count;
				data = ELF.GetDataFromPointer (pointer, mapSize);
				module.duplicate_map = new List<TypeMapModuleEntry> ();
				ReadMapEntries (module.duplicate_map, data, module.duplicate_count);
			}

			pointer = ReadPointer (symbol, moduleData, ref offset);
			if (pointer != 0) {
				module.assembly_name = ELF.GetASCIIZFromPointer (pointer);
			}

			Log.Debug ($"  assembly_name == {module.assembly_name}");
			Log.Debug ("");

			// Read the values to properly adjust the offset taking padding into account
			ReadPointer (moduleData, ref offset);
			ReadUInt32 (moduleData, ref offset);
			ReadPointer (moduleData, ref offset);

			ret.Add (module);

			// Padding
			offset += offset % size;
		}

		Log.Debug (LogTag, $"Map modules loaded (count: {ret.Count})");
		Log.Debug ();

		return ret;

		void ReadMapEntries (List<TypeMapModuleEntry> map, byte[] inputData, uint entryCount)
		{
			ulong mapOffset = 0;
			for (uint i = 0; i < entryCount; i++) {
				var entry = new TypeMapModuleEntry {
					type_token_id = ReadUInt32 (inputData, ref mapOffset),
					java_map_index = ReadUInt32 (inputData, ref mapOffset)
				};

				map.Add (entry);
			}
		}
	}

	string GetJavaTypeName (TypeMapJava tmj)
	{
		if (javaTypeNames == null) {
			return "<no Java type names data>";
		}

		if (tmj.java_name_index >= javaTypeNames.Count) {
			return $"<invalid name index {tmj.java_name_index}>";
		}

		return javaTypeNames[(int)tmj.java_name_index];
	}

	string HashToHexString (ulong hash) => Is64Bit ? $"{hash:X16}" : $"{hash:X8}";
}
