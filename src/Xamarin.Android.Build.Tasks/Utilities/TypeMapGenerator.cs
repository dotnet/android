using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Mono.Cecil;

namespace Xamarin.Android.Tasks
{
	class TypeMapGenerator
	{
		const string TypeMapMagicString = "XATM"; // Xamarin Android TypeMap
		const string TypeMapIndexMagicString = "XATI"; // Xamarin Android Typemap Index
		const uint TypeMapFormatVersion = 1; // Keep in sync with the value in src/monodroid/jni/xamarin-app.hh

		internal sealed class ModuleUUIDArrayComparer : IComparer<ModuleData>
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

			public int Compare (ModuleData left, ModuleData right)
			{
				return Compare (left.MvidBytes, right.MvidBytes);
			}
		}

		internal sealed class TypeMapEntryArrayComparer : IComparer<TypeMapEntry>
		{
			public int Compare (TypeMapEntry left, TypeMapEntry right)
			{
				return String.CompareOrdinal (left.JavaName, right.JavaName);
			}
		}

		internal sealed class TypeMapEntry
		{
			public string JavaName;
			public int JavaNameLength;
			public string ManagedTypeName;
			public uint Token;
			public int AssemblyNameIndex = -1;
			public int ModuleIndex = -1;
		}

		internal sealed class ModuleData
		{
			public Guid Mvid;
			public byte[] MvidBytes;
			public AssemblyDefinition Assembly;
			public TypeMapEntry[] Types;
			public Dictionary<uint, TypeMapEntry> DuplicateTypes;
			public string AssemblyName;
			public string AssemblyNameLabel;
			public string OutputFilePath;

			public Dictionary<string, TypeMapEntry> TypesScratch;
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

		public bool Generate (bool skipJniAddNativeMethodRegistrationAttributeScan, List<TypeDefinition> javaTypes, string outputDirectory, bool generateNativeAssembly, out ApplicationConfigTaskState appConfState)
		{
			if (String.IsNullOrEmpty (outputDirectory))
				throw new ArgumentException ("must not be null or empty", nameof (outputDirectory));

			if (!Directory.Exists (outputDirectory))
				Directory.CreateDirectory (outputDirectory);

			int assemblyId = 0;
			int maxJavaNameLength = 0;
			int maxModuleFileNameLength = 0;
			var knownAssemblies = new Dictionary<string, int> (StringComparer.Ordinal);
			var tempModules = new Dictionary<byte[], ModuleData> ();
			Dictionary <AssemblyDefinition, int> moduleCounter = null;
			var mvidCache = new Dictionary <Guid, byte[]> ();
			appConfState = new ApplicationConfigTaskState {
				JniAddNativeMethodRegistrationAttributePresent = skipJniAddNativeMethodRegistrationAttributeScan
			};

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

				ModuleData moduleData;
				if (!tempModules.TryGetValue (moduleUUID, out moduleData)) {
					if (moduleCounter == null)
						moduleCounter = new Dictionary <AssemblyDefinition, int> ();

					moduleData = new ModuleData {
						Mvid = td.Module.Mvid,
						MvidBytes = moduleUUID,
						Assembly = td.Module.Assembly,
						AssemblyName = td.Module.Assembly.Name.Name,
						TypesScratch = new Dictionary<string, TypeMapEntry> (StringComparer.Ordinal),
						DuplicateTypes = new Dictionary<uint, TypeMapEntry> (),
					};
					tempModules.Add (moduleUUID, moduleData);

					if (!generateNativeAssembly) {
						int moduleNum;
						if (!moduleCounter.TryGetValue (moduleData.Assembly, out moduleNum)) {
							moduleNum = 0;
							moduleCounter [moduleData.Assembly] = 0;
						} else {
							moduleNum++;
							moduleCounter [moduleData.Assembly] = moduleNum;
						}

						string fileName = $"{moduleData.Assembly.Name.Name}.{moduleNum}.typemap";
						moduleData.OutputFilePath = Path.Combine (outputDirectory, fileName);
						if (maxModuleFileNameLength < fileName.Length)
							maxModuleFileNameLength = fileName.Length;
					}
				}

				string javaName = Java.Interop.Tools.TypeNameMappings.JavaNativeTypeManager.ToJniName (td);
				var entry = new TypeMapEntry {
					JavaName = javaName,
					JavaNameLength = outputEncoding.GetByteCount (javaName),
					ManagedTypeName = td.FullName,
					Token = td.MetadataToken.ToUInt32 (),
					AssemblyNameIndex = knownAssemblies [assemblyName]
				};

				if (generateNativeAssembly) {
					if (entry.JavaNameLength > maxJavaNameLength)
						maxJavaNameLength = entry.JavaNameLength;
				}

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
			foreach (ModuleData module in modules) {
				if (module.TypesScratch.Count == 0) {
					module.Types = new TypeMapEntry[0];
					continue;
				}

				module.Types = module.TypesScratch.Values.ToArray ();
				Array.Sort (module.Types, typeMapEntryComparer);
			}

			NativeTypeMappingData data;
			if (!generateNativeAssembly) {
				string typeMapIndexPath = Path.Combine (outputDirectory, "typemap.index");
				using (var indexWriter = MemoryStreamPool.Shared.CreateBinaryWriter ()) {
					OutputModules (modules, indexWriter, maxModuleFileNameLength + 1);
					indexWriter.Flush ();
					MonoAndroidHelper.CopyIfStreamChanged (indexWriter.BaseStream, typeMapIndexPath);
				}
				GeneratedBinaryTypeMaps.Add (typeMapIndexPath);

				data = new NativeTypeMappingData (logger, new ModuleData[0], 0);
			} else {
				data = new NativeTypeMappingData (logger, modules, maxJavaNameLength + 1);
			}

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

				var generator = new TypeMappingNativeAssemblyGenerator (asmTargetProvider, data, Path.Combine (outputDirectory, "typemaps"), sharedBitsWritten, sharedIncludeUsesAbiPrefix);

				using (var sw = MemoryStreamPool.Shared.CreateStreamWriter (outputEncoding)) {
					generator.Write (sw);
					sw.Flush ();
					MonoAndroidHelper.CopyIfStreamChanged (sw.BaseStream, generator.MainSourceFile);
					if (!sharedIncludeUsesAbiPrefix)
						sharedBitsWritten = true;
				}
			}
			return true;
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
		//  [Module UUID][File name]<NUL>
		//
		// Where:
		//
		//   [Module UUID] is 16 bytes long
		//   [File name] is right-padded with <NUL> characters to the [Module file name width] boundary.
		//
		void OutputModules (ModuleData[] modules, BinaryWriter indexWriter, int moduleFileNameWidth)
		{
			indexWriter.Write (typemapIndexMagicString);
			indexWriter.Write (TypeMapFormatVersion);
			indexWriter.Write (modules.Length);
			indexWriter.Write (moduleFileNameWidth);

			foreach (ModuleData data in modules) {
				OutputModule (data.MvidBytes, data);
				indexWriter.Write (data.MvidBytes);

				string outputFilePath = Path.GetFileName (data.OutputFilePath);
				indexWriter.Write (outputEncoding.GetBytes (outputFilePath));
				PadField (indexWriter, outputFilePath.Length, moduleFileNameWidth);
			}
		}

		void OutputModule (byte[] moduleUUID, ModuleData moduleData)
		{
			if (moduleData.Types.Length == 0)
				return;

			using (var bw = MemoryStreamPool.Shared.CreateBinaryWriter ()) {
				OutputModule (bw, moduleUUID, moduleData);
				bw.Flush ();
				MonoAndroidHelper.CopyIfStreamChanged (bw.BaseStream, moduleData.OutputFilePath);
			}
			GeneratedBinaryTypeMaps.Add (moduleData.OutputFilePath);
		}

		// Binary file format, all data is little-endian:
		//
		//  [Magic string]                    # XATM
		//  [Format version]                  # 32-bit integer, 4 bytes
		//  [Module UUID]                     # 16 bytes
		//  [Entry count]                     # unsigned 32-bit integer, 4 bytes
		//  [Duplicate count]                 # unsigned 32-bit integer, 4 bytes (might be 0)
		//  [Java type name width]            # unsigned 32-bit integer, 4 bytes
		//  [Assembly name size]              # unsigned 32-bit integer, 4 bytes
		//  [Assembly name]                   # Non-null terminated assembly name
		//  [Java-to-managed map]             # Format described below, [Entry count] entries
		//  [Managed-to-java map]             # Format described below, [Entry count] entries
		//  [Managed-to-java duplicates map]  # Map of unique managed IDs which point to the same Java type name (might be empty)
		//
		// Java-to-managed map format:
		//
		//    [Java type name]<NUL>[Managed type token ID]
		//
		//  Each name is padded with <NUL> to the width specified in the [Java type name width] field above.
		//  Names are written without the size prefix, instead they are always terminated with a nul character
		//  to make it easier and faster to handle by the native runtime.
		//
		//  Each token ID is an unsigned 32-bit integer, 4 bytes
		//
		//
		// Managed-to-java map format:
		//
		//    [Managed type token ID][Java type name table index]
		//
		//  Both fields are unsigned 32-bit integers, to a total of 8 bytes per entry. Index points into the
		//  [Java-to-managed map] table above.
		//
		// Managed-to-java duplicates map format:
		//
		//  Format is identical to [Managed-to-java] above.
		//
		void OutputModule (BinaryWriter bw, byte[] moduleUUID, ModuleData moduleData)
		{
			bw.Write (moduleMagicString);
			bw.Write (TypeMapFormatVersion);
			bw.Write (moduleUUID);

			var javaNames = new Dictionary<string, uint> (StringComparer.Ordinal);
			var managedTypes = new Dictionary<uint, uint> ();
			int maxJavaNameLength = 0;

			foreach (TypeMapEntry entry in moduleData.Types) {
				javaNames.Add (entry.JavaName, entry.Token);
				if (entry.JavaNameLength > maxJavaNameLength)
					maxJavaNameLength = entry.JavaNameLength;

				managedTypes.Add (entry.Token, 0);
			}

			var javaNameList = javaNames.Keys.ToList ();
			foreach (TypeMapEntry entry in moduleData.Types) {
				var javaIndex = (uint)javaNameList.IndexOf (entry.JavaName);
				managedTypes[entry.Token] = javaIndex;
			}

			bw.Write (javaNames.Count);
			bw.Write (moduleData.DuplicateTypes.Count);
			bw.Write (maxJavaNameLength + 1);

			string assemblyName = moduleData.Assembly.Name.Name;
			bw.Write (assemblyName.Length);
			bw.Write (outputEncoding.GetBytes (assemblyName));

			var sortedJavaNames = javaNames.Keys.ToArray ();
			Array.Sort (sortedJavaNames, StringComparer.Ordinal);
			foreach (string typeName in sortedJavaNames) {
				byte[] bytes = outputEncoding.GetBytes (typeName);
				bw.Write (bytes);
				PadField (bw, bytes.Length, maxJavaNameLength + 1);
				bw.Write (javaNames[typeName]);
			}

			WriteManagedTypes (managedTypes);
			if (moduleData.DuplicateTypes.Count == 0)
				return;

			var managedDuplicates = new Dictionary<uint, uint> ();
			foreach (var kvp in moduleData.DuplicateTypes) {
				uint javaIndex = kvp.Key;
				uint typeId = kvp.Value.Token;

				managedDuplicates.Add (javaIndex, typeId);
			}

			WriteManagedTypes (managedDuplicates);

			void WriteManagedTypes (IDictionary<uint, uint> types)
			{
				var sortedTokens = types.Keys.ToArray ();
				Array.Sort (sortedTokens);

				foreach (uint token in sortedTokens) {
					bw.Write (token);
					bw.Write (types[token]);
				}
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
