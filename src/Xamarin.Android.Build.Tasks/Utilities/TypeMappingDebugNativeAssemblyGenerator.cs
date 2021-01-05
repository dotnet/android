
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	class TypeMappingDebugNativeAssemblyGenerator : NativeAssemblyGenerator
	{
		const string JavaToManagedSymbol = "map_java_to_managed";
		const string ManagedToJavaSymbol = "map_managed_to_java";
		const string TypeMapSymbol = "type_map"; // MUST match src/monodroid/xamarin-app.hh

		readonly string baseFileName;
		readonly bool sharedBitsWritten;
		readonly TypeMapGenerator.ModuleDebugData data;

		public TypeMappingDebugNativeAssemblyGenerator (NativeAssemblerTargetProvider targetProvider, TypeMapGenerator.ModuleDebugData data, string baseFileName, bool sharedBitsWritten, bool sharedIncludeUsesAbiPrefix = false)
			: base (targetProvider, baseFileName, sharedIncludeUsesAbiPrefix)
		{
			if (String.IsNullOrEmpty (baseFileName))
				throw new ArgumentException("must not be null or empty", nameof (baseFileName));
			this.data = data ?? throw new ArgumentNullException (nameof (data));

			this.baseFileName = baseFileName;
			this.sharedBitsWritten = sharedBitsWritten;
		}

		protected override void WriteSymbols (StreamWriter output)
		{
			bool haveJavaToManaged = data.JavaToManagedMap != null && data.JavaToManagedMap.Count > 0;
			bool haveManagedToJava = data.ManagedToJavaMap != null && data.ManagedToJavaMap.Count > 0;

			using (var sharedOutput = MemoryStreamPool.Shared.CreateStreamWriter (output.Encoding)) {
				WriteSharedBits (sharedOutput, haveJavaToManaged, haveManagedToJava);
				sharedOutput.Flush ();
				Files.CopyIfStreamChanged (sharedOutput.BaseStream, SharedIncludeFile);
			}

			if (haveJavaToManaged || haveManagedToJava) {
				output.Write (Indent);
				output.Write (".include");
				output.Write (Indent);
				output.Write ('"');
				output.Write (Path.GetFileName (SharedIncludeFile));
				output.WriteLine ('"');

				output.WriteLine ();
			}

			uint size = 0;
			WriteCommentLine (output, "Managed to java map: START", indent: false);
			WriteSection (output, $".data.rel.{ManagedToJavaSymbol}", hasStrings: false, writable: true);
			WriteStructureSymbol (output, ManagedToJavaSymbol, alignBits: TargetProvider.DebugTypeMapAlignBits, isGlobal: false);
			if (haveManagedToJava) {
				foreach (TypeMapGenerator.TypeMapDebugEntry entry in data.ManagedToJavaMap) {
					size += WritePointer (output, entry.ManagedLabel);
					size += WritePointer (output, entry.JavaLabel);
				}
			}
			WriteStructureSize (output, ManagedToJavaSymbol, size, alwaysWriteSize: true);
			WriteCommentLine (output, "Managed to java map: END", indent: false);
			output.WriteLine ();

			size = 0;
			WriteCommentLine (output, "Java to managed map: START", indent: false);
			WriteSection (output, $".data.rel.{JavaToManagedSymbol}", hasStrings: false, writable: true);
			WriteStructureSymbol (output, JavaToManagedSymbol, alignBits: TargetProvider.DebugTypeMapAlignBits, isGlobal: false);
			if (haveJavaToManaged) {
				foreach (TypeMapGenerator.TypeMapDebugEntry entry in data.JavaToManagedMap) {
					size += WritePointer (output, entry.JavaLabel);
					size += WritePointer (output, entry.SkipInJavaToManaged ? null : entry.ManagedLabel);
				}
			}
			WriteStructureSize (output, JavaToManagedSymbol, size, alwaysWriteSize: true);
			WriteCommentLine (output, "Java to managed map: END", indent: false);
			output.WriteLine ();

			// MUST match src/monodroid/xamarin-app.hh
			WriteCommentLine (output, "TypeMap structure");
			WriteSection (output, $".data.rel.ro.{TypeMapSymbol}", hasStrings: false, writable: true);
			WriteStructureSymbol (output, TypeMapSymbol, alignBits: TargetProvider.DebugTypeMapAlignBits, isGlobal: true);

			size = WriteStructure (output, packed: false, structureWriter: () => WriteTypeMapStruct (output));

			WriteStructureSize (output, TypeMapSymbol, size);
		}

		// MUST match the TypeMap struct from src/monodroid/xamarin-app.hh
		uint WriteTypeMapStruct (StreamWriter output)
		{
			uint size = 0;

			WriteCommentLine (output, "entry_count");
			size += WriteData (output, data.EntryCount);

			WriteCommentLine (output, "assembly_name (unused in this mode)");
			size += WritePointer (output);

			WriteCommentLine (output, "data (unused in this mode)");
			size += WritePointer (output);

			WriteCommentLine (output, "java_to_managed");
			size += WritePointer (output, JavaToManagedSymbol);

			WriteCommentLine (output, "managed_to_java");
			size += WritePointer (output, ManagedToJavaSymbol);

			return size;
		}

		void WriteSharedBits (StreamWriter output, bool haveJavaToManaged, bool haveManagedToJava)
		{
			string label;

			if (haveJavaToManaged) {
				WriteCommentLine (output, "Java type names: START");
				foreach (TypeMapGenerator.TypeMapDebugEntry entry in data.JavaToManagedMap) {
					label = $"java_type_name.{entry.JavaIndex}";
					WriteData (output, entry.JavaName, label, isGlobal: false);
					entry.JavaLabel = MakeLocalLabel (label);
					output.WriteLine ();
				}
				WriteCommentLine (output, "Java type names: END");
				output.WriteLine ();
			}

			if (haveManagedToJava) {
				WriteCommentLine (output, "Managed type names: START");
				foreach (TypeMapGenerator.TypeMapDebugEntry entry in data.ManagedToJavaMap) {
					label = $"managed_type_name.{entry.ManagedIndex}";
					WriteData (output, entry.ManagedName, label, isGlobal: false);
					entry.ManagedLabel = MakeLocalLabel (label);
				}
				WriteCommentLine (output, "Managed type names: END");
			}
		}
	}
}
