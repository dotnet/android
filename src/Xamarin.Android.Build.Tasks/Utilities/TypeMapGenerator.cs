using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Java.Interop.Tools.Cecil;
using Mono.Cecil;

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

		internal sealed class TypeMapEntryArrayComparer : IComparer<TypeMapReleaseEntry>
		{
			public int Compare (TypeMapReleaseEntry left, TypeMapReleaseEntry right)
			{
				return String.CompareOrdinal (left.JavaName, right.JavaName);
			}
		}

		internal sealed class TypeMapReleaseEntry
		{
			public string JavaName;
			public int JavaNameLength;
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
			public Dictionary<uint, TypeMapReleaseEntry> DuplicateTypes;
			public string AssemblyName;
			public string AssemblyNameLabel;
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

		Action<string> logger;
		Encoding outputEncoding;
		byte[] moduleMagicString;
		byte[] typemapIndexMagicString;
		string[] supportedAbis;

		public IList<string> GeneratedBinaryTypeMaps { get; } = new List<string> ();

		public TypeMapGenerator (Action<string> logger, string[] supportedAbis)
		{
			this.logger = logger ?? throw new ArgumentNullException (nameof (logger));
			if (supportedAbis == null)
				throw new ArgumentNullException (nameof (supportedAbis));
			this.supportedAbis = supportedAbis;

			outputEncoding = MonoAndroidHelper.UTF8withoutBOM;
			moduleMagicString = outputEncoding.GetBytes (TypeMapMagicString);
			typemapIndexMagicString = outputEncoding.GetBytes (TypeMapIndexMagicString);
		}

		void UpdateApplicationConfig (TypeDefinition javaType, ApplicationConfigTaskState appConfState)
		{
			if (appConfState.JniAddNativeMethodRegistrationAttributePresent)
				return;
			if (!javaType.HasCustomAttributes)
				return;

			foreach (CustomAttribute ca in javaType.CustomAttributes) {
				if (!appConfState.JniAddNativeMethodRegistrationAttributePresent && String.Compare ("JniAddNativeMethodRegistrationAttribute", ca.AttributeType.Name, StringComparison.Ordinal) == 0) {
					appConfState.JniAddNativeMethodRegistrationAttributePresent = true;
					break;
				}
			}
		}

		public bool Generate (bool debugBuild, bool skipJniAddNativeMethodRegistrationAttributeScan, List<TypeDefinition> javaTypes, TypeDefinitionCache cache, string outputDirectory, bool generateNativeAssembly, out ApplicationConfigTaskState appConfState)
		{
			if (String.IsNullOrEmpty (outputDirectory))
				throw new ArgumentException ("must not be null or empty", nameof (outputDirectory));

			if (!Directory.Exists (outputDirectory))
				Directory.CreateDirectory (outputDirectory);

			appConfState = new ApplicationConfigTaskState {
				JniAddNativeMethodRegistrationAttributePresent = skipJniAddNativeMethodRegistrationAttributeScan
			};

			string typemapsOutputDirectory = Path.Combine (outputDirectory, "typemaps");

			if (debugBuild) {
				return GenerateDebug (skipJniAddNativeMethodRegistrationAttributeScan, javaTypes, cache, typemapsOutputDirectory, generateNativeAssembly, appConfState);
			}

			return GenerateRelease (skipJniAddNativeMethodRegistrationAttributeScan, javaTypes, typemapsOutputDirectory, appConfState);
		}

		bool GenerateDebug (bool skipJniAddNativeMethodRegistrationAttributeScan, List<TypeDefinition> javaTypes, TypeDefinitionCache cache, string outputDirectory, bool generateNativeAssembly, ApplicationConfigTaskState appConfState)
		{
			if (generateNativeAssembly)
				return GenerateDebugNativeAssembly (skipJniAddNativeMethodRegistrationAttributeScan, javaTypes, cache, outputDirectory, appConfState);
			return GenerateDebugFiles (skipJniAddNativeMethodRegistrationAttributeScan, javaTypes, cache, outputDirectory, appConfState);
		}

		bool GenerateDebugFiles (bool skipJniAddNativeMethodRegistrationAttributeScan, List<TypeDefinition> javaTypes, TypeDefinitionCache cache, string outputDirectory, ApplicationConfigTaskState appConfState)
		{
			var modules = new Dictionary<string, ModuleDebugData> (StringComparer.Ordinal);
			int maxModuleFileNameWidth = 0;
			int maxModuleNameWidth = 0;

			var javaDuplicates = new Dictionary<string, List<TypeMapDebugEntry>> (StringComparer.Ordinal);
			foreach (TypeDefinition td in javaTypes) {
				UpdateApplicationConfig (td, appConfState);
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

				TypeMapDebugEntry entry = GetDebugEntry (td);
				HandleDebugDuplicates (javaDuplicates, entry, td, cache);
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
				MonoAndroidHelper.CopyIfStreamChanged (indexWriter.BaseStream, typeMapIndexPath);
			}
			GeneratedBinaryTypeMaps.Add (typeMapIndexPath);

			GenerateNativeAssembly (
				(NativeAssemblerTargetProvider asmTargetProvider, bool sharedBitsWritten, bool sharedIncludeUsesAbiPrefix) => {
					return new TypeMappingDebugNativeAssemblyGenerator (asmTargetProvider, new ModuleDebugData (), outputDirectory, sharedBitsWritten);
				}
			);

			return true;
		}

		bool GenerateDebugNativeAssembly (bool skipJniAddNativeMethodRegistrationAttributeScan, List<TypeDefinition> javaTypes, TypeDefinitionCache cache, string outputDirectory, ApplicationConfigTaskState appConfState)
		{
			var javaToManaged = new List<TypeMapDebugEntry> ();
			var managedToJava = new List<TypeMapDebugEntry> ();

			var javaDuplicates = new Dictionary<string, List<TypeMapDebugEntry>> (StringComparer.Ordinal);
			foreach (TypeDefinition td in javaTypes) {
				UpdateApplicationConfig (td, appConfState);

				TypeMapDebugEntry entry = GetDebugEntry (td);
				HandleDebugDuplicates (javaDuplicates, entry, td, cache);

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
			GenerateNativeAssembly (
				(NativeAssemblerTargetProvider asmTargetProvider, bool sharedBitsWritten, bool sharedIncludeUsesAbiPrefix) => {
					return new TypeMappingDebugNativeAssemblyGenerator (asmTargetProvider, data, outputDirectory, sharedBitsWritten, sharedIncludeUsesAbiPrefix);
				}
			);

			return true;
		}

		void SyncDebugDuplicates (Dictionary<string, List<TypeMapDebugEntry>> javaDuplicates)
		{
			foreach (List<TypeMapDebugEntry> duplicates in javaDuplicates.Values) {
				if (duplicates.Count < 2) {
					continue;
				}

				TypeMapDebugEntry template = duplicates [0];
				for (int i = 1; i < duplicates.Count; i++) {
					duplicates[i].TypeDefinition = template.TypeDefinition;
					duplicates[i].ManagedName = template.ManagedName;
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

		TypeMapDebugEntry GetDebugEntry (TypeDefinition td)
		{
			return new TypeMapDebugEntry {
				JavaName = Java.Interop.Tools.TypeNameMappings.JavaNativeTypeManager.ToJniName (td),
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

		bool GenerateRelease (bool skipJniAddNativeMethodRegistrationAttributeScan, List<TypeDefinition> javaTypes, string outputDirectory, ApplicationConfigTaskState appConfState)
		{
			int assemblyId = 0;
			int maxJavaNameLength = 0;
			var knownAssemblies = new Dictionary<string, int> (StringComparer.Ordinal);
			var tempModules = new Dictionary<byte[], ModuleReleaseData> ();
			Dictionary <AssemblyDefinition, int> moduleCounter = null;
			var mvidCache = new Dictionary <Guid, byte[]> ();

			foreach (TypeDefinition td in javaTypes) {
				UpdateApplicationConfig (td, appConfState);

				string assemblyName = td.Module.Assembly.FullName;

				if (!knownAssemblies.ContainsKey (assemblyName)) {
					assemblyId++;
					knownAssemblies.Add (assemblyName, assemblyId);
				}

				// We must NOT use Guid here! The reason is that Guid sort order is different than its corresponding
				// byte array representation and on the runtime we need the latter in order to be able to binary search
				// through the module array.
				byte[] moduleUUID;
				if (!mvidCache.TryGetValue (td.Module.Mvid, out moduleUUID)) {
					moduleUUID = td.Module.Mvid.ToByteArray ();
					mvidCache.Add (td.Module.Mvid, moduleUUID);
				}

				ModuleReleaseData moduleData;
				if (!tempModules.TryGetValue (moduleUUID, out moduleData)) {
					if (moduleCounter == null)
						moduleCounter = new Dictionary <AssemblyDefinition, int> ();

					moduleData = new ModuleReleaseData {
						Mvid = td.Module.Mvid,
						MvidBytes = moduleUUID,
						Assembly = td.Module.Assembly,
						AssemblyName = td.Module.Assembly.Name.Name,
						TypesScratch = new Dictionary<string, TypeMapReleaseEntry> (StringComparer.Ordinal),
						DuplicateTypes = new Dictionary<uint, TypeMapReleaseEntry> (),
					};
					tempModules.Add (moduleUUID, moduleData);
				}

				string javaName = Java.Interop.Tools.TypeNameMappings.JavaNativeTypeManager.ToJniName (td);
				// We will ignore generic types and interfaces when generating the Java to Managed map, but we must not
				// omit them from the table we output - we need the same number of entries in both java-to-managed and
				// managed-to-java tables.  `SkipInJavaToManaged` set to `true` will cause the native assembly generator
				// to output `0` as the token id for the type, thus effectively causing the runtime unable to match such
				// a Java type name to a managed type. This fixes https://github.com/xamarin/xamarin-android/issues/4660
				var entry = new TypeMapReleaseEntry {
					JavaName = javaName,
					JavaNameLength = outputEncoding.GetByteCount (javaName),
					ManagedTypeName = td.FullName,
					Token = td.MetadataToken.ToUInt32 (),
					AssemblyNameIndex = knownAssemblies [assemblyName],
					SkipInJavaToManaged = ShouldSkipInJavaToManaged (td),
				};

				if (entry.JavaNameLength > maxJavaNameLength)
					maxJavaNameLength = entry.JavaNameLength;

				if (moduleData.TypesScratch.ContainsKey (entry.JavaName)) {
					// This is disabled because it costs a lot of time (around 150ms per standard XF Integration app
					// build) and has no value for the end user. The message is left here because it may be useful to us
					// in our devloop at some point.
					//logger ($"Warning: duplicate Java type name '{entry.JavaName}' in assembly '{moduleData.AssemblyName}' (new token: {entry.Token}).");
					moduleData.DuplicateTypes.Add (entry.Token, entry);
				} else
					moduleData.TypesScratch.Add (entry.JavaName, entry);
			}

			var modules = tempModules.Values.ToArray ();
			Array.Sort (modules, new ModuleUUIDArrayComparer ());

			var typeMapEntryComparer = new TypeMapEntryArrayComparer ();
			foreach (ModuleReleaseData module in modules) {
				if (module.TypesScratch.Count == 0) {
					module.Types = new TypeMapReleaseEntry[0];
					continue;
				}

				module.Types = module.TypesScratch.Values.ToArray ();
				Array.Sort (module.Types, typeMapEntryComparer);
			}

			NativeTypeMappingData data;
			data = new NativeTypeMappingData (logger, modules, maxJavaNameLength + 1);

			GenerateNativeAssembly (
				(NativeAssemblerTargetProvider asmTargetProvider, bool sharedBitsWritten, bool sharedIncludeUsesAbiPrefix) => {
					return new TypeMappingReleaseNativeAssemblyGenerator (asmTargetProvider, data, outputDirectory, sharedBitsWritten, sharedIncludeUsesAbiPrefix);
				}
			);

			return true;
		}

		bool ShouldSkipInJavaToManaged (TypeDefinition td)
		{
			return td.IsInterface || td.HasGenericParameters;
		}

		void GenerateNativeAssembly (Func<NativeAssemblerTargetProvider, bool, bool, NativeAssemblyGenerator> getGenerator)
		{
			NativeAssemblerTargetProvider asmTargetProvider;
			bool sharedBitsWritten = false;
			bool sharedIncludeUsesAbiPrefix;
			foreach (string abi in supportedAbis) {
				sharedIncludeUsesAbiPrefix = false;
				switch (abi.Trim ()) {
					case "armeabi-v7a":
						asmTargetProvider = new ARMNativeAssemblerTargetProvider (is64Bit: false);
						sharedIncludeUsesAbiPrefix = true; // ARMv7a is "special", it uses different directive prefix
														   // than the others and the "shared" code won't build for it
						break;

					case "arm64-v8a":
						asmTargetProvider = new ARMNativeAssemblerTargetProvider (is64Bit: true);
						break;

					case "x86":
						asmTargetProvider = new X86NativeAssemblerTargetProvider (is64Bit: false);
						break;

					case "x86_64":
						asmTargetProvider = new X86NativeAssemblerTargetProvider (is64Bit: true);
						break;

					default:
						throw new InvalidOperationException ($"Unknown ABI {abi}");
				}

				NativeAssemblyGenerator generator = getGenerator (asmTargetProvider, sharedBitsWritten, sharedIncludeUsesAbiPrefix);

				using (var sw = MemoryStreamPool.Shared.CreateStreamWriter (outputEncoding)) {
					generator.Write (sw);
					sw.Flush ();
					MonoAndroidHelper.CopyIfStreamChanged (sw.BaseStream, generator.MainSourceFile);
					if (!sharedIncludeUsesAbiPrefix)
						sharedBitsWritten = true;
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
				MonoAndroidHelper.CopyIfStreamChanged (bw.BaseStream, moduleData.OutputFilePath);
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
				bw.Write (entry.SkipInJavaToManaged ? InvalidJavaToManagedMappingIndex : (uint)entry.ManagedIndex);
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
