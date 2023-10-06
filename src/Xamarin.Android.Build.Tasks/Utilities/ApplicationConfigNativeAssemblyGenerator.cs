using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks.LLVMIR;

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

	class ApplicationConfigNativeAssemblyGenerator : LlvmIrComposer
	{
		sealed class DSOCacheEntryContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var dso_entry = EnsureType<DSOCacheEntry> (data);
				if (String.Compare ("hash", fieldName, StringComparison.Ordinal) == 0) {
					return $" hash, from name: {dso_entry.HashedName}";
				}

				if (String.Compare ("name", fieldName, StringComparison.Ordinal) == 0) {
					return $" name: {dso_entry.name}";
				}

				return String.Empty;
			}
		}

		// Order of fields and their type must correspond *exactly* (with exception of the
		// ignored managed members) to that in
		// src/monodroid/jni/xamarin-app.hh DSOCacheEntry structure
		[NativeAssemblerStructContextDataProvider (typeof (DSOCacheEntryContextDataProvider))]
		sealed class DSOCacheEntry
		{
			[NativeAssembler (Ignore = true)]
			public string HashedName;

			[NativeAssembler (UsesDataProvider = true)]
			public ulong hash;
			public bool ignore;

			[NativeAssembler (UsesDataProvider = true)]
			public string name;
			public IntPtr handle = IntPtr.Zero;
		}

		sealed class XamarinAndroidBundledAssemblyContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override ulong GetBufferSize (object data, string fieldName)
			{
				if (String.Compare ("name", fieldName, StringComparison.Ordinal) != 0) {
					return 0;
				}

				var xaba = EnsureType<XamarinAndroidBundledAssembly> (data);
				return xaba.name_length;
			}
		}

		// Order of fields and their type must correspond *exactly* to that in
		// src/monodroid/jni/xamarin-app.hh XamarinAndroidBundledAssembly structure
		[NativeAssemblerStructContextDataProvider (typeof (XamarinAndroidBundledAssemblyContextDataProvider))]
		sealed class XamarinAndroidBundledAssembly
		{
			public int  apk_fd;
			public uint data_offset;
			public uint data_size;

			[NativePointer]
			public byte data;
			public uint name_length;

			[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToPreAllocatedBuffer = true)]
			public string name;
		}

		// Keep in sync with FORMAT_TAG in src/monodroid/jni/xamarin-app.hh
		const ulong FORMAT_TAG = 0x015E6972616D58;

		SortedDictionary <string, string>? environmentVariables;
		SortedDictionary <string, string>? systemProperties;
		TaskLoggingHelper log;
		StructureInstance? application_config;
		List<StructureInstance<DSOCacheEntry>>? dsoCache;
		List<StructureInstance<XamarinAndroidBundledAssembly>>? xamarinAndroidBundledAssemblies;

		StructureInfo? applicationConfigStructureInfo;
		StructureInfo? dsoCacheEntryStructureInfo;
		StructureInfo? xamarinAndroidBundledAssemblyStructureInfo;
		StructureInfo? assemblyStoreSingleAssemblyRuntimeDataStructureinfo;
		StructureInfo? assemblyStoreRuntimeDataStructureInfo;

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
		public bool HaveStandaloneAssemblyDSOs { get; set; }
		public int NumberOfAssembliesInApk { get; set; }
		public int BundledAssemblyNameWidth { get; set; } // including the trailing NUL
		public int AndroidRuntimeJNIEnvToken { get; set; }
		public int JNIEnvInitializeToken { get; set; }
		public int JNIEnvRegisterJniNativesToken { get; set; }
		public int JniRemappingReplacementTypeCount { get; set; }
		public int JniRemappingReplacementMethodIndexEntryCount { get; set; }
		public MonoComponent MonoComponents { get; set; }
		public PackageNamingPolicy PackageNamingPolicy { get; set; }
		public List<ITaskItem> NativeLibraries { get; set; }
		public bool MarshalMethodsEnabled { get; set; }

		public ApplicationConfigNativeAssemblyGenerator (IDictionary<string, string> environmentVariables, IDictionary<string, string> systemProperties, TaskLoggingHelper log)
		{
			if (environmentVariables != null) {
				this.environmentVariables = new SortedDictionary<string, string> (environmentVariables, StringComparer.Ordinal);
			}

			if (systemProperties != null) {
				this.systemProperties = new SortedDictionary<string, string> (systemProperties, StringComparer.Ordinal);
			}

			this.log = log;
		}

		protected override void Construct (LlvmIrModule module)
		{
			MapStructures (module);

			module.AddGlobalVariable ("format_tag", FORMAT_TAG, comment: $" 0x{FORMAT_TAG:x}");
			module.AddGlobalVariable ("mono_aot_mode_name", MonoAOTMode);

			var envVars = new LlvmIrGlobalVariable (environmentVariables, "app_environment_variables") {
				Comment = " Application environment variables array, name:value",
			};
			module.Add (envVars, stringGroupName: "env", stringGroupComment: " Application environment variables name:value pairs");

			var sysProps = new LlvmIrGlobalVariable (systemProperties, "app_system_properties") {
				Comment = " System properties defined by the application",
			};
			module.Add (sysProps, stringGroupName: "sysprop", stringGroupComment: " System properties name:value pairs");

			dsoCache = InitDSOCache ();
			var app_cfg = new ApplicationConfig {
				uses_mono_llvm = UsesMonoLLVM,
				uses_mono_aot = UsesMonoAOT,
				aot_lazy_load = AotEnableLazyLoad,
				uses_assembly_preload = UsesAssemblyPreload,
				broken_exception_transitions = BrokenExceptionTransitions,
				instant_run_enabled = InstantRunEnabled,
				jni_add_native_method_registration_attribute_present = JniAddNativeMethodRegistrationAttributePresent,
				have_runtime_config_blob = HaveRuntimeConfigBlob,
				have_standalone_assembly_dsos = HaveStandaloneAssemblyDSOs,
				marshal_methods_enabled = MarshalMethodsEnabled,
				bound_stream_io_exception_type = (byte)BoundExceptionType,
				package_naming_policy = (uint)PackageNamingPolicy,
				environment_variable_count = (uint)(environmentVariables == null ? 0 : environmentVariables.Count * 2),
				system_property_count = (uint)(systemProperties == null ? 0 : systemProperties.Count * 2),
				number_of_assemblies_in_apk = (uint)NumberOfAssembliesInApk,
				bundled_assembly_name_width = (uint)BundledAssemblyNameWidth,
				number_of_dso_cache_entries = (uint)dsoCache.Count,
				android_runtime_jnienv_class_token = (uint)AndroidRuntimeJNIEnvToken,
				jnienv_initialize_method_token = (uint)JNIEnvInitializeToken,
				jnienv_registerjninatives_method_token = (uint)JNIEnvRegisterJniNativesToken,
				jni_remapping_replacement_type_count = (uint)JniRemappingReplacementTypeCount,
				jni_remapping_replacement_method_index_entry_count = (uint)JniRemappingReplacementMethodIndexEntryCount,
				mono_components_mask = (uint)MonoComponents,
				android_package_name = AndroidPackageName,
			};
			application_config = new StructureInstance<ApplicationConfig> (applicationConfigStructureInfo, app_cfg);
			module.AddGlobalVariable ("application_config", application_config);

			var dso_cache = new LlvmIrGlobalVariable (dsoCache, "dso_cache", LlvmIrVariableOptions.GlobalWritable) {
				Comment = " DSO cache entries",
				BeforeWriteCallback = HashAndSortDSOCache,
				NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal,
			};
			module.Add (dso_cache);

			// TODO: don't generate if we're embedding assemblies into DSOs
			xamarinAndroidBundledAssemblies = new List<StructureInstance<XamarinAndroidBundledAssembly>> (NumberOfAssembliesInApk);
			var emptyBundledAssemblyData = new XamarinAndroidBundledAssembly {
				apk_fd = -1,
				data_offset = 0,
				data_size = 0,
				data = 0,
				name_length = (uint)BundledAssemblyNameWidth,
				name = null,
			};

			for (int i = 0; i < NumberOfAssembliesInApk; i++) {
				xamarinAndroidBundledAssemblies.Add (new StructureInstance<XamarinAndroidBundledAssembly> (xamarinAndroidBundledAssemblyStructureInfo, emptyBundledAssemblyData));
			}

			string bundledBuffersSize = xamarinAndroidBundledAssemblies == null ? "empty (unused when assembly stores are enabled)" : $"{BundledAssemblyNameWidth} bytes long";
			var bundled_assemblies = new LlvmIrGlobalVariable (typeof(List<StructureInstance<XamarinAndroidBundledAssembly>>), "bundled_assemblies", LlvmIrVariableOptions.GlobalWritable) {
				Value = xamarinAndroidBundledAssemblies,
				Comment = $" Bundled assembly name buffers, all {bundledBuffersSize}",
			};
			module.Add (bundled_assemblies);
		}

		void HashAndSortDSOCache (LlvmIrVariable variable, LlvmIrModuleTarget target, object? state)
		{
			var cache = variable.Value as List<StructureInstance<DSOCacheEntry>>;
			if (cache == null) {
				throw new InvalidOperationException ($"Internal error: DSO cache must no be empty");
			}

			bool is64Bit = target.Is64Bit;
			foreach (StructureInstance instance in cache) {
				if (instance.Obj == null) {
					throw new InvalidOperationException ("Internal error: DSO cache must not contain null entries");
				}

				var entry = instance.Obj as DSOCacheEntry;
				if (entry == null) {
					throw new InvalidOperationException ($"Internal error: DSO cache entry has unexpected type {instance.Obj.GetType ()}");
				}

				entry.hash = GetXxHash (entry.HashedName, is64Bit);
			}

			cache.Sort ((StructureInstance<DSOCacheEntry> a, StructureInstance<DSOCacheEntry> b) => a.Instance.hash.CompareTo (b.Instance.hash));
		}

		List<StructureInstance<DSOCacheEntry>> InitDSOCache ()
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

				dsos.Add ((name, $"dsoName{dsos.Count.ToString (CultureInfo.InvariantCulture)}", ELFHelper.IsEmptyAOTLibrary (log, item.ItemSpec)));
			}

			var dsoCache = new List<StructureInstance<DSOCacheEntry>> ();
			var nameMutations = new List<string> ();

			for (int i = 0; i < dsos.Count; i++) {
				string name = dsos[i].name;
				nameMutations.Clear();
				AddNameMutations (name);
				// All mutations point to the actual library name, but have hash of the mutated one
				foreach (string entryName in nameMutations) {
					var entry = new DSOCacheEntry {
						HashedName = entryName,
						hash = 0, // Hash is arch-specific, we compute it before writing
						ignore = dsos[i].ignore,
						name = name,
					};

					dsoCache.Add (new StructureInstance<DSOCacheEntry> (dsoCacheEntryStructureInfo, entry));
				}
			}

			return dsoCache;

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

		void MapStructures (LlvmIrModule module)
		{
			applicationConfigStructureInfo = module.MapStructure<ApplicationConfig> ();
			xamarinAndroidBundledAssemblyStructureInfo = module.MapStructure<XamarinAndroidBundledAssembly> ();
			dsoCacheEntryStructureInfo = module.MapStructure<DSOCacheEntry> ();
		}
	}
}
