using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class TypeMappingReleaseNativeAssemblyGenerator : TypeMappingAssemblyGenerator
	{
		sealed class TypeMapModuleContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var map_module = data as TypeMapModule;
				if (map_module == null) {
					throw new InvalidOperationException ("Invalid data type, expected an instance of TypeMapModule");
				}

				if (String.Compare ("module_uuid", fieldName, StringComparison.Ordinal) == 0) {
					return $"module_uuid: {map_module.MVID}";
				}

				if (String.Compare ("assembly_name", fieldName, StringComparison.Ordinal) == 0) {
					return $"assembly_name: {map_module.AssemblyNameValue}";
				}

				return String.Empty;
			}
		}

		sealed class TypeMapJavaContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override uint GetMaxInlineWidth (object data, string fieldName)
			{
				var map_java = data as TypeMapJava;
				if (map_java == null) {
					throw new InvalidOperationException ("Invalid data type, expected an instance of TypeMapJava");
				}

				if (String.Compare ("java_name", fieldName, StringComparison.Ordinal) == 0) {
					return map_java.MaxJavaNameLength;
				}

				return 0;
			}
		}

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
			public Guid   MVID;

			[NativeAssembler (Ignore = true)]
			public string AssemblyNameValue;

			[NativeAssembler (UsesDataProvider = true)]
			public byte[] module_uuid;
			public uint   entry_count;
			public uint   duplicate_count;

			[NativeAssemblerString (AssemblerStringFormat.PointerToSymbol)]
			public string map;

			[NativeAssemblerString (AssemblerStringFormat.PointerToSymbol)]
			public string duplicate_map;

			[NativeAssemblerString (AssemblerStringFormat.PointerToSymbol, UsesDataProvider = true)]
			public string assembly_name;
			public IntPtr image;
			public uint   java_name_width;
			public IntPtr java_map;
		}

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh TypeMapJava structure
		[NativeAssemblerStructContextDataProvider (typeof (TypeMapJavaContextDataProvider))]
		sealed class TypeMapJava
		{
			[NativeAssembler (Ignore = true)]
			public uint MaxJavaNameLength;

			public uint module_index;
			public uint type_token_id;

			[NativeAssemblerString (AssemblerStringFormat.InlineArray, PadToMaxLength = true)]
			public string  java_name;
		}

		readonly string baseFileName;
		readonly NativeTypeMappingData mappingData;
		readonly bool sharedBitsWritten;

		public TypeMappingReleaseNativeAssemblyGenerator (AndroidTargetArch arch, NativeTypeMappingData mappingData, string baseFileName, bool sharedBitsWritten, bool sharedIncludeUsesAbiPrefix = false)
			: base (arch, baseFileName, sharedIncludeUsesAbiPrefix)
		{
			this.mappingData = mappingData ?? throw new ArgumentNullException (nameof (mappingData));
			this.baseFileName = baseFileName;
			this.sharedBitsWritten = sharedIncludeUsesAbiPrefix ? false : sharedBitsWritten;
		}

		protected override void Write (NativeAssemblyGenerator generator)
		{
			WriteHeaderField (generator, "map_module_count", mappingData.MapModuleCount);
			WriteHeaderField (generator, "java_type_count", mappingData.JavaTypeCount);
			WriteHeaderField (generator, "java_name_width", mappingData.JavaNameWidth);

			bool haveAssemblyNames = mappingData.AssemblyNames.Count > 0;
			bool haveModules = mappingData.Modules.Length > 0;

			if (haveAssemblyNames) {
				generator.WriteInclude (Path.GetFileName (SharedIncludeFile));
			} else {
				generator.WriteCommentLine ($"No shared data present, {Path.GetFileName (SharedIncludeFile)} not generated");
			}

			generator.WriteEOL ();
			if (haveModules) {
				generator.WriteInclude (Path.GetFileName (TypemapsIncludeFile));
			} else {
				generator.WriteCommentLine ($"No modules defined, {Path.GetFileName (TypemapsIncludeFile)} not generated");
			}

			if (!sharedBitsWritten && haveAssemblyNames) {
				using (var sharedOutput = MemoryStreamPool.Shared.CreateStreamWriter (generator.Output.Encoding)) {
					WriteAssemblyNames (generator, sharedOutput);
					sharedOutput.Flush ();
					Files.CopyIfStreamChanged (sharedOutput.BaseStream, SharedIncludeFile);
				}
			}

			if (haveModules) {
				using (var mapOutput = MemoryStreamPool.Shared.CreateStreamWriter (generator.Output.Encoding)) {
					WriteMapModules (generator, mapOutput, "map_modules");
					mapOutput.Flush ();
					Files.CopyIfStreamChanged (mapOutput.BaseStream, TypemapsIncludeFile);
				}
			} else {
				WriteMapModules (generator, null, "map_modules");
			}

			WriteJavaMap (generator, "map_java");
		}

		void WriteAssemblyNames (NativeAssemblyGenerator generator, StreamWriter output)
		{
			generator.WriteStringSection (output, "map_assembly_names");
			foreach (var kvp in mappingData.AssemblyNames) {
				string label = kvp.Key;
				string name = kvp.Value;

				generator.WriteStringSymbol (output, label, name, global: false);
			}
		}

		void WriteManagedMaps (NativeAssemblyGenerator generator, StreamWriter output, string moduleSymbolName, IEnumerable<TypeMapGenerator.TypeMapReleaseEntry> entries)
		{
			if (entries == null)
				return;

			var tokens = new Dictionary<uint, uint> ();
			foreach (TypeMapGenerator.TypeMapReleaseEntry entry in entries) {
				int idx = Array.BinarySearch (mappingData.JavaTypeNames, entry.JavaName, StringComparer.Ordinal);
				if (idx < 0)
					throw new InvalidOperationException ($"Could not map entry '{entry.JavaName}' to array index");

				tokens[entry.Token] = (uint)idx;
			}

			var sortedTokens = tokens.Keys.ToArray ();
			Array.Sort (sortedTokens);

			generator.WriteDataSection (output, moduleSymbolName, writable: false);
			var map_entry = new TypeMapModuleEntry ();

			NativeAssemblyGenerator.StructureWriteContext mapArray = generator.StartStructureArray ();
			foreach (uint token in sortedTokens) {
				map_entry.type_token_id = token;
				map_entry.java_map_index = tokens[token];

				NativeAssemblyGenerator.StructureWriteContext mapEntryStruct = generator.AddStructureArrayElement (mapArray);
				generator.WriteStructure (mapEntryStruct, map_entry);
			}
			generator.WriteSymbol (output, mapArray, moduleSymbolName, alreadyInSection: true, skipLabelCounter: true);
		}

		void WriteMapModules (NativeAssemblyGenerator generator, StreamWriter mapOutput, string symbolName)
		{
			generator.WriteEOL ();
			generator.WriteCommentLine ("Managed to Java map: START");
			generator.WriteDataSection ($"rel.{symbolName}");

			int moduleCounter = 0;
			var map_module = new TypeMapModule {
				image = IntPtr.Zero,

				// These two are used only in Debug builds with Instant Run enabled, but for simplicity we always output
				// them.
				java_name_width = 0,
				java_map = IntPtr.Zero,
			};

			NativeAssemblyGenerator.StructureWriteContext mapModulesArray = generator.StartStructureArray ();
			foreach (TypeMapGenerator.ModuleReleaseData data in mappingData.Modules) {
				string mapName = $"module{moduleCounter++}_managed_to_java";
				string duplicateMapName;

				if (data.DuplicateTypes.Count == 0)
					duplicateMapName = String.Empty;
				else
					duplicateMapName = $"{mapName}_duplicates";

				map_module.MVID = data.Mvid;
				map_module.AssemblyNameValue = data.AssemblyName;
				map_module.module_uuid = data.MvidBytes;
				map_module.entry_count = (uint)data.Types.Length;
				map_module.duplicate_count = (uint)data.DuplicateTypes.Count;
				map_module.map = generator.MakeLocalLabel (mapName, skipCounter: true);
				map_module.duplicate_map = duplicateMapName.Length == 0 ? null : generator.MakeLocalLabel (duplicateMapName, skipCounter: true);
				map_module.assembly_name = data.AssemblyNameLabel;

				NativeAssemblyGenerator.StructureWriteContext mapModuleStruct = generator.AddStructureArrayElement (mapModulesArray);
				generator.WriteStructure (mapModuleStruct, map_module);

				if (mapOutput != null) {
					WriteManagedMaps (generator, mapOutput, mapName, data.Types);
					if (data.DuplicateTypes.Count > 0) {
						WriteManagedMaps (generator, mapOutput, duplicateMapName, data.DuplicateTypes.Values);
					}
				}
			}
			generator.WriteSymbol (mapModulesArray, symbolName, local: false, alreadyInSection: true);
			generator.WriteCommentLine ("Managed to Java map: END");
		}

		void WriteJavaMap (NativeAssemblyGenerator generator, string symbolName)
		{
			generator.WriteEOL ();
			generator.WriteCommentLine ("Java to managed map: START");
			generator.WriteDataSection (symbolName, writable: false);

			var map_entry = new TypeMapJava {
				MaxJavaNameLength = mappingData.JavaNameWidth,
			};

			NativeAssemblyGenerator.StructureWriteContext javaMapArray = generator.StartStructureArray ();
			foreach (TypeMapGenerator.TypeMapReleaseEntry entry in mappingData.JavaTypes) {
				map_entry.module_index = (uint)entry.ModuleIndex;
				map_entry.type_token_id = entry.SkipInJavaToManaged ? 0 : entry.Token;
				map_entry.java_name = entry.JavaName;

				NativeAssemblyGenerator.StructureWriteContext mapEntryStruct = generator.AddStructureArrayElement (javaMapArray);
				generator.WriteStructure (mapEntryStruct, map_entry);
			}
			generator.WriteSymbol (javaMapArray, symbolName, local: false, alreadyInSection: true);
			generator.WriteCommentLine ("Java to managed map: END");
		}

		void WriteHeaderField (NativeAssemblyGenerator generator, string name, uint value)
		{
			generator.WriteEOL ();
			generator.WriteCommentLine ($"{name}: START");
			generator.WriteDataSection (name, writable: false);
			generator.WriteSymbol (name, value, hex: false, local: false);
			generator.WriteCommentLine ($"{name}: END");
		}
	}
}
