using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Java.Interop.Tools.TypeNameMappings;
using K4os.Hash.xxHash;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	// Must match the MonoComponent enum in src/monodroid/jni/xamarin-app.hh
	[Flags]
	enum MonoComponent
	{
		None      = 0x00,
		Debugger  = 0x01,
		HotReload = 0x02,
		Tracing   = 0x04,
	}

	class ApplicationConfigNativeAssemblyGenerator : NativeAssemblyComposer
	{
		sealed class DSOCacheEntryContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var dso_entry = data as DSOCacheEntry;
				if (dso_entry == null) {
					throw new InvalidOperationException ("Invalid data type, expected an instance of DSOCacheEntry");
				}

				if (String.Compare ("hash", fieldName, StringComparison.Ordinal) == 0) {
					return $"hash, from name: {dso_entry.HashedName}";
				}

				if (String.Compare ("name", fieldName, StringComparison.Ordinal) == 0) {
					return $"name: {dso_entry.name}";
				}

				return String.Empty;
			}
		}

		[NativeAssemblerStructContextDataProvider (typeof (DSOCacheEntryContextDataProvider))]
		sealed class DSOCacheEntry
		{
			[NativeAssembler (Ignore = true)]
			public string HashedName;

			[NativeAssembler (UsesDataProvider = true)]
			public ulong hash;
			public bool ignore;

			[NativeAssemblerString (UsesDataProvider = true)]
			public string name;
			public IntPtr handle = IntPtr.Zero;
		}

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh AssemblyStoreSingleAssemblyRuntimeData structure
		sealed class AssemblyStoreSingleAssemblyRuntimeData
		{
			public IntPtr  image_data;
			public IntPtr  debug_info_data;
			public IntPtr  config_data;
			public IntPtr  descriptor;
		};

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh AssemblyStoreRuntimeData structure
		sealed class AssemblyStoreRuntimeData
		{
			public IntPtr data_start;
			public uint   assembly_count;
			public IntPtr assemblies;
		};

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh XamarinAndroidBundledAssembly structure
		sealed class XamarinAndroidBundledAssembly
		{
			public int    apk_fd;
			public uint   data_offset;
			public uint   data_size;
			public IntPtr data;
			public uint   name_length;

			[NativeAssemblerString (AssemblerStringFormat.PointerToSymbol)]
			public string name;
		};

		SortedDictionary <string, string> environmentVariables;
		SortedDictionary <string, string> systemProperties;
		uint stringCounter = 0;
		uint bufferCounter = 0;

		public bool IsBundledApp { get; set; }
		public bool UsesMonoAOT { get; set; }
		public bool UsesMonoLLVM { get; set; }
		public bool UsesAssemblyPreload { get; set; }
		public string MonoAOTMode { get; set; }
		public bool AotEnableLazyLoad { get; set; }
		public string AndroidPackageName { get; set; }
		public bool BrokenExceptionTransitions { get; set; }
		public global::Android.Runtime.BoundExceptionType BoundExceptionType { get; set; }
		public bool InstantRunEnabled { get; set; }
		public bool JniAddNativeMethodRegistrationAttributePresent { get; set; }
		public bool HaveRuntimeConfigBlob { get; set; }
		public bool HaveAssemblyStore { get; set; }
		public int NumberOfAssembliesInApk { get; set; }
		public int NumberOfAssemblyStoresInApks { get; set; }
		public int BundledAssemblyNameWidth { get; set; } // including the trailing NUL
		public MonoComponent MonoComponents { get; set; }
		public List<ITaskItem> NativeLibraries { get; set; }

		public PackageNamingPolicy PackageNamingPolicy { get; set; }

		TaskLoggingHelper log;

		public ApplicationConfigNativeAssemblyGenerator (AndroidTargetArch arch, IDictionary<string, string> environmentVariables, IDictionary<string, string> systemProperties, TaskLoggingHelper log)
			: base (arch)
		{
			if (environmentVariables != null)
				this.environmentVariables = new SortedDictionary<string, string> (environmentVariables, StringComparer.Ordinal);
			if (systemProperties != null)
			this.systemProperties = new SortedDictionary<string, string> (systemProperties, StringComparer.Ordinal);
			this.log = log;
		}

		protected override void Write (NativeAssemblyGenerator generator)
		{
			if (String.IsNullOrEmpty (AndroidPackageName))
				throw new InvalidOperationException ("Android package name must be set");

			if (UsesMonoAOT && String.IsNullOrEmpty (MonoAOTMode))
				throw new InvalidOperationException ("Mono AOT enabled but no AOT mode specified");

			generator.WriteStringPointerSymbol ("mono_aot_mode_name", MonoAOTMode ?? String.Empty, global: true);

			WriteNameValueStringArray (generator, "app_environment_variables", environmentVariables);
			WriteNameValueStringArray (generator, "app_system_properties", systemProperties);

			WriteBundledAssemblies (generator);
			WriteAssemblyStoreAssemblies (generator);

			uint dsoCacheEntries = WriteDSOCache (generator);
			var application_config = new ApplicationConfig {
				uses_mono_llvm = UsesMonoLLVM,
				uses_mono_aot = UsesMonoAOT,
				aot_lazy_load = AotEnableLazyLoad,
				uses_assembly_preload = UsesAssemblyPreload,
				is_a_bundled_app = IsBundledApp,
				broken_exception_transitions = BrokenExceptionTransitions,
				instant_run_enabled = InstantRunEnabled,
				jni_add_native_method_registration_attribute_present = JniAddNativeMethodRegistrationAttributePresent,
				have_runtime_config_blob = HaveRuntimeConfigBlob,
				have_assemblies_blob = HaveAssemblyStore,
				bound_stream_io_exception_type = (byte)BoundExceptionType,
				package_naming_policy = (uint)PackageNamingPolicy,
				environment_variable_count = (uint)(environmentVariables == null ? 0 : environmentVariables.Count * 2),
				system_property_count = (uint)(systemProperties == null ? 0 : systemProperties.Count * 2),
				number_of_assemblies_in_apk = (uint)NumberOfAssembliesInApk,
				bundled_assembly_name_width = (uint)BundledAssemblyNameWidth,
				number_of_assembly_store_files = (uint)NumberOfAssemblyStoresInApks,
				number_of_dso_cache_entries = dsoCacheEntries,
				mono_components_mask = (uint)MonoComponents,
				android_package_name = AndroidPackageName,
			};

			NativeAssemblyGenerator.StructureWriteContext structStatus = generator.StartStructure ();
			generator.WriteStructure (structStatus, application_config);
			generator.WriteSymbol (structStatus, "application_config", local: false);
		}

		uint WriteDSOCache (NativeAssemblyGenerator generator)
		{
			var dsos = new List<(string name, string nameLabel, bool ignore)> ();
			var nameCache = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

			foreach (ITaskItem item in NativeLibraries) {
				string? name = item.GetMetadata ("ArchiveFileName");
				if (String.IsNullOrEmpty (name)) {
					name = item.ItemSpec;
				}
				name = Path.GetFileName (name);

				if (nameCache.Contains (name)) {
					continue;
				}

				dsos.Add ((name, $"dsoName{dsos.Count}", ELFHelper.IsEmptyAOTLibrary (log, item.ItemSpec)));
			}

			var dsoCache = new List<DSOCacheEntry> ();
			var nameMutations = new List<string> ();

			for (int i = 0; i < dsos.Count; i++) {
				string name = dsos[i].name;
				nameMutations.Clear();
				AddNameMutations (name);
				// All mutations point to the actual library name, but have hash of the mutated one
				foreach (string entryName in nameMutations) {
					dsoCache.Add (
						new DSOCacheEntry {
							HashedName = entryName,
							hash = HashName (entryName),
							ignore = dsos[i].ignore,
							name = name,
						}
					);
				}
			}

			dsoCache.Sort ((DSOCacheEntry a, DSOCacheEntry b) => a.hash.CompareTo (b.hash));

			NativeAssemblyGenerator.StructureWriteContext dsoCacheArray = generator.StartStructureArray ();
			foreach (DSOCacheEntry entry in dsoCache) {
				NativeAssemblyGenerator.StructureWriteContext dsoStruct = generator.AddStructureArrayElement (dsoCacheArray);
				generator.WriteStructure (dsoStruct, entry);
			}
			generator.WriteSymbol (dsoCacheArray, "dso_cache", local: false);

			return (uint)dsoCache.Count;

			ulong HashName (string name)
			{
				byte[] nameBytes = Encoding.UTF8.GetBytes (name);
				if (generator.Is64Bit) {
					return XXH64.DigestOf (nameBytes, 0, nameBytes.Length);
				}

				return (ulong)XXH32.DigestOf (nameBytes, 0, nameBytes.Length);
			}

			void AddNameMutations (string name)
			{
				nameMutations.Add (name);
				if (name.EndsWith (".dll.so", StringComparison.OrdinalIgnoreCase)) {
					nameMutations.Add (Path.GetFileNameWithoutExtension (Path.GetFileNameWithoutExtension (name))!);
				} else {
					nameMutations.Add (Path.GetFileNameWithoutExtension (name)!);
				}

				const string aotPrefix = "libaot-";
				if (name.StartsWith (aotPrefix, StringComparison.OrdinalIgnoreCase)) {
					AddNameMutations (name.Substring (aotPrefix.Length));
				}

				const string libPrefix = "lib";
				if (name.StartsWith (libPrefix, StringComparison.OrdinalIgnoreCase)) {
					AddNameMutations (name.Substring (libPrefix.Length));
				}
			}
		}

		void WriteAssemblyStoreAssemblies (NativeAssemblyGenerator generator)
		{
			string label = "assembly_store_bundled_assemblies";
			generator.WriteCommentLine ("Assembly store individual assembly data");

			if (HaveAssemblyStore) {
				var emptyAssemblyData = new AssemblyStoreSingleAssemblyRuntimeData {
					image_data = IntPtr.Zero,
					debug_info_data = IntPtr.Zero,
					config_data = IntPtr.Zero,
					descriptor = IntPtr.Zero
				};

				NativeAssemblyGenerator.StructureWriteContext assemblyArray = generator.StartStructureArray ();
				for (int i = 0; i < NumberOfAssembliesInApk; i++) {
					NativeAssemblyGenerator.StructureWriteContext assemblyStruct = generator.AddStructureArrayElement (assemblyArray);
					generator.WriteStructure (assemblyStruct, emptyAssemblyData);
				}
				generator.WriteSymbol (assemblyArray, label, local: false);
			} else {
				generator.WriteEmptySymbol (SymbolType.Object, label, local: false);
			}

			label = "assembly_stores";
			generator.WriteCommentLine ("Assembly store data");

			if (HaveAssemblyStore) {
				var emptyStoreData = new AssemblyStoreRuntimeData {
					data_start = IntPtr.Zero,
					assembly_count = 0,
					assemblies = IntPtr.Zero,
				};

				NativeAssemblyGenerator.StructureWriteContext assemblyStoreArray = generator.StartStructureArray ();
				for (int i = 0; i < NumberOfAssemblyStoresInApks; i++) {
					NativeAssemblyGenerator.StructureWriteContext assemblyStoreStruct = generator.AddStructureArrayElement (assemblyStoreArray);
					generator.WriteStructure (assemblyStoreStruct, emptyStoreData);
				}
				generator.WriteSymbol (assemblyStoreArray, label, local: false);
			} else {
				generator.WriteEmptySymbol (SymbolType.Object, label, local: false);
			}
		}

		void WriteBundledAssemblies (NativeAssemblyGenerator generator)
		{
			generator.WriteCommentLine ($"Bundled assembly name buffers, all {BundledAssemblyNameWidth} bytes long");
			generator.WriteSection (".bss.bundled_assembly_names", SectionFlags.Writable | SectionFlags.Allocatable, SectionType.NoData);

			List<NativeAssemblyGenerator.LabeledSymbol>? name_labels = null;
			if (!HaveAssemblyStore) {
				name_labels = new List<NativeAssemblyGenerator.LabeledSymbol> ();
				for (int i = 0; i < NumberOfAssembliesInApk; i++) {
					name_labels.Add (generator.WriteEmptyBuffer ((uint)BundledAssemblyNameWidth, "env.buf"));
				}
			}

			string label = "bundled_assemblies";
			generator.WriteCommentLine ("Bundled assemblies data");

			if (!HaveAssemblyStore) {
				var emptyBundledAssemblyData = new XamarinAndroidBundledAssembly {
					apk_fd = -1,
					data_offset = 0,
					data_size = 0,
					data = IntPtr.Zero,
					name_length = 0,
					name = String.Empty,
				};

				NativeAssemblyGenerator.StructureWriteContext bundledAssemblyArray = generator.StartStructureArray ();
				for (int i = 0; i < NumberOfAssembliesInApk; i++) {
					emptyBundledAssemblyData.name = name_labels [i]!.Label;
					NativeAssemblyGenerator.StructureWriteContext bundledAssemblyStruct = generator.AddStructureArrayElement (bundledAssemblyArray);
					generator.WriteStructure (bundledAssemblyStruct, emptyBundledAssemblyData);
				}
				generator.WriteSymbol (bundledAssemblyArray, label, local: false);
			} else {
				generator.WriteEmptySymbol (SymbolType.Object, label, local: false);
			}
		}

		void WriteNameValueStringArray (NativeAssemblyGenerator generator, string label, SortedDictionary<string, string> entries)
		{
			if (entries == null || entries.Count == 0) {
				generator.WriteDataSection ();
				generator.WriteEmptySymbol (SymbolType.Object, label, local: false);
				return;
			}

			NativeAssemblyGenerator.StructureWriteContext nameValueStruct = generator.StartStructure ();
			foreach (var kvp in entries) {
				string name = kvp.Key;
				string value = kvp.Value ?? String.Empty;

				generator.WriteStringPointer (nameValueStruct, name);
				generator.WriteStringPointer (nameValueStruct, value);
			}
			generator.WriteSymbol (nameValueStruct, label, local: false);
		}
	};
}
