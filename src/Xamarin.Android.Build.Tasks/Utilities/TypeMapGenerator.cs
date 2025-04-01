using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class TypeMapGenerator
	{
		const string TypeMapMagicString = "XATS"; // Xamarin Android TypeMap
		const string TypeMapIndexMagicString = "XATI"; // Xamarin Android Typemap Index
		const uint TypeMapFormatVersion = 2; // Keep in sync with the value in src/monodroid/jni/xamarin-app.hh
		const uint InvalidJavaToManagedMappingIndex = UInt32.MaxValue;

		internal sealed class ModuleUUIDArrayComparer : IComparer<ModuleReleaseData>
		{
			int Compare (byte[] left, byte[] right)
			{
				int ret;

				for (int i = 0; i < 16; i++) {
					ret = left[i].CompareTo (right[i]);
					if (ret != 0)
						return ret;
				}

				return 0;
			}

			public int Compare (ModuleReleaseData left, ModuleReleaseData right)
			{
				return Compare (left.MvidBytes, right.MvidBytes);
			}
		}

		internal sealed class TypeMapReleaseEntry
		{
			public string JavaName;
			public string ManagedTypeName;
			public uint Token;
			public int AssemblyNameIndex = -1;
			public int ModuleIndex = -1;
			public bool SkipInJavaToManaged;
		}

		internal sealed class ModuleReleaseData
		{
			public Guid Mvid;
			public byte[] MvidBytes;
			public AssemblyDefinition Assembly;
			public TypeMapReleaseEntry[] Types;
			public List<TypeMapReleaseEntry> DuplicateTypes;
			public string AssemblyName;
			public string OutputFilePath;

			public Dictionary<string, TypeMapReleaseEntry> TypesScratch;
		}

		internal sealed class TypeMapDebugEntry
		{
			public string JavaName;
			public string JavaLabel;
			public string ManagedName;
			public string ManagedLabel;
			public int JavaIndex;
			public int ManagedIndex;
			public TypeDefinition TypeDefinition;
			public bool SkipInJavaToManaged;
			public TypeMapDebugEntry DuplicateForJavaToManaged;

			public override string ToString ()
			{
				return $"TypeMapDebugEntry{{JavaName={JavaName}, ManagedName={ManagedName}, JavaIndex={JavaIndex}, ManagedIndex={ManagedIndex}, SkipInJavaToManaged={SkipInJavaToManaged}, DuplicateForJavaToManaged={DuplicateForJavaToManaged}}}";
			}
		}

		// Widths include the terminating nul character but not the padding!
		internal sealed class ModuleDebugData
		{
			public uint EntryCount;
			public uint JavaNameWidth;
			public uint ManagedNameWidth;
			public List<TypeMapDebugEntry> JavaToManagedMap;
			public List<TypeMapDebugEntry> ManagedToJavaMap;
			public string OutputFilePath;
			public string ModuleName;
			public byte[] ModuleNameBytes;
		}

		internal sealed class ReleaseGenerationState
		{
			int assemblyId = 0;

			public readonly Dictionary<string, int> KnownAssemblies;
			public readonly Dictionary <Guid, byte[]> MvidCache;
			public readonly Dictionary<byte[], ModuleReleaseData> TempModules;

			public ReleaseGenerationState ()
			{
				KnownAssemblies = new Dictionary<string, int> (StringComparer.Ordinal);
				MvidCache = new Dictionary<Guid, byte[]> ();
				TempModules = new Dictionary<byte[], ModuleReleaseData> ();
			}

			public void AddKnownAssembly (string assemblyName)
			{
				if (KnownAssemblies.ContainsKey (assemblyName)) {
					return;
				}

				KnownAssemblies.Add (assemblyName, ++assemblyId);
			}
		}

		readonly Encoding outputEncoding;
		readonly byte[] moduleMagicString;
		readonly byte[] typemapIndexMagicString;
		readonly TaskLoggingHelper log;
		readonly NativeCodeGenState state;
		readonly AndroidRuntime runtime;

		public IList<string> GeneratedBinaryTypeMaps { get; } = new List<string> ();

		public TypeMapGenerator (TaskLoggingHelper log, NativeCodeGenState state, AndroidRuntime runtime)
		{
			this.log = log ?? throw new ArgumentNullException (nameof (log));
			this.state = state ?? throw new ArgumentNullException (nameof (state));
			this.runtime = runtime;
			outputEncoding = Files.UTF8withoutBOM;
			moduleMagicString = outputEncoding.GetBytes (TypeMapMagicString);
			typemapIndexMagicString = outputEncoding.GetBytes (TypeMapIndexMagicString);
		}

		public bool Generate (bool debugBuild, bool skipJniAddNativeMethodRegistrationAttributeScan, string outputDirectory, bool generateNativeAssembly)
		{
			if (String.IsNullOrEmpty (outputDirectory)) {
				throw new ArgumentException ("must not be null or empty", nameof (outputDirectory));
			}
			Directory.CreateDirectory (outputDirectory);

			state.JniAddNativeMethodRegistrationAttributePresent = skipJniAddNativeMethodRegistrationAttributeScan;
			string typemapsOutputDirectory = Path.Combine (outputDirectory, "typemaps");
			if (debugBuild) {
				return GenerateDebug (skipJniAddNativeMethodRegistrationAttributeScan, typemapsOutputDirectory, generateNativeAssembly);
			}

			return GenerateRelease (skipJniAddNativeMethodRegistrationAttributeScan, typemapsOutputDirectory);
		}

		bool GenerateDebug (bool skipJniAddNativeMethodRegistrationAttributeScan, string outputDirectory, bool generateNativeAssembly)
		{
			if (generateNativeAssembly) {
				return GenerateDebugNativeAssembly (skipJniAddNativeMethodRegistrationAttributeScan, outputDirectory);
			}

			// Debug builds which don't put typemaps in native assembly must output data files in architecture-specific
			// subdirectories, so that fastdev can properly sync them to the device.
			// The (empty) native assembly files, however, must still be generated in the usual directory.
			return GenerateDebugFiles (
				skipJniAddNativeMethodRegistrationAttributeScan,
				typemapFilesOutputDirectory: Path.Combine (outputDirectory, MonoAndroidHelper.ArchToAbi (state.TargetArch)),
				llFilesOutputDirectory: outputDirectory
			);
		}

		bool GenerateDebugFiles (bool skipJniAddNativeMethodRegistrationAttributeScan, string typemapFilesOutputDirectory, string llFilesOutputDirectory)
		{
			var modules = TypeMapCecilAdapter.GetDebugModules (state, typemapFilesOutputDirectory, outputEncoding, out var maxModuleFileNameWidth);

			foreach (ModuleDebugData module in modules.Values) {
				PrepareDebugMaps (module);
			}

			string typeMapIndexPath = Path.Combine (typemapFilesOutputDirectory, "typemap.index");
			using (var indexWriter = MemoryStreamPool.Shared.CreateBinaryWriter ()) {
				OutputModules (modules, indexWriter, maxModuleFileNameWidth + 1);
				indexWriter.Flush ();
				Files.CopyIfStreamChanged (indexWriter.BaseStream, typeMapIndexPath);
			}
			GeneratedBinaryTypeMaps.Add (typeMapIndexPath);

			var composer = new TypeMappingDebugNativeAssemblyGenerator (log, new ModuleDebugData ());
			GenerateNativeAssembly (composer, composer.Construct (), llFilesOutputDirectory);

			return true;
		}

		bool GenerateDebugNativeAssembly (bool skipJniAddNativeMethodRegistrationAttributeScan, string outputDirectory)
		{
			(var javaToManaged, var managedToJava) = TypeMapCecilAdapter.GetDebugNativeEntries (state);

			var data = new ModuleDebugData {
				EntryCount = (uint)javaToManaged.Count,
				JavaToManagedMap = javaToManaged,
				ManagedToJavaMap = managedToJava,
			};

			PrepareDebugMaps (data);

			var composer = new TypeMappingDebugNativeAssemblyGenerator (log, data);
			GenerateNativeAssembly (composer, composer.Construct (), outputDirectory);

			return true;
		}

		void PrepareDebugMaps (ModuleDebugData module)
		{
			module.JavaToManagedMap.Sort ((TypeMapDebugEntry a, TypeMapDebugEntry b) => String.Compare (a.JavaName, b.JavaName, StringComparison.Ordinal));
			module.ManagedToJavaMap.Sort ((TypeMapDebugEntry a, TypeMapDebugEntry b) => String.Compare (a.ManagedName, b.ManagedName, StringComparison.Ordinal));

			for (int i = 0; i < module.JavaToManagedMap.Count; i++) {
				module.JavaToManagedMap[i].JavaIndex = i;
			}

			for (int i = 0; i < module.ManagedToJavaMap.Count; i++) {
				module.ManagedToJavaMap[i].ManagedIndex = i;
			}
		}

		bool GenerateRelease (bool skipJniAddNativeMethodRegistrationAttributeScan, string outputDirectory)
		{
			var genState = TypeMapCecilAdapter.GetReleaseGenerationState (state);

			ModuleReleaseData [] modules = genState.TempModules.Values.ToArray ();
			Array.Sort (modules, new ModuleUUIDArrayComparer ());

			foreach (ModuleReleaseData module in modules) {
				if (module.TypesScratch.Count == 0) {
					module.Types = Array.Empty<TypeMapReleaseEntry> ();
					continue;
				}

				// No need to sort here, the LLVM IR generator will compute hashes and sort
				// the array on write.
				module.Types = module.TypesScratch.Values.ToArray ();
			}

			LLVMIR.LlvmIrComposer composer = runtime switch {
				AndroidRuntime.MonoVM => new TypeMappingReleaseNativeAssemblyGenerator (log, new NativeTypeMappingData (log, modules)),
				AndroidRuntime.CoreCLR => new TypeMappingReleaseNativeAssemblyGeneratorCLR (log, new NativeTypeMappingData (log, modules)),
				_ => throw new NotSupportedException ($"Internal error: unsupported runtime {runtime}")
			};

			GenerateNativeAssembly (composer, composer.Construct (), outputDirectory);

			return true;
		}

		string GetOutputFilePath (string baseFileName, AndroidTargetArch arch) => $"{baseFileName}.{MonoAndroidHelper.ArchToAbi (arch)}.ll";

		void GenerateNativeAssembly (LLVMIR.LlvmIrComposer composer, LLVMIR.LlvmIrModule typeMapModule, string baseFileName)
		{
			WriteNativeAssembly (
				composer,
				typeMapModule,
				GetOutputFilePath (baseFileName, state.TargetArch)
			);
		}

		void WriteNativeAssembly (LLVMIR.LlvmIrComposer composer, LLVMIR.LlvmIrModule typeMapModule, string outputFile)
		{
			// TODO: each .ll file should have a comment which lists paths to all the DLLs that were used to generate
			// the native code
			using (var sw = MemoryStreamPool.Shared.CreateStreamWriter ()) {
				try {
					composer.Generate (typeMapModule, state.TargetArch, sw, outputFile);
				} catch {
					throw;
				} finally {
					sw.Flush ();
					Files.CopyIfStreamChanged (sw.BaseStream, outputFile);
				}
			}
		}

		// Binary index file format, all data is little-endian:
		//
		//  [Magic string]             # XATI
		//  [Format version]           # 32-bit unsigned integer, 4 bytes
		//  [Entry count]              # 32-bit unsigned integer, 4 bytes
		//  [Module file name width]   # 32-bit unsigned integer, 4 bytes
		//  [Index entries]            # Format described below, [Entry count] entries
		//
		// Index entry format:
		//
		//  [File name]<NUL>
		//
		// Where:
		//
		//   [File name] is right-padded with <NUL> characters to the [Module file name width] boundary.
		//
		void OutputModules (Dictionary<string, ModuleDebugData> modules, BinaryWriter indexWriter, int moduleFileNameWidth)
		{
			indexWriter.Write (typemapIndexMagicString);
			indexWriter.Write (TypeMapFormatVersion);
			indexWriter.Write (modules.Count);
			indexWriter.Write (moduleFileNameWidth);

			foreach (ModuleDebugData module in modules.Values) {
				OutputModule (module);

				string outputFilePath = Path.GetFileName (module.OutputFilePath);
				indexWriter.Write (outputEncoding.GetBytes (outputFilePath));
				PadField (indexWriter, outputFilePath.Length, moduleFileNameWidth);
			}
		}

		void OutputModule (ModuleDebugData moduleData)
		{
			if (moduleData.JavaToManagedMap.Count == 0)
				return;

			using (var bw = MemoryStreamPool.Shared.CreateBinaryWriter ()) {
				OutputModule (bw, moduleData);
				bw.Flush ();
				Files.CopyIfStreamChanged (bw.BaseStream, moduleData.OutputFilePath);
			}
			GeneratedBinaryTypeMaps.Add (moduleData.OutputFilePath);
		}

		// Binary file format, all data is little-endian:
		//
		//  [Magic string]                    # XATS
		//  [Format version]                  # 32-bit unsigned integer, 4 bytes
		//  [Entry count]                     # 32-bit unsigned integer, 4 bytes
		//  [Java type name width]            # 32-bit unsigned integer, 4 bytes
		//  [Managed type name width]         # 32-bit unsigned integer, 4 bytes
		//  [Assembly name size]              # 32-bit unsigned integer, 4 bytes
		//  [Assembly name]                   # Non-null terminated assembly name
		//  [Java-to-managed map]             # Format described below, [Entry count] entries
		//  [Managed-to-java map]             # Format described below, [Entry count] entries
		//
		// Java-to-managed map format:
		//
		//    [Java type name]<NUL>[Managed type table index]
		//
		//  Each name is padded with <NUL> to the width specified in the [Java type name width] field above.
		//  Names are written without the size prefix, instead they are always terminated with a nul character
		//  to make it easier and faster to handle by the native runtime.
		//
		//  Each [Managed type table index] is an unsigned 32-bit integer, 4 bytes
		//
		//
		// Managed-to-java map is identical to the [Java-to-managed] table above, with the exception of the index
		// pointing to the Java name table.
		//
		void OutputModule (BinaryWriter bw, ModuleDebugData moduleData)
		{
			if ((uint)moduleData.JavaToManagedMap.Count == InvalidJavaToManagedMappingIndex) {
				throw new InvalidOperationException ($"Too many types in module {moduleData.ModuleName}");
			}

			bw.Write (moduleMagicString);
			bw.Write (TypeMapFormatVersion);
			bw.Write (moduleData.JavaToManagedMap.Count);
			bw.Write (moduleData.JavaNameWidth);
			bw.Write (moduleData.ManagedNameWidth);
			bw.Write (moduleData.ModuleNameBytes.Length);
			bw.Write (moduleData.ModuleNameBytes);

			foreach (TypeMapDebugEntry entry in moduleData.JavaToManagedMap) {
				bw.Write (outputEncoding.GetBytes (entry.JavaName));
				PadField (bw, entry.JavaName.Length, (int)moduleData.JavaNameWidth);

				TypeMapGenerator.TypeMapDebugEntry managedEntry = entry.DuplicateForJavaToManaged != null ? entry.DuplicateForJavaToManaged : entry;
				bw.Write (managedEntry.SkipInJavaToManaged ? InvalidJavaToManagedMappingIndex : (uint)managedEntry.ManagedIndex);
			}

			foreach (TypeMapDebugEntry entry in moduleData.ManagedToJavaMap) {
				bw.Write (outputEncoding.GetBytes (entry.ManagedName));
				PadField (bw, entry.ManagedName.Length, (int)moduleData.ManagedNameWidth);
				bw.Write (entry.JavaIndex);
			}
		}

		void PadField (BinaryWriter bw, int valueWidth, int maxWidth)
		{
			for (int i = 0; i < maxWidth - valueWidth; i++) {
				bw.Write ((byte)0);
			}
		}
	}
}
