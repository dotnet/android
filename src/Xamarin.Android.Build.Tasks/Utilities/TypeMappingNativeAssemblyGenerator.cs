using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tasks
{
	class TypeMappingNativeAssemblyGenerator : NativeAssemblyGenerator
	{
		readonly string baseFileName;
		readonly NativeTypeMappingData mappingData;
		readonly bool sharedBitsWritten;

		public TypeMappingNativeAssemblyGenerator (NativeAssemblerTargetProvider targetProvider, NativeTypeMappingData mappingData, string baseFileName, bool sharedBitsWritten, bool sharedIncludeUsesAbiPrefix = false)
			: base (targetProvider, baseFileName, sharedIncludeUsesAbiPrefix)
		{
			this.mappingData = mappingData ?? throw new ArgumentNullException (nameof (mappingData));

			if (String.IsNullOrEmpty (baseFileName))
				throw new ArgumentException("must not be null or empty", nameof (baseFileName));

			this.baseFileName = baseFileName;
			this.sharedBitsWritten = sharedIncludeUsesAbiPrefix ? false : sharedBitsWritten;
		}

		protected override void WriteSymbols (StreamWriter output)
		{
			output.WriteLine ();
			WriteHeaderField (output, "map_module_count", mappingData.MapModuleCount);

			output.WriteLine ();
			WriteHeaderField (output, "java_type_count", mappingData.JavaTypeCount);

			output.WriteLine ();
			WriteHeaderField (output, "java_name_width", mappingData.JavaNameWidth);

			bool haveAssemblyNames = mappingData.AssemblyNames.Count > 0;
			bool haveModules = mappingData.Modules.Length > 0;

			output.WriteLine ();
			if (haveAssemblyNames) {
				output.WriteLine ($"{Indent}.include{Indent}\"{Path.GetFileName (SharedIncludeFile)}\"");
			} else {
				WriteCommentLine (output, $"No shared data present, {Path.GetFileName (SharedIncludeFile)} not generated");
			}

			if (haveModules) {
				output.WriteLine ($"{Indent}.include{Indent}\"{Path.GetFileName (TypemapsIncludeFile)}\"");
			} else {
				WriteCommentLine (output, $"No modules defined, {Path.GetFileName (TypemapsIncludeFile)} not generated");
			}
			output.WriteLine ();

			if (!sharedBitsWritten && haveAssemblyNames) {
				using (var ms = new MemoryStream ()) {
					using (var sharedOutput = new StreamWriter (ms, output.Encoding)) {
						WriteAssemblyNames (sharedOutput);
						sharedOutput.Flush ();
						MonoAndroidHelper.CopyIfStreamChanged (ms, SharedIncludeFile);
					}
				}
			}

			if (haveModules) {
				using (var ms = new MemoryStream ()) {
					using (var mapOutput = new StreamWriter (ms, output.Encoding)) {
						WriteMapModules (output, mapOutput, "map_modules");
						mapOutput.Flush ();
						MonoAndroidHelper.CopyIfStreamChanged (ms, TypemapsIncludeFile);
					}
				}
			} else {
				WriteMapModules (output, null, "map_modules");
			}

			WriteJavaMap (output, "map_java");
		}

		void WriteAssemblyNames (StreamWriter output)
		{
			foreach (var kvp in mappingData.AssemblyNames) {
				string label = kvp.Key;
				string name = kvp.Value;

				WriteData (output, name, label, isGlobal: false);
				output.WriteLine ();
			}
		}

		void WriteManagedMaps (StreamWriter output, string moduleSymbolName, IEnumerable<TypeMapGenerator.TypeMapEntry> entries)
		{
			if (entries == null)
				return;

			var tokens = new Dictionary<uint, uint> ();
			foreach (TypeMapGenerator.TypeMapEntry entry in entries) {
				int idx = Array.BinarySearch (mappingData.JavaTypeNames, entry.JavaName, StringComparer.Ordinal);
				if (idx < 0)
					throw new InvalidOperationException ($"Could not map entry '{entry.JavaName}' to array index");

				tokens[entry.Token] = (uint)idx;
			}

			WriteSection (output, $".rodata.{moduleSymbolName}", hasStrings: false, writable: false);
			WriteStructureSymbol (output, moduleSymbolName, alignBits: 0, isGlobal: false);

			uint size = 0;
			var sortedTokens = tokens.Keys.ToArray ();
			Array.Sort (sortedTokens);

			foreach (uint token in sortedTokens) {
				size += WriteStructure (output, packed: false, structureWriter: () => WriteManagedMapEntry (output, token, tokens[token]));
			}

			WriteStructureSize (output, moduleSymbolName, size);
			output.WriteLine ();
		}

		uint WriteManagedMapEntry (StreamWriter output, uint token, uint javaMapIndex)
		{
			uint size = WriteData (output, token);
			size += WriteData (output, javaMapIndex);

			return size;
		}

		void WriteMapModules (StreamWriter output, StreamWriter mapOutput, string symbolName)
		{
			WriteCommentLine (output, "Managed to Java map: START", indent: false);
			WriteSection (output, $".data.rel.{symbolName}", hasStrings: false, writable: true);
			WriteStructureSymbol (output, symbolName, alignBits: TargetProvider.MapModulesAlignBits, isGlobal: true);

			uint size = 0;
			int moduleCounter = 0;
			foreach (TypeMapGenerator.ModuleData data in mappingData.Modules) {
				string mapName = $"module{moduleCounter++}_managed_to_java";
				string duplicateMapName;

				if (data.DuplicateTypes.Count == 0)
					duplicateMapName = null;
				else
					duplicateMapName = $"{mapName}_duplicates";

				size += WriteStructure (output, packed: false, structureWriter: () => WriteMapModule (output, mapName, duplicateMapName, data));
				if (mapOutput != null) {
					WriteManagedMaps (mapOutput, mapName, data.Types);
					if (data.DuplicateTypes.Count > 0)
						WriteManagedMaps (mapOutput, duplicateMapName, data.DuplicateTypes.Values);
				}
			}

			WriteStructureSize (output, symbolName, size);
			WriteCommentLine (output, "Managed to Java map: END", indent: false);
			output.WriteLine ();
		}

		uint WriteMapModule (StreamWriter output, string mapName, string duplicateMapName, TypeMapGenerator.ModuleData data)
		{
			uint size = 0;
			WriteCommentLine (output, $"module_uuid: {data.Mvid}");
			size += WriteData (output, data.MvidBytes);

			WriteCommentLine (output, "entry_count");
			size += WriteData (output, data.Types.Length);

			WriteCommentLine (output, "duplicate_count");
			size += WriteData (output, data.DuplicateTypes.Count);

			WriteCommentLine (output, "map");
			size += WritePointer (output, mapName);

			WriteCommentLine (output, "duplicate_map");
			size += WritePointer (output, duplicateMapName);

			WriteCommentLine (output, $"assembly_name: {data.AssemblyName}");
			size += WritePointer (output, MakeLocalLabel (data.AssemblyNameLabel));

			WriteCommentLine (output, "image");
			size += WritePointer (output);

			// These two are used only in Debug builds with Instant Run enabled, but for simplicity we always output
			// them.
			WriteCommentLine (output, "java_name_width");
			size += WriteData (output, (uint)0);

			WriteCommentLine (output, "java_map");
			size += WritePointer (output);

			output.WriteLine ();

			return size;
		}

		void WriteJavaMap (StreamWriter output, string symbolName)
		{
			WriteCommentLine (output, "Java to managed map: START", indent: false);
			WriteSection (output, $".rodata.{symbolName}", hasStrings: false, writable: false);
			WriteStructureSymbol (output, symbolName, alignBits: TargetProvider.MapJavaAlignBits, isGlobal: true);

			uint size = 0;
			int entryCount = 0;
			foreach (TypeMapGenerator.TypeMapEntry entry in mappingData.JavaTypes) {
				size += WriteJavaMapEntry (output, entry, entryCount++);
			}

			WriteStructureSize (output, symbolName, size);
			WriteCommentLine (output, "Java to managed map: END", indent: false);
			output.WriteLine ();
		}

		uint WriteJavaMapEntry (StreamWriter output, TypeMapGenerator.TypeMapEntry entry, int entryIndex)
		{
			uint size = 0;

			WriteCommentLine (output, $"#{entryIndex}");
			WriteCommentLine (output, "module_index");
			size += WriteData (output, entry.ModuleIndex);

			WriteCommentLine (output, "type_token_id");
			size += WriteData (output, entry.Token);

			WriteCommentLine (output, "java_name");
			size += WriteAsciiData (output, entry.JavaName, mappingData.JavaNameWidth);

			output.WriteLine ();

			return size;
		}

		void WriteHeaderField (StreamWriter output, string name, uint value)
		{
			WriteCommentLine (output, $"{name}: START", indent: false);
			WriteSection (output, $".rodata.{name}", hasStrings: false, writable: false);
			WriteSymbol (output, name, size: 4, alignBits: 2, isGlobal: true, isObject: true, alwaysWriteSize: true);
			WriteData (output, value);
			WriteCommentLine (output, $"{name}: END", indent: false);
		}
	}
}
