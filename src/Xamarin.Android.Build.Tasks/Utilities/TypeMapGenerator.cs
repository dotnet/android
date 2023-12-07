using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Java.Interop.Tools.Cecil;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class TypeMapGenerator
	{
		const string TypeMapMagicString = "XATS"; // Xamarin Android TypeMap
		const string TypeMapIndexMagicString = "XATI"; // Xamarin Android Typemap Index
		const uint TypeMapFormatVersion = 2; // Keep in sync with the value in src/monodroid/jni/xamarin-app.hh
		const string TypemapExtension = ".typemap";
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

		sealed class ReleaseGenerationState
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

			public void AddKnownAssembly (TypeDefinition td)
			{
				string assemblyName = GetAssemblyName (td);

				if (KnownAssemblies.ContainsKey (assemblyName)) {
					return;
				}

				KnownAssemblies.Add (assemblyName, ++assemblyId);
			}

			public string GetAssemblyName (TypeDefinition td) => td.Module.Assembly.FullName;
		}

		readonly Encoding outputEncoding;
		readonly byte[] moduleMagicString;
		readonly byte[] typemapIndexMagicString;
		readonly TaskLoggingHelper log;
		readonly NativeCodeGenState state;

		public IList<string> GeneratedBinaryTypeMaps { get; } = new List<string> ();

		public TypeMapGenerator (TaskLoggingHelper log, NativeCodeGenState state)
		{
			this.log = log ?? throw new ArgumentNullException (nameof (log));
			this.state = state ?? throw new ArgumentNullException (nameof (state));
			outputEncoding = Files.UTF8withoutBOM;
			moduleMagicString = outputEncoding.GetBytes (TypeMapMagicString);
			typemapIndexMagicString = outputEncoding.GetBytes (TypeMapIndexMagicString);
		}

		void UpdateApplicationConfig (TypeDefinition javaType)
		{
			if (state.JniAddNativeMethodRegistrationAttributePresent || !javaType.HasCustomAttributes) {
				return;
			}

			foreach (CustomAttribute ca in javaType.CustomAttributes) {
				if (!state.JniAddNativeMethodRegistrationAttributePresent && String.Compare ("JniAddNativeMethodRegistrationAttribute", ca.AttributeType.Name, StringComparison.Ordinal) == 0) {
					state.JniAddNativeMethodRegistrationAttributePresent = true;
					break;
				}
			}
		}

		public bool Generate (bool debugBuild, bool skipJniAddNativeMethodRegistrationAttributeScan, string outputDirectory, bool generateNativeAssembly)
		{
			if (String.IsNullOrEmpty (outputDirectory)) {
				throw new ArgumentException ("must not be null or empty", nameof (outputDirectory));
			}
			Directory.CreateDirectory (outputDirectory);

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
			return GenerateDebugFiles (skipJniAddNativeMethodRegistrationAttributeScan, outputDirectory);
		}

		bool GenerateDebugFiles (bool skipJniAddNativeMethodRegistrationAttributeScan, string outputDirectory)
		{
			var modules = new Dictionary<string, ModuleDebugData> (StringComparer.Ordinal);
			int maxModuleFileNameWidth = 0;
			int maxModuleNameWidth = 0;

			var javaDuplicates = new Dictionary<string, List<TypeMapDebugEntry>> (StringComparer.Ordinal);
			foreach (TypeDefinition td in state.AllJavaTypes) {
				UpdateApplicationConfig (td);
				string moduleName = td.Module.Assembly.Name.Name;
				ModuleDebugData module;

				if (!modules.TryGetValue (moduleName, out module)) {
					string outputFileName = $"{moduleName}{TypemapExtension}";
					module = new ModuleDebugData {
						EntryCount = 0,
						JavaNameWidth = 0,
						ManagedNameWidth = 0,
						JavaToManagedMap = new List<TypeMapDebugEntry> (),
						ManagedToJavaMap = new List<TypeMapDebugEntry> (),
						OutputFilePath = Path.Combine (outputDirectory, outputFileName),
						ModuleName = moduleName,
						ModuleNameBytes = outputEncoding.GetBytes (moduleName),
					};

					if (module.ModuleNameBytes.Length > maxModuleNameWidth)
						maxModuleNameWidth = module.ModuleNameBytes.Length;

					if (outputFileName.Length > maxModuleFileNameWidth)
						maxModuleFileNameWidth = outputFileName.Length;

					modules.Add (moduleName, module);
				}

				TypeMapDebugEntry entry = GetDebugEntry (td, state.TypeCache);
				HandleDebugDuplicates (javaDuplicates, entry, td, state.TypeCache);
				if (entry.JavaName.Length > module.JavaNameWidth)
					module.JavaNameWidth = (uint)entry.JavaName.Length + 1;

				if (entry.ManagedName.Length > module.ManagedNameWidth)
					module.ManagedNameWidth = (uint)entry.ManagedName.Length + 1;

				module.JavaToManagedMap.Add (entry);
				module.ManagedToJavaMap.Add (entry);
			}
			SyncDebugDuplicates (javaDuplicates);

			foreach (ModuleDebugData module in modules.Values) {
				PrepareDebugMaps (module);
			}

			string typeMapIndexPath = Path.Combine (outputDirectory, "typemap.index");
			using (var indexWriter = MemoryStreamPool.Shared.CreateBinaryWriter ()) {
				OutputModules (modules, indexWriter, maxModuleFileNameWidth + 1);
				indexWriter.Flush ();
				Files.CopyIfStreamChanged (indexWriter.BaseStream, typeMapIndexPath);
			}
			GeneratedBinaryTypeMaps.Add (typeMapIndexPath);

			var composer = new TypeMappingDebugNativeAssemblyGenerator (new ModuleDebugData ());
			GenerateNativeAssembly (composer, composer.Construct (), outputDirectory);

			return true;
		}

		bool GenerateDebugNativeAssembly (bool skipJniAddNativeMethodRegistrationAttributeScan, string outputDirectory)
		{
			var javaToManaged = new List<TypeMapDebugEntry> ();
			var managedToJava = new List<TypeMapDebugEntry> ();

			var javaDuplicates = new Dictionary<string, List<TypeMapDebugEntry>> (StringComparer.Ordinal);
			foreach (TypeDefinition td in state.AllJavaTypes) {
				UpdateApplicationConfig (td);

				TypeMapDebugEntry entry = GetDebugEntry (td, state.TypeCache);
				HandleDebugDuplicates (javaDuplicates, entry, td, state.TypeCache);

				javaToManaged.Add (entry);
				managedToJava.Add (entry);
			}
			SyncDebugDuplicates (javaDuplicates);

			var data = new ModuleDebugData {
				EntryCount = (uint)javaToManaged.Count,
				JavaToManagedMap = javaToManaged,
				ManagedToJavaMap = managedToJava,
			};

			PrepareDebugMaps (data);

			var composer = new TypeMappingDebugNativeAssemblyGenerator (data);
			GenerateNativeAssembly (composer, composer.Construct (), outputDirectory);

			return true;
		}

		void SyncDebugDuplicates (Dictionary<string, List<TypeMapDebugEntry>> javaDuplicates)
		{
			foreach (List<TypeMapDebugEntry> duplicates in javaDuplicates.Values) {
				if (duplicates.Count < 2) {
					continue;
				}

				// Java duplicates must all point to the same managed type
				// Managed types, however, must point back to the original Java type instead
				// File/assembly generator use the `DuplicateForJavaToManaged` field to know to which managed type the
				// duplicate Java type must be mapped.
				TypeMapDebugEntry template = duplicates [0];
				for (int i = 1; i < duplicates.Count; i++) {
					duplicates[i].DuplicateForJavaToManaged = template;
				}
			}
		}

		void HandleDebugDuplicates (Dictionary<string, List<TypeMapDebugEntry>> javaDuplicates, TypeMapDebugEntry entry, TypeDefinition td, TypeDefinitionCache cache)
		{
			List<TypeMapDebugEntry> duplicates;

			if (!javaDuplicates.TryGetValue (entry.JavaName, out duplicates)) {
				javaDuplicates.Add (entry.JavaName, new List<TypeMapDebugEntry> { entry });
			} else {
				duplicates.Add (entry);
				TypeMapDebugEntry oldEntry = duplicates[0];
				if (td.IsAbstract || td.IsInterface || oldEntry.TypeDefinition.IsAbstract || oldEntry.TypeDefinition.IsInterface) {
					if (td.IsAssignableFrom (oldEntry.TypeDefinition, cache)) {
						oldEntry.TypeDefinition = td;
						oldEntry.ManagedName = GetManagedTypeName (td);
					}
				}
			}
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

		TypeMapDebugEntry GetDebugEntry (TypeDefinition td, TypeDefinitionCache cache)
		{
			return new TypeMapDebugEntry {
				JavaName = Java.Interop.Tools.TypeNameMappings.JavaNativeTypeManager.ToJniName (td, cache),
				ManagedName = GetManagedTypeName (td),
				TypeDefinition = td,
				SkipInJavaToManaged = ShouldSkipInJavaToManaged (td),
			};
		}

		string GetManagedTypeName (TypeDefinition td)
		{
			// This is necessary because Mono runtime will return to us type name with a `.` for nested types (not a
			// `/` or a `+`. So, for instance, a type named `DefaultRenderer` found in the
			// `Xamarin.Forms.Platform.Android.Platform` class in the `Xamarin.Forms.Platform.Android` assembly will
			// be seen here as
			//
			//   Xamarin.Forms.Platform.Android.Platform/DefaultRenderer
			//
			// The managed land name for the type will be rendered as
			//
			//   Xamarin.Forms.Platform.Android.Platform+DefaultRenderer
			//
			// And this is the form that we need in the map file
			//
			string managedTypeName = td.FullName.Replace ('/', '+');

			return $"{managedTypeName}, {td.Module.Assembly.Name.Name}";
		}

		void ProcessReleaseType (ReleaseGenerationState genState, TypeDefinition td)
		{
			UpdateApplicationConfig (td);
			genState.AddKnownAssembly (td);

			// We must NOT use Guid here! The reason is that Guid sort order is different than its corresponding
			// byte array representation and on the runtime we need the latter in order to be able to binary search
			// through the module array.
			byte[] moduleUUID;
			if (!genState.MvidCache.TryGetValue (td.Module.Mvid, out moduleUUID)) {
				moduleUUID = td.Module.Mvid.ToByteArray ();
				genState.MvidCache.Add (td.Module.Mvid, moduleUUID);
			}

			Dictionary<byte[], ModuleReleaseData> tempModules = genState.TempModules;
			if (!tempModules.TryGetValue (moduleUUID, out ModuleReleaseData moduleData)) {
				moduleData = new ModuleReleaseData {
					Mvid = td.Module.Mvid,
					MvidBytes = moduleUUID,
					Assembly = td.Module.Assembly,
					AssemblyName = td.Module.Assembly.Name.Name,
					TypesScratch = new Dictionary<string, TypeMapReleaseEntry> (StringComparer.Ordinal),
					DuplicateTypes = new List<TypeMapReleaseEntry> (),
				};

				tempModules.Add (moduleUUID, moduleData);
			}

			string javaName = Java.Interop.Tools.TypeNameMappings.JavaNativeTypeManager.ToJniName (td, state.TypeCache);
			// We will ignore generic types and interfaces when generating the Java to Managed map, but we must not
			// omit them from the table we output - we need the same number of entries in both java-to-managed and
			// managed-to-java tables.  `SkipInJavaToManaged` set to `true` will cause the native assembly generator
			// to output `0` as the token id for the type, thus effectively causing the runtime unable to match such
			// a Java type name to a managed type. This fixes https://github.com/xamarin/xamarin-android/issues/4660
			var entry = new TypeMapReleaseEntry {
				JavaName = javaName,
				ManagedTypeName = td.FullName,
				Token = td.MetadataToken.ToUInt32 (),
				AssemblyNameIndex = genState.KnownAssemblies [genState.GetAssemblyName (td)],
				SkipInJavaToManaged = ShouldSkipInJavaToManaged (td),
			};

			if (moduleData.TypesScratch.ContainsKey (entry.JavaName)) {
				// This is disabled because it costs a lot of time (around 150ms per standard XF Integration app
				// build) and has no value for the end user. The message is left here because it may be useful to us
				// in our devloop at some point.
				//logger ($"Warning: duplicate Java type name '{entry.JavaName}' in assembly '{moduleData.AssemblyName}' (new token: {entry.Token}).");
				moduleData.DuplicateTypes.Add (entry);
			} else {
				moduleData.TypesScratch.Add (entry.JavaName, entry);
			}
		}

		bool GenerateRelease (bool skipJniAddNativeMethodRegistrationAttributeScan, string outputDirectory)
		{
			var genState = new ReleaseGenerationState ();
			foreach (TypeDefinition td in state.AllJavaTypes) {
				ProcessReleaseType (genState, td);
			}

			ModuleReleaseData[] modules = genState.TempModules.Values.ToArray ();
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

			var composer = new TypeMappingReleaseNativeAssemblyGenerator (new NativeTypeMappingData (modules));
			GenerateNativeAssembly (composer, composer.Construct (), outputDirectory);

			return true;
		}

		bool ShouldSkipInJavaToManaged (TypeDefinition td)
		{
			return td.IsInterface || td.HasGenericParameters;
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
