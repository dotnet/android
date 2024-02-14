using System;
using System.Collections.Generic;

using ELFSharp.ELF.Sections;

namespace tmt;

class XamarinAppDebugDSO_V2 : XamarinAppDebugDSO_Version
{
	sealed class MappedType
	{
		public string TargetType;
		public ulong  DuplicateCount = 0;

		public MappedType (string targetType)
		{
			TargetType = targetType;
		}
	}

	const string TypeMapSymbolName = "type_map";

	protected override string LogTag => "DebugDSO_V2";

	Map? map;
	SortedDictionary<string, List<MappedType>> javaToManaged = new SortedDictionary<string, List<MappedType>> (StringComparer.Ordinal);
	SortedDictionary<string, MappedType> managedToJava = new SortedDictionary<string, MappedType> (StringComparer.Ordinal);

	public override string FormatVersion => "2";
	public override Map Map => map ?? throw new InvalidOperationException ("Data hasn't been loaded yet");

	public XamarinAppDebugDSO_V2 (ManagedTypeResolver managedResolver, AnELF elf)
	: base (managedResolver, elf)
	{}

	public override bool CanLoad (AnELF elf)
	{
		return HasSymbol (elf, TypeMapSymbolName);
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

	void DoConvert ()
	{
		var managed = new List<MapEntry> ();
		foreach (var kvp in managedToJava) {
			string managedName = kvp.Key;
			MappedType javaType = kvp.Value;

			managed.Add (
				new MapEntry (
					new MapManagedType (managedName) { IsDuplicate = javaType.DuplicateCount > 0 },
					new MapJavaType (javaType.TargetType)
				)
			);
		}

		var java = new List<MapEntry> ();
		foreach (var kvp in javaToManaged) {
			string javaName = kvp.Key;
			List<MappedType> managedTypes = kvp.Value;
			var javaType = new MapJavaType (javaName);

			foreach (MappedType managedType in managedTypes) {
				java.Add (
					new MapEntry (
						new MapManagedType (managedType.TargetType) { IsDuplicate = managedType.DuplicateCount > 0 },
						javaType
					)
				);
			}
		}

		map = MakeMap (managed, java);
	}

	protected override bool LoadMaps ()
	{
		try {
			return DoLoadMaps ();
		} catch (Exception ex) {
			Log.ExceptionError ($"{Description}: failed to load maps", ex);
			return false;
		}
	}

	bool DoLoadMaps ()
	{
		// MUST be kept in sync with: src/monodroid/jni/xamarin-app.hh (struct TypeMap)
		ulong size = 0;
		size += GetPaddedSize<uint> (size);   // entry_count
		size += GetPaddedSize<string> (size); // assembly_name (pointer)
		size += GetPaddedSize<string> (size); // data (pointer)
		size += GetPaddedSize<string> (size); // java_to_managed (pointer)
		size += GetPaddedSize<string> (size); // managed_to_java (pointer)

		string filePath = ELF.FilePath;
		(byte[] mapData, ISymbolEntry? symbol) = ELF.GetData (TypeMapSymbolName);
		if (mapData.Length == 0 || symbol == null) {
			Log.Error ($"{filePath} doesn't have a valid '{TypeMapSymbolName}' symbol");
			return false;
		}

		if ((ulong)mapData.Length != size) {
			Log.Error ($"Symbol '{TypeMapSymbolName}' in {filePath} has invalid size. Expected {size}, got {mapData.Length}");
			return false;
		}

		ulong offset = 0;
		uint entry_count = ReadUInt32 (mapData, ref offset);
		ReadPointer (symbol, mapData, ref offset); // assembly_name, unused in Debug mode
		ReadPointer (symbol, mapData, ref offset); // data, unused in Debug mode

		ulong pointer = ReadPointer (symbol, mapData, ref offset); // java_to_managed
		LoadMap ("Java to Managed", pointer, entry_count, AddJavaToManaged);

		pointer = ReadPointer (symbol, mapData, ref offset); // managed_to_java
		LoadMap ("Managed to Java", pointer, entry_count, AddManagedToJava);

		return true;
	}

	void AddManagedToJava (string mapFrom, string mapTo)
	{
		if (managedToJava.TryGetValue (mapFrom, out MappedType? entry)) {
			entry.DuplicateCount++;
			return;
		}

		managedToJava.Add (mapFrom, new MappedType (mapTo));
	}

	void AddJavaToManaged (string mapFrom, string mapTo)
	{
		if (javaToManaged.TryGetValue (mapFrom, out List<MappedType>? types)) {
			types.Add (new MappedType (mapTo));
			for (int i = 1; i < types.Count; i++) {
				MappedType entry = types[i];
				entry.DuplicateCount++;
			}
			return;
		}

		javaToManaged.Add (mapFrom, new List<MappedType> {new MappedType (mapTo)});
	}

	void LoadMap (string name, ulong arrayPointer, uint entry_count, Action<string, string> addToMap)
	{
		string entries = entry_count == 1 ? "entry" : "entries";
		Log.Info ($"  Loading {name} map: {entry_count} {entries}");
		Log.Debug (LogTag, $"  pointer == 0x{arrayPointer:X}");

		ulong size = 0;
		size += GetPaddedSize<string> (size); // from
		size += GetPaddedSize<string> (size); // to

		ulong mapSize = entry_count * size;
		byte[] data = ELF.GetDataFromPointer (arrayPointer, mapSize);

		ulong offset = 0;
		string mapFrom;
		string mapTo;
		for (uint i = 0; i < entry_count; i++) {
			ulong pointer = ReadPointer (arrayPointer, data, ref offset);
			Log.Debug (LogTag, $"  [{i}] pointer1 == 0x{pointer:X}");
			if (pointer != 0) {
				mapFrom = ELF.GetASCIIZFromPointer (pointer);
			} else {
				mapFrom = $"#{i} <null>";
			}

			pointer = ReadPointer (arrayPointer, data, ref offset);
			Log.Debug (LogTag, $"  [{i}] pointer2 == 0x{pointer:X}");
			if (pointer != 0) {
				mapTo = ELF.GetASCIIZFromPointer (pointer);
			} else {
				mapTo = $"#{i} <null>";
			}

			addToMap (mapFrom, mapTo);
		}
	}
}
