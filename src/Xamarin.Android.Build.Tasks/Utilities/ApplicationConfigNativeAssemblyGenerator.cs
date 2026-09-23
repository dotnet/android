#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class ApplicationConfigNativeAssemblyGenerator
{
	// From host_runtime_contract.h in dotnet/runtime
	const string HOST_PROPERTY_RUNTIME_CONTRACT   = "HOST_RUNTIME_CONTRACT";
	const string HOST_PROPERTY_BUNDLE_PROBE       = "BUNDLE_PROBE";
	const string HOST_PROPERTY_PINVOKE_OVERRIDE   = "PINVOKE_OVERRIDE";
	const string HOST_PROPERTY_RUNTIME_IDENTIFIER = "RUNTIME_IDENTIFIER";
	const string HOST_PROPERTY_APP_CONTEXT_BASE_DIRECTORY = "APP_CONTEXT_BASE_DIRECTORY";

	// The structure declarations written by this class must be identical to the structures in
	// src/native/clr/include/xamarin-app.hh.  Data sizes are the sums of sizes of all the non-pointer
	// members (see LlvmIrTarget.GetAggregateAlignment).  All of the structures contain pointers, and
	// their non-pointer members don't need alignment higher than NonPointerMemberAlignment
	const ulong ApplicationConfigDataSize = 52;
	const ulong AssemblyStoreRuntimeDataDataSize = 8;
	const ulong AssemblyStoreSingleAssemblyRuntimeDataDataSize = 0;
	const ulong DSOCacheEntryDataSize = 10;
	const ulong XamarinAndroidBundledAssemblyDataSize = 16;
	const ulong NonPointerMemberAlignment = 4;

	sealed class DSOCacheEntry
	{
		public string HashedName = "";
		public string RealName = "";

		public uint hash;
		public bool ignore;
		public bool is_jni_library;
		public uint name_index;
	}

	sealed class State
	{
		public readonly AppEnvironmentVariableTable EnvironmentVariables;
		public readonly AppEnvironmentVariableTable SystemProperties;
		public ApplicationConfig ApplicationConfig = new ();
		public readonly List<DSOCacheEntry> DsoCache = [];
		public readonly List<DSOCacheEntry> JniPreloadDSOs = [];
		public readonly LlvmIrStringBlob NamesBlob = new ();
		public uint NameMutationsCount = 1;
		public readonly List<string> RuntimePropertyNames = [];
		public readonly List<string?> RuntimePropertyValues = [];

		public State (AppEnvironmentVariableTable environmentVariables, AppEnvironmentVariableTable systemProperties)
		{
			EnvironmentVariables = environmentVariables;
			SystemProperties = systemProperties;
		}
	}

	// Keep in sync with FORMAT_TAG in src/monodroid/jni/xamarin-app.hh
	const ulong FORMAT_TAG = 0x00025E6972616D58; // 'Xmari^XY' where XY is the format version

	// List of library names to ignore when generating the list of JNI-using libraries to preload
	internal static readonly HashSet<string> DsoCacheJniPreloadIgnore = new (StringComparer.OrdinalIgnoreCase) {
		"libmonodroid.so",
	};

	readonly TaskLoggingHelper Log;
	readonly SortedDictionary <string, string>? environmentVariables;
	readonly SortedDictionary <string, string>? systemProperties;
	readonly SortedDictionary <string, string> runtimeProperties;
	State? state;

	/// <summary>
	/// Whether to write additional descriptive comments into the generated LLVM IR.  Defaults to <c>false</c>.
	/// Set from the <c>$(_AndroidEmitLlvmIrComments)</c> MSBuild property.
	/// </summary>
	public bool EmitComments { get; set; }

	public bool UsesAssemblyPreload { get; set; }
	public string AndroidPackageName { get; set; } = "";
	public int NumberOfAssembliesInApk { get; set; }
	public int BundledAssemblyNameWidth { get; set; } // including the trailing NUL
	public int AndroidRuntimeJNIEnvToken { get; set; }
	public int JNIEnvInitializeToken { get; set; }
	public int JniRemappingReplacementTypeCount { get; set; }
	public int JniRemappingReplacementMethodIndexEntryCount { get; set; }
	public PackageNamingPolicy PackageNamingPolicy { get; set; }
	public List<ITaskItem> NativeLibraries { get; set; } = [];
	public ICollection<ITaskItem>? NativeLibrariesNoJniPreload { get; set; }
	public ICollection<ITaskItem>? NativeLibrariesAlwaysJniPreload { get; set; }
	public bool MarshalMethodsEnabled { get; set; }
	public bool IgnoreSplitConfigs { get; set; }
	public bool HaveAssemblyStore { get; set; }

	public ApplicationConfigNativeAssemblyGenerator (IDictionary<string, string> environmentVariables, IDictionary<string, string> systemProperties,
		IDictionary<string, string>? runtimeProperties, TaskLoggingHelper log)
	{
		Log = log ?? throw new ArgumentNullException (nameof (log));

		if (environmentVariables != null) {
			this.environmentVariables = new SortedDictionary<string, string> (environmentVariables, StringComparer.Ordinal);
		}

		if (systemProperties != null) {
			this.systemProperties = new SortedDictionary<string, string> (systemProperties, StringComparer.Ordinal);
		}

		if (runtimeProperties != null) {
			this.runtimeProperties = new SortedDictionary<string, string> (runtimeProperties, StringComparer.Ordinal);
		} else {
			this.runtimeProperties = new SortedDictionary<string, string> (StringComparer.Ordinal);
		}

		// These will be filled in by the native host.
		this.runtimeProperties[HOST_PROPERTY_RUNTIME_CONTRACT] = String.Empty;
		this.runtimeProperties[HOST_PROPERTY_RUNTIME_IDENTIFIER] = String.Empty;
		this.runtimeProperties[HOST_PROPERTY_APP_CONTEXT_BASE_DIRECTORY] = String.Empty;

		// these mustn't be there, they would break our host contract
		this.runtimeProperties.Remove (HOST_PROPERTY_PINVOKE_OVERRIDE);
		this.runtimeProperties.Remove (HOST_PROPERTY_BUNDLE_PROBE);
	}

	/// <summary>
	/// Performs the architecture independent part of the work (e.g. scanning native libraries), which
	/// may fail on invalid input.  Called by <see cref="Generate"/> if not called explicitly before.
	/// </summary>
	public void Initialize () => EnsureState ();

	State EnsureState () => state ??= Init ();

	State Init ()
	{
		var envVars = new AppEnvironmentVariableTable (Log, environmentVariables);

		// We reuse the same structure as for environment variables, there's no point in adding a new, identical, one
		var sysProps = new AppEnvironmentVariableTable (Log, systemProperties);

		var ret = new State (envVars, sysProps);
		InitDSOCache (ret);

		ret.ApplicationConfig = new ApplicationConfig {
			uses_assembly_preload = UsesAssemblyPreload,
			marshal_methods_enabled = MarshalMethodsEnabled,
			ignore_split_configs = IgnoreSplitConfigs,
			number_of_runtime_properties = (uint)runtimeProperties.Count,
			package_naming_policy = (uint)PackageNamingPolicy,
			environment_variable_count = (uint)(environmentVariables == null ? 0 : environmentVariables.Count),
			system_property_count = (uint)sysProps.Count,
			number_of_assemblies_in_apk = (uint)NumberOfAssembliesInApk,
			number_of_shared_libraries = (uint)NativeLibraries.Count,
			bundled_assembly_name_width = (uint)BundledAssemblyNameWidth,
			number_of_dso_cache_entries = (uint)ret.DsoCache.Count,
			android_runtime_jnienv_class_token = (uint)AndroidRuntimeJNIEnvToken,
			jnienv_initialize_method_token = (uint)JNIEnvInitializeToken,
			jni_remapping_replacement_type_count = (uint)JniRemappingReplacementTypeCount,
			jni_remapping_replacement_method_index_entry_count = (uint)JniRemappingReplacementMethodIndexEntryCount,
			android_package_name = AndroidPackageName,
			have_assembly_store = HaveAssemblyStore,
		};

		// HOST_PROPERTY_RUNTIME_CONTRACT, HOST_PROPERTY_RUNTIME_IDENTIFIER and
		// HOST_PROPERTY_APP_CONTEXT_BASE_DIRECTORY will come first, in that order, our native runtime
		// requires that since it needs to set their values in the values array and we don't want to
		// spend time searching for the indices, nor we want to add yet another variable storing the
		// index to the entry. KISS.
		ret.RuntimePropertyNames.Add (HOST_PROPERTY_RUNTIME_CONTRACT);
		ret.RuntimePropertyNames.Add (HOST_PROPERTY_RUNTIME_IDENTIFIER);
		ret.RuntimePropertyNames.Add (HOST_PROPERTY_APP_CONTEXT_BASE_DIRECTORY);
		ret.RuntimePropertyValues.Add (null);
		ret.RuntimePropertyValues.Add (null);
		ret.RuntimePropertyValues.Add (null);

		foreach (var kvp in runtimeProperties) {
			if (MonoAndroidHelper.StringEquals (kvp.Key, HOST_PROPERTY_RUNTIME_CONTRACT) ||
					MonoAndroidHelper.StringEquals (kvp.Key, HOST_PROPERTY_RUNTIME_IDENTIFIER) ||
					MonoAndroidHelper.StringEquals (kvp.Key, HOST_PROPERTY_APP_CONTEXT_BASE_DIRECTORY)) {
				continue;
			}
			ret.RuntimePropertyNames.Add (kvp.Key);
			ret.RuntimePropertyValues.Add (kvp.Value);
		}

		return ret;
	}

	public void Generate (AndroidTargetArch arch, TextWriter output, string fileName)
	{
		State data = EnsureState ();

		using var w = new LlvmIrWriter (output, LlvmIrTarget.Get (arch), EmitComments);
		var strings = new LlvmIrStringPool ();
		ulong structAlignment = Math.Max (w.Target.PointerSize, NonPointerMemberAlignment);

		w.WriteHeader (fileName);
		AppEnvironmentVariableTable.WriteDeclaration (w);
		w.Write ($$"""

			%struct.ApplicationConfig = type {
				i1, ; bool uses_assembly_preload
				i1, ; bool marshal_methods_enabled
				i1, ; bool ignore_split_configs
				i32, ; uint32_t number_of_runtime_properties
				i32, ; uint32_t package_naming_policy
				i32, ; uint32_t environment_variable_count
				i32, ; uint32_t system_property_count
				i32, ; uint32_t number_of_assemblies_in_apk
				i32, ; uint32_t bundled_assembly_name_width
				i32, ; uint32_t number_of_dso_cache_entries
				i32, ; uint32_t number_of_shared_libraries
				i32, ; uint32_t android_runtime_jnienv_class_token
				i32, ; uint32_t jnienv_initialize_method_token
				i32, ; uint32_t jni_remapping_replacement_type_count
				i32, ; uint32_t jni_remapping_replacement_method_index_entry_count
				ptr, ; char* android_package_name
				i1 ; bool have_assembly_store
			}

			%struct.AssemblyStoreAssemblyDescriptor = type {
				i32, ; uint32_t data_offset
				i32, ; uint32_t data_size
				i32, ; uint32_t debug_data_offset
				i32, ; uint32_t debug_data_size
				i32, ; uint32_t config_data_offset
				i32 ; uint32_t config_data_size
			}

			%struct.AssemblyStoreRuntimeData = type {
				ptr, ; uint8_t data_start
				i32, ; uint32_t assembly_count
				i32, ; uint32_t index_entry_count
				ptr ; AssemblyStoreAssemblyDescriptor assemblies
			}

			%struct.AssemblyStoreSingleAssemblyRuntimeData = type {
				ptr, ; uint8_t image_data
				ptr, ; uint8_t debug_info_data
				ptr, ; uint8_t config_data
				ptr ; AssemblyStoreAssemblyDescriptor descriptor
			}

			%struct.DSOCacheEntry = type {
				i32, ; uint32_t hash
				i1, ; bool ignore
				i1, ; bool is_jni_library
				i32, ; uint32_t name_index
				ptr ; void* handle
			}

			%struct.XamarinAndroidBundledAssembly = type {
				i32, ; int32_t file_fd
				ptr, ; char* file_name
				i32, ; uint32_t data_offset
				i32, ; uint32_t data_size
				ptr, ; uint8_t data
				i32, ; uint32_t name_length
				ptr ; char* name
			}

			""");

		w.WriteGlobal ("format_tag", LlvmIrWriter.GlobalConstant, "i64", FORMAT_TAG.ToString (), 8, $" 0x{FORMAT_TAG:x}");

		data.EnvironmentVariables.Write (w, "app_environment_variables", "app_environment_variable_contents", " Application environment variables array, name:value");
		data.SystemProperties.Write (w, "app_system_properties", "app_system_property_contents", " System properties defined by the application");

		ApplicationConfig cfg = data.ApplicationConfig;
		w.WriteGlobal ("application_config", LlvmIrWriter.GlobalConstant, "%struct.ApplicationConfig", $$"""
			{
				i1 {{(cfg.uses_assembly_preload ? "true" : "false")}}, ; bool uses_assembly_preload
				i1 {{(cfg.marshal_methods_enabled ? "true" : "false")}}, ; bool marshal_methods_enabled
				i1 {{(cfg.ignore_split_configs ? "true" : "false")}}, ; bool ignore_split_configs
				i32 {{cfg.number_of_runtime_properties}}, ; uint32_t number_of_runtime_properties
				i32 {{cfg.package_naming_policy}}, ; uint32_t package_naming_policy
				i32 {{cfg.environment_variable_count}}, ; uint32_t environment_variable_count
				i32 {{cfg.system_property_count}}, ; uint32_t system_property_count
				i32 {{cfg.number_of_assemblies_in_apk}}, ; uint32_t number_of_assemblies_in_apk
				i32 {{cfg.bundled_assembly_name_width}}, ; uint32_t bundled_assembly_name_width
				i32 {{cfg.number_of_dso_cache_entries}}, ; uint32_t number_of_dso_cache_entries
				i32 {{cfg.number_of_shared_libraries}}, ; uint32_t number_of_shared_libraries
				i32 u0x{{cfg.android_runtime_jnienv_class_token:x8}}, ; uint32_t android_runtime_jnienv_class_token
				i32 u0x{{cfg.jnienv_initialize_method_token:x8}}, ; uint32_t jnienv_initialize_method_token
				i32 u0x{{cfg.jni_remapping_replacement_type_count:x8}}, ; uint32_t jni_remapping_replacement_type_count
				i32 {{cfg.jni_remapping_replacement_method_index_entry_count}}, ; uint32_t jni_remapping_replacement_method_index_entry_count
				ptr {{strings.GetPointer (cfg.android_package_name, "ApplicationConfig", "android_package_name")}}, ; char* android_package_name
				i1 {{(cfg.have_assembly_store ? "true" : "false")}}; bool have_assembly_store
			}
			""", w.GetAggregateAlignment (structAlignment, ApplicationConfigDataSize));

		WriteDsoCache (w, data, structAlignment);

		w.WriteGlobal ("bundled_assemblies", LlvmIrWriter.GlobalWritable, "[0 x %struct.XamarinAndroidBundledAssembly]", "zeroinitializer", w.GetAggregateAlignment (structAlignment, 0 * XamarinAndroidBundledAssemblyDataSize), " Bundled assembly name buffers, all empty (unused when assembly stores are enabled)");

		var names = new List<string> (data.RuntimePropertyNames.Count);
		foreach (string name in data.RuntimePropertyNames) {
			names.Add ($"\tptr {strings.GetPointer (name)}");
		}
		w.WriteGlobal (
			"init_runtime_property_names",
			LlvmIrWriter.GlobalConstant,
			$"[{names.Count} x ptr]",
			w.ArrayValue (names, i => $" {i} ('{data.RuntimePropertyNames [i]}')"),
			w.GetPointerArrayAlignment (names.Count),
			"Names of properties passed to coreclr_initialize"
		);

		var values = new List<string> (data.RuntimePropertyValues.Count);
		foreach (string? value in data.RuntimePropertyValues) {
			values.Add ($"\tptr {strings.GetPointer (value)}");
		}
		w.WriteGlobal (
			"init_runtime_property_values",
			LlvmIrWriter.GlobalWritable,
			$"[{values.Count} x ptr]",
			w.ArrayValue (values, i => $" {i} ('{data.RuntimePropertyValues [i]}')"),
			w.GetPointerArrayAlignment (values.Count),
			"Values of properties passed to coreclr_initialize"
		);

		ulong assemblyCount = (ulong)NumberOfAssembliesInApk;
		w.WriteGlobal ("assembly_store_bundled_assemblies", LlvmIrWriter.GlobalWritable, $"[{assemblyCount} x %struct.AssemblyStoreSingleAssemblyRuntimeData]", "zeroinitializer", w.GetAggregateAlignment (w.Target.PointerSize, assemblyCount * AssemblyStoreSingleAssemblyRuntimeDataDataSize));
		w.WriteGlobal ("assembly_store", LlvmIrWriter.GlobalWritable, "%struct.AssemblyStoreRuntimeData", $$"""
			{
				ptr null, ; uint8_t* data_start
				i32 0, ; uint32_t assembly_count
				i32 0, ; uint32_t index_entry_count
				ptr null; AssemblyStoreAssemblyDescriptor* assemblies
			}
			""", w.GetAggregateAlignment (structAlignment, AssemblyStoreRuntimeDataDataSize));

		strings.Write (w);
		w.WriteMetadata ();
		output.Flush ();
	}

	void WriteDsoCache (LlvmIrWriter w, State state, ulong structAlignment)
	{
		// Hashes are architecture independent, but the sort is repeated for every architecture as it
		// determines the order of entries with identical hashes
		state.DsoCache.Sort ((DSOCacheEntry a, DSOCacheEntry b) => a.hash.CompareTo (b.hash));

		var entries = new List<string> (state.DsoCache.Count);
		foreach (DSOCacheEntry entry in state.DsoCache) {
			entries.Add ($$"""
					%struct.DSOCacheEntry {
						i32 u0x{{entry.hash:x8}}, {{w.Comment ($" from name: {entry.HashedName}")}}
						i1 {{(entry.ignore ? "true" : "false")}}, ; bool ignore
						i1 {{(entry.is_jni_library ? "true" : "false")}}, ; bool is_jni_library
						i32 {{entry.name_index}}, {{w.Comment ($" name: {entry.RealName}")}}
						ptr null; void* handle
					}
				""");
		}
		w.WriteGlobal (
			"dso_cache",
			LlvmIrWriter.GlobalWritable,
			$"[{entries.Count} x %struct.DSOCacheEntry]",
			w.ArrayValue (entries, i => $" {i}"),
			w.GetAggregateAlignment (structAlignment, (ulong)entries.Count * DSOCacheEntryDataSize),
			" DSO cache entries"
		);

		w.WriteGlobal ("dso_jni_preloads_idx_stride", LlvmIrWriter.GlobalConstant, "i32", state.NameMutationsCount.ToString (), 4);

		// Indices array MUST NOT be sorted, since it groups alias entries together with the main entry
		var indices = new List<string> (state.JniPreloadDSOs.Count);
		var indexNames = new List<string> (state.JniPreloadDSOs.Count);
		foreach (DSOCacheEntry preload in state.JniPreloadDSOs) {
			int dsoIdx = state.DsoCache.FindIndex (entry => ReferenceEquals (entry, preload));
			if (dsoIdx == -1) {
				throw new InvalidOperationException ($"Internal error: DSO entry in JNI preload list not found in the DSO cache list.");
			}

			indices.Add ($"\ti32 {dsoIdx}");
			indexNames.Add (preload.HashedName);
		}

		// Historically, the count has always been a 64-bit integer
		w.WriteGlobal ("dso_jni_preloads_idx_count", LlvmIrWriter.GlobalConstant, "i64", indices.Count.ToString (), 8);
		w.WriteGlobal (
			"dso_jni_preloads_idx",
			LlvmIrWriter.GlobalConstant,
			$"[{indices.Count} x i32]",
			w.ArrayValue (indices, i => $" {indexNames [i]}"),
			w.GetAggregateAlignment (4, (ulong)indices.Count * 4),
			" Indices into dso_cache[] of DSO libraries to preload because of JNI use"
		);

		state.NamesBlob.Write (w, "dso_names_data");
	}

	void InitDSOCache (State state)
	{
		var dsos = new List<(string name, ITaskItem item)> ();
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

			dsos.Add ((name, item));
		}

		var nameMutations = new List<string> ();
		int nameMutationsCount = -1;
		ICollection<string> ignorePreload = MakeJniPreloadIgnoreCollection (Log, NativeLibrariesAlwaysJniPreload, NativeLibrariesNoJniPreload);

		for (int i = 0; i < dsos.Count; i++) {
			string name = dsos[i].name;

			int nameOffset = state.NamesBlob.Add (name);

			bool isJniLibrary = ELFHelper.IsJniLibrary (Log, dsos[i].item.ItemSpec);
			bool ignore_for_preload = ShouldIgnoreForJniPreload (Log, ignorePreload, dsos[i].item);

			nameMutations.Clear();
			AddNameMutations (name);
			if (nameMutationsCount == -1) {
				nameMutationsCount = nameMutations.Count;
			}

			// All mutations point to the actual library name, but have hash of the mutated one
			foreach (string entryName in nameMutations) {
				var entry = new DSOCacheEntry {
					HashedName = entryName,
					RealName = name,

					hash = TypeMapHelper.HashNameForCLR (entryName),
					ignore = false,
					is_jni_library = isJniLibrary,
					name_index = (uint)nameOffset,
				};

				// We must add all aliases to the preloads indices array so that all of them have their handle
				// set when the library is preloaded.
				if (entry.is_jni_library && !ignore_for_preload) {
					state.JniPreloadDSOs.Add (entry);
				}

				state.DsoCache.Add (entry);
			}
		}

		state.NameMutationsCount = (uint)(nameMutationsCount <= 0 ? 1 : nameMutationsCount);

		void AddNameMutations (string name)
		{
			// NOTE: The CoreCLR runtime re-derives these mutations to disambiguate CRC32 hash
			// collisions in the DSO cache (see MonodroidDl::name_is_mutation_of in
			// src/native/clr/include/runtime-base/monodroid-dl.hh). Keep the two in sync.
			nameMutations.Add (name);
			if (name.EndsWith (".dll.so", StringComparison.OrdinalIgnoreCase)) {
				string nameNoExt = Path.GetFileNameWithoutExtension (Path.GetFileNameWithoutExtension (name));
				nameMutations.Add (nameNoExt);
				nameMutations.Add ($"{nameNoExt}.so");
			} else {
				nameMutations.Add (Path.GetFileNameWithoutExtension (name));
			}

			const string libPrefix = "lib";
			if (name.StartsWith (libPrefix, StringComparison.OrdinalIgnoreCase)) {
				AddNameMutations (name.Substring (libPrefix.Length));
			}
		}
	}

	internal static bool ShouldIgnoreForJniPreload (TaskLoggingHelper log, ICollection<string> libsToIgnore, ITaskItem libItem)
	{
		if (libsToIgnore.Count == 0) {
			return false;
		}

		string? libFileName = GetFileName (log, libItem);
		if (libFileName == null) {
			return false; // We have no idea what it is, so let the caller handle the situation
		}

		return libsToIgnore.Contains (libFileName);
	}

	internal static ICollection<string> MakeJniPreloadIgnoreCollection (TaskLoggingHelper log, ICollection<ITaskItem>? alwaysPreload, ICollection<ITaskItem>? ignorePreload)
	{
		// There Can Be Only One, no matter what name casing is on the user's build OS.
		var libsToIgnore = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		if (ignorePreload == null || ignorePreload.Count == 0) {
			return libsToIgnore;
		}

		var neverIgnore = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		string? fileName;
		if (alwaysPreload != null) {
			foreach (ITaskItem item in alwaysPreload) {
				fileName = GetFileName (log, item);
				if (fileName == null) {
					continue;
				}

				neverIgnore.Add (fileName);
			}
		}

		foreach (ITaskItem item in ignorePreload) {
			fileName = GetFileName (log, item);
			if (fileName == null) {
				continue;
			}

			if (neverIgnore.Contains (fileName)) {
				log.LogDebugMessage ($"Native library '{item.ItemSpec}' cannot be ignored when preloading JNI native libraries.");
				continue;
			}

			libsToIgnore.Add (fileName);
		}

		return libsToIgnore;
	}

	static string? GetFileName (TaskLoggingHelper log, ITaskItem item)
	{
		string? name = item.GetMetadata ("ArchiveFileName");
		if (String.IsNullOrEmpty (name)) {
			name = MonoAndroidHelper.GetNormalizedNativeLibraryName (item);
		}

		if (String.IsNullOrEmpty (name)) {
			log.LogDebugMessage ($"Failed to convert item path '{item.ItemSpec}' to canonical native shared library name.");
			return null;
		}

		return name;
	}
}
