#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks;

class ApplicationConfigNativeAssemblyGeneratorCLR : LlvmIrComposer
{
	// From host_runtime_contract.h in dotnet/runtime
	const string HOST_PROPERTY_RUNTIME_CONTRACT = "HOST_RUNTIME_CONTRACT";
	const string HOST_PROPERTY_BUNDLE_PROBE     = "BUNDLE_PROBE";
	const string HOST_PROPERTY_PINVOKE_OVERRIDE = "PINVOKE_OVERRIDE";

	sealed class DSOCacheEntryContextDataProvider : NativeAssemblerStructContextDataProvider
	{
		public override string GetComment (object data, string fieldName)
		{
			var dso_entry = EnsureType<DSOCacheEntry> (data);
			if (MonoAndroidHelper.StringEquals ("hash", fieldName)) {
				return $" from name: {dso_entry.HashedName}";
			}

			if (MonoAndroidHelper.StringEquals ("name_index", fieldName)) {
				return $" name: {dso_entry.RealName}";
			}

			return String.Empty;
		}
	}

	// Disable "Field 'X' is never assigned to, and will always have its default value Y"
	// Classes below are used in native code generation, thus all the fields must be present
	// but they aren't always assigned values (which is fine).
	#pragma warning disable CS0649

	// Order of fields and their type must correspond *exactly* (with exception of the
	// ignored managed members) to that in
	// src/native/clr/include/xamarin-app.hh DSOCacheEntry structure
	[NativeAssemblerStructContextDataProvider (typeof (DSOCacheEntryContextDataProvider))]
	sealed class DSOCacheEntry
	{
		[NativeAssembler (Ignore = true)]
		public string? HashedName;

		[NativeAssembler (Ignore = true)]
		public string? RealName;

		[NativeAssembler (UsesDataProvider = true, NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal)]
		public ulong hash;

		[NativeAssembler (NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal)]
		public ulong real_name_hash;
		public bool ignore;
		public bool is_jni_library;

		[NativeAssembler (UsesDataProvider = true)]
		public uint name_index;
		public IntPtr handle = IntPtr.Zero;
	}

	sealed class DSOApkEntryContextDataProvider : NativeAssemblerStructContextDataProvider
	{
		public override string GetComment (object data, string fieldName)
		{
			var dso_apk_entry = EnsureType<DSOApkEntry> (data);
			if (MonoAndroidHelper.StringEquals ("name_hash", fieldName)) {
				return $" from name: {dso_apk_entry.Name}";
			}

			return String.Empty;
		}
	}

	// Order of fields and their type must correspond *exactly* (with exception of the
	// ignored managed members) to that in
	// src/native/clr/include/xamarin-app.hh DSOApkEntry structure
	[NativeAssemblerStructContextDataProvider (typeof (DSOApkEntryContextDataProvider))]
	sealed class DSOApkEntry
	{
		[NativeAssembler (Ignore = true)]
		public string Name = String.Empty;

		[NativeAssembler (UsesDataProvider = true, NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal)]
		public ulong name_hash;
		public uint  offset; // offset into the APK
		public int   fd; // apk file descriptor
	};

	// Order of fields and their type must correspond *exactly* to that in
	// src/monodroid/jni/xamarin-app.hh AssemblyStoreAssemblyDescriptor structure
	sealed class AssemblyStoreAssemblyDescriptor
	{
		public uint data_offset;
		public uint data_size;

		public uint debug_data_offset;
		public uint debug_data_size;

		public uint config_data_offset;
		public uint config_data_size;
	}

	// Order of fields and their type must correspond *exactly* to that in
	// src/monodroid/jni/xamarin-app.hh AssemblyStoreSingleAssemblyRuntimeData structure
	sealed class AssemblyStoreSingleAssemblyRuntimeData
	{
		[NativePointer]
		public byte image_data;

		[NativePointer]
		public byte debug_info_data;

		[NativePointer]
		public byte config_data;

		[NativePointer]
		public AssemblyStoreAssemblyDescriptor? descriptor;
	}

	// Order of fields and their type must correspond *exactly* to that in
	// src/native/clr/include/xamarin-app.hh AssemblyStoreRuntimeData structure
	sealed class AssemblyStoreRuntimeData
	{
		[NativePointer (IsNull = true)]
		public byte data_start;
		public uint assembly_count;
		public uint index_entry_count;

		[NativePointer (IsNull = true)]
		public AssemblyStoreAssemblyDescriptor? assemblies;
	}

	sealed class RuntimePropertyContextDataProvider : NativeAssemblerStructContextDataProvider
	{
		public override string GetComment (object data, string fieldName)
		{
			var runtimeProp = EnsureType<RuntimeProperty> (data);
			if (MonoAndroidHelper.StringEquals ("key_index", fieldName)) {
				return $" '{runtimeProp.Key}'";
			}

			if (MonoAndroidHelper.StringEquals ("value_index", fieldName)) {
				return $" '{runtimeProp.Value}'";
			}

			return String.Empty;
		}
	}

	// Order of fields and their types must correspond *exactly* to that in
	// src/native/clr/include/xamarin-app.hh RuntimeProperty structure
	[NativeAssemblerStructContextDataProvider (typeof (RuntimePropertyContextDataProvider))]
	sealed class RuntimeProperty
	{
		[NativeAssembler (Ignore = true)]
		public string? Key;

		[NativeAssembler (Ignore = true)]
		public string? Value;

		[NativeAssembler (UsesDataProvider = true)]
		public uint key_index;

		[NativeAssembler (UsesDataProvider = true)]
		public uint value_index;
		public uint value_size;
	}

	// Order of fields and their types must correspond *exactly* to that in
	// src/native/clr/include/xamarin-app.hh RuntimePropertyIndexEntry structure
	sealed class RuntimePropertyIndexEntry
	{
		[NativeAssembler (Ignore = true)]
		public string? HashedKey;

		[NativeAssembler (NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal)]
		public ulong key_hash;
		public uint index;
	}

	sealed class XamarinAndroidBundledAssemblyContextDataProvider : NativeAssemblerStructContextDataProvider
	{
		public override ulong GetBufferSize (object data, string fieldName)
		{
			var xaba = EnsureType<XamarinAndroidBundledAssembly> (data);
			if (MonoAndroidHelper.StringEquals ("name", fieldName)) {
				return xaba.name_length;
			}

			if (MonoAndroidHelper.StringEquals ("file_name", fieldName)) {
				return xaba.name_length + MonoAndroidHelper.GetMangledAssemblyNameSizeOverhead ();
			}

			return 0;
		}
	}

	// Order of fields and their type must correspond *exactly* to that in
	// src/monodroid/jni/xamarin-app.hh XamarinAndroidBundledAssembly structure
	[NativeAssemblerStructContextDataProvider (typeof (XamarinAndroidBundledAssemblyContextDataProvider))]
	sealed class XamarinAndroidBundledAssembly
	{
		public int  file_fd;

		[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToPreAllocatedBuffer = true)]
		public string? file_name;
		public uint data_offset;
		public uint data_size;

		[NativePointer]
		public byte data;
		public uint name_length;

		[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToPreAllocatedBuffer = true)]
		public string? name;
	}
#pragma warning restore CS0649

	sealed class DsoCacheState
	{
		public List<StructureInstance<DSOCacheEntry>> DsoCache = [];
		public List<DSOCacheEntry> JniPreloadDSOs = [];
		public List<string> JniPreloadNames = [];
		public List<StructureInstance<DSOCacheEntry>> AotDsoCache = [];
		public LlvmIrStringBlob NamesBlob = null!;
		public uint NameMutationsCount = 1;
	}

	// Keep in sync with FORMAT_TAG in src/monodroid/jni/xamarin-app.hh
	const ulong FORMAT_TAG = 0x00025E6972616D58; // 'Xmari^XY' where XY is the format version

	// List of library names to ignore when generating the list of JNI-using libraries to preload
	internal static readonly HashSet<string> DsoCacheJniPreloadIgnore = new (StringComparer.OrdinalIgnoreCase) {
		"libmonodroid.so",
	};

	SortedDictionary <string, string>? environmentVariables;
	SortedDictionary <string, string>? systemProperties;
	SortedDictionary <string, string>? runtimeProperties;
	StructureInstance? application_config;

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value - assigned conditionally by build process
	List<StructureInstance<XamarinAndroidBundledAssembly>>? xamarinAndroidBundledAssemblies;
#pragma warning restore CS0649
	List<StructureInstance<RuntimeProperty>>? runtimePropertiesData;
	List<StructureInstance<RuntimePropertyIndexEntry>>? runtimePropertyIndex;

	StructureInfo? applicationConfigStructureInfo;
	StructureInfo? dsoCacheEntryStructureInfo;
	StructureInfo? dsoApkEntryStructureInfo;
	StructureInfo? xamarinAndroidBundledAssemblyStructureInfo;
	StructureInfo? assemblyStoreSingleAssemblyRuntimeDataStructureinfo;
	StructureInfo? assemblyStoreRuntimeDataStructureInfo;
	StructureInfo? runtimePropertyStructureInfo;
	StructureInfo? runtimePropertyIndexEntryStructureInfo;
#pragma warning disable CS0169 // Field is never used - might be used in future versions
	StructureInfo? hostConfigurationPropertyStructureInfo;
#pragma warning restore CS0169
#pragma warning disable CS0169 // Field is never used - might be used in future versions
	StructureInfo? hostConfigurationPropertiesStructureInfo;
#pragma warning restore CS0169
	StructureInfo? appEnvironmentVariableStructureInfo;

	public bool UsesAssemblyPreload { get; set; }
	public string AndroidPackageName { get; set; } = "";
	public bool JniAddNativeMethodRegistrationAttributePresent { get; set; }
	public int NumberOfAssembliesInApk { get; set; }
	public int BundledAssemblyNameWidth { get; set; } // including the trailing NUL
	public int AndroidRuntimeJNIEnvToken { get; set; }
	public int JNIEnvInitializeToken { get; set; }
	public int JNIEnvRegisterJniNativesToken { get; set; }
	public int JniRemappingReplacementTypeCount { get; set; }
	public int JniRemappingReplacementMethodIndexEntryCount { get; set; }
	public PackageNamingPolicy PackageNamingPolicy { get; set; }
	public List<ITaskItem> NativeLibraries { get; set; } = [];
	public ICollection<ITaskItem>? NativeLibrariesNoJniPreload { get; set; }
	public ICollection<ITaskItem>? NativeLibrarysAlwaysJniPreload { get; set; }
	public bool MarshalMethodsEnabled { get; set; }
	public bool ManagedMarshalMethodsLookupEnabled { get; set; }
	public bool IgnoreSplitConfigs { get; set; }

	public ApplicationConfigNativeAssemblyGeneratorCLR (IDictionary<string, string> environmentVariables, IDictionary<string, string> systemProperties,
		IDictionary<string, string>? runtimeProperties, TaskLoggingHelper log)
	: base (log)
	{
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

		// This will be filled in by the native host.
		this.runtimeProperties[HOST_PROPERTY_RUNTIME_CONTRACT] = String.Empty;

		// these mustn't be there, they would break our host contract
		this.runtimeProperties.Remove (HOST_PROPERTY_PINVOKE_OVERRIDE);
		this.runtimeProperties.Remove (HOST_PROPERTY_BUNDLE_PROBE);
	}

	protected override void Construct (LlvmIrModule module)
	{
		MapStructures (module);

		module.AddGlobalVariable ("format_tag", FORMAT_TAG, comment: $" 0x{FORMAT_TAG:x}");

		var envVarsBlob = new LlvmIrStringBlob ();
		List<StructureInstance<LlvmIrHelpers.AppEnvironmentVariable>> appEnvVars = LlvmIrHelpers.MakeEnvironmentVariableList (
			Log,
			environmentVariables,
			envVarsBlob,
			appEnvironmentVariableStructureInfo
		);

		var envVars = new LlvmIrGlobalVariable (appEnvVars, "app_environment_variables") {
			Comment = " Application environment variables array, name:value",
			Options = LlvmIrVariableOptions.GlobalConstant,
		};
		module.Add (envVars);
		module.AddGlobalVariable ("app_environment_variable_contents", envVarsBlob, LlvmIrVariableOptions.GlobalConstant);

		// We reuse the same structure as for environment variables, there's no point in adding a new, identical, one
		var sysPropsBlob = new LlvmIrStringBlob ();
		List<StructureInstance<LlvmIrHelpers.AppEnvironmentVariable>> appSysProps = LlvmIrHelpers.MakeEnvironmentVariableList (
			Log,
			systemProperties,
			sysPropsBlob,
			appEnvironmentVariableStructureInfo
		);

		var sysProps = new LlvmIrGlobalVariable (appSysProps, "app_system_properties") {
			Comment = " System properties defined by the application",
			Options = LlvmIrVariableOptions.GlobalConstant,
		};
		module.Add (sysProps);
		module.AddGlobalVariable ("app_system_property_contents", sysPropsBlob, LlvmIrVariableOptions.GlobalConstant);

		DsoCacheState dsoState = InitDSOCache ();
		var app_cfg = new ApplicationConfigCLR {
			uses_assembly_preload = UsesAssemblyPreload,
			jni_add_native_method_registration_attribute_present = JniAddNativeMethodRegistrationAttributePresent,
			marshal_methods_enabled = MarshalMethodsEnabled,
			managed_marshal_methods_lookup_enabled = ManagedMarshalMethodsLookupEnabled,
			ignore_split_configs = IgnoreSplitConfigs,
			number_of_runtime_properties = (uint)(runtimeProperties == null ? 0 : runtimeProperties.Count),
			package_naming_policy = (uint)PackageNamingPolicy,
			environment_variable_count = (uint)(environmentVariables == null ? 0 : environmentVariables.Count),
			system_property_count = (uint)(appSysProps.Count),
			number_of_assemblies_in_apk = (uint)NumberOfAssembliesInApk,
			number_of_shared_libraries = (uint)NativeLibraries.Count,
			bundled_assembly_name_width = (uint)BundledAssemblyNameWidth,
			number_of_dso_cache_entries = (uint)dsoState.DsoCache.Count,
			number_of_aot_cache_entries = (uint)dsoState.AotDsoCache.Count,
			android_runtime_jnienv_class_token = (uint)AndroidRuntimeJNIEnvToken,
			jnienv_initialize_method_token = (uint)JNIEnvInitializeToken,
			jnienv_registerjninatives_method_token = (uint)JNIEnvRegisterJniNativesToken,
			jni_remapping_replacement_type_count = (uint)JniRemappingReplacementTypeCount,
			jni_remapping_replacement_method_index_entry_count = (uint)JniRemappingReplacementMethodIndexEntryCount,
			android_package_name = AndroidPackageName,
		};
		application_config = new StructureInstance<ApplicationConfigCLR> (applicationConfigStructureInfo, app_cfg);
		module.AddGlobalVariable ("application_config", application_config);

		var dso_cache = new LlvmIrGlobalVariable (dsoState.DsoCache, "dso_cache", LlvmIrVariableOptions.GlobalWritable) {
			Comment = " DSO cache entries",
			BeforeWriteCallback = HashAndSortDSOCache,
		};
		module.Add (dso_cache);

		module.AddGlobalVariable ("dso_jni_preloads_idx_stride", dsoState.NameMutationsCount);

		// This variable MUST be written after `dso_cache` since it relies on sorting performed by HashAndSortDSOCache
		var dso_jni_preloads_idx = new LlvmIrGlobalVariable (typeof (List<uint>), "dso_jni_preloads_idx", LlvmIrVariableOptions.GlobalConstant) {
			Comment = " Indices into dso_cache[] of DSO libraries to preload because of JNI use",
			ArrayItemCount = (uint)dsoState.JniPreloadDSOs.Count,
			GetArrayItemCommentCallback = GetPreloadIndicesLibraryName,
			GetArrayItemCommentCallbackCallerState = dsoState,
			BeforeWriteCallback = PopulatePreloadIndices,
			BeforeWriteCallbackCallerState = dsoState,
		};
		module.AddGlobalVariable ("dso_jni_preloads_idx_count", dso_jni_preloads_idx.ArrayItemCount);
		module.Add (dso_jni_preloads_idx);

		var aot_dso_cache = new LlvmIrGlobalVariable (dsoState.AotDsoCache, "aot_dso_cache", LlvmIrVariableOptions.GlobalWritable) {
			Comment = " AOT DSO cache entries",
			BeforeWriteCallback = HashAndSortDSOCache,
		};
		module.Add (aot_dso_cache);
		module.AddGlobalVariable ("dso_names_data", dsoState.NamesBlob, LlvmIrVariableOptions.GlobalConstant);

		var dso_apk_entries = new LlvmIrGlobalVariable (new List<StructureInstance<DSOApkEntry>> (NativeLibraries.Count), "dso_apk_entries") {
			Options = LlvmIrVariableOptions.GlobalWritable,
			BeforeWriteCallback = PopulateDsoApkEntries,
		};
		module.Add (dso_apk_entries);

		string bundledBuffersSize = xamarinAndroidBundledAssemblies == null ? "empty (unused when assembly stores are enabled)" : $"{BundledAssemblyNameWidth} bytes long";
		var bundled_assemblies = new LlvmIrGlobalVariable (typeof(List<StructureInstance<XamarinAndroidBundledAssembly>>), "bundled_assemblies", LlvmIrVariableOptions.GlobalWritable) {
			Value = xamarinAndroidBundledAssemblies,
			Comment = $" Bundled assembly name buffers, all {bundledBuffersSize}",
		};
		module.Add (bundled_assemblies);

		(runtimePropertiesData, runtimePropertyIndex, LlvmIrStringBlob runtimePropsBlob) = InitRuntimeProperties ();
		var runtime_properties = new LlvmIrGlobalVariable (runtimePropertiesData, "runtime_properties", LlvmIrVariableOptions.GlobalConstant) {
			Comment = "Runtime config properties",
		};
		module.Add (runtime_properties);

		var runtime_properties_data = new LlvmIrGlobalVariable (runtimePropsBlob, "runtime_properties_data", LlvmIrVariableOptions.GlobalConstant) {
			Comment = "Runtime config properties data",
		};
		module.Add (runtime_properties_data);

		var runtime_property_index = new LlvmIrGlobalVariable (runtimePropertyIndex, "runtime_property_index", LlvmIrVariableOptions.GlobalConstant) {
			Comment = "Runtime config property index, sorted on property key hash",
			BeforeWriteCallback = HashAndSortRuntimePropertiesIndex,
		};
		module.Add (runtime_property_index);

		// HOST_PROPERTY_RUNTIME_CONTRACT will come first, our native runtime requires that since it needs
		// to set its value in the values array and we don't want to spend time searching for the index, nor
		// we want to add yet another variable storing the index to the entry. KISS.
		var runtime_property_names = new List<string> {
			HOST_PROPERTY_RUNTIME_CONTRACT,
		};
		var runtime_property_values = new List<string?> {
			null,
		};

		if (runtimeProperties != null) {
			foreach (var kvp in runtimeProperties) {
				if (MonoAndroidHelper.StringEquals (kvp.Key, HOST_PROPERTY_RUNTIME_CONTRACT)) {
					continue;
				}
				runtime_property_names.Add (kvp.Key);
				runtime_property_values.Add (kvp.Value);
			}
		}

		var init_runtime_property_names = new LlvmIrGlobalVariable (runtime_property_names, "init_runtime_property_names", LlvmIrVariableOptions.GlobalConstant) {
			Comment = "Names of properties passed to coreclr_initialize",
		};
		module.Add (init_runtime_property_names);

		var init_runtime_property_values = new LlvmIrGlobalVariable (runtime_property_values, "init_runtime_property_values", LlvmIrVariableOptions.GlobalWritable) {
			Comment = "Values of properties passed to coreclr_initialize",
		};
		module.Add (init_runtime_property_values);

		AddAssemblyStores (module);
	}

	void HashAndSortRuntimePropertiesIndex (LlvmIrVariable variable, LlvmIrModuleTarget target, object? state)
	{
		var index = variable.Value as List<StructureInstance<RuntimePropertyIndexEntry>>;
		if (index == null) {
			return;
		}

		bool is64Bit = target.Is64Bit;
		foreach (StructureInstance instance in index) {
			if (instance.Obj == null) {
				throw new InvalidOperationException ("Internal error: runtime property index must not contain null entries");
			}

			var entry = instance.Obj as RuntimePropertyIndexEntry;
			if (entry == null) {
				throw new InvalidOperationException ($"Internal error: runtime property index entry has unexpected type {instance.Obj.GetType ()}");
			}

			entry.key_hash = MonoAndroidHelper.GetXxHash (entry.HashedKey ?? "", is64Bit);
		};

		index.Sort ((StructureInstance<RuntimePropertyIndexEntry> a, StructureInstance<RuntimePropertyIndexEntry> b) => {
			if (a.Instance == null || b.Instance == null) return 0;
			return a.Instance.key_hash.CompareTo (b.Instance.key_hash);
		});
	}

	(
		List<StructureInstance<RuntimeProperty>> runtimeProps,
		List<StructureInstance<RuntimePropertyIndexEntry>> runtimePropsIndex,
		LlvmIrStringBlob
	) InitRuntimeProperties ()
	{
		var runtimeProps = new List<StructureInstance<RuntimeProperty>> ();
		var runtimePropsIndex = new List<StructureInstance<RuntimePropertyIndexEntry>> ();
		var propsBlob = new LlvmIrStringBlob ();

		if (runtimeProperties == null || runtimeProperties.Count == 0) {
			return (runtimeProps, runtimePropsIndex, propsBlob);
		}

		foreach (var kvp in runtimeProperties) {
			string name = kvp.Key;
			string value = kvp.Value;
			(int name_index, _) = propsBlob.Add (name);
			(int value_index, int value_size) = propsBlob.Add (value);

			var prop = new RuntimeProperty {
				Key = name,
				Value = value,

				key_index = (uint)name_index,
				value_index = (uint)value_index,

				// Includes the terminating NUL
				value_size = (uint)value_size,
			};
			runtimeProps.Add (new StructureInstance<RuntimeProperty> (runtimePropertyStructureInfo, prop));

			var indexEntry = new RuntimePropertyIndexEntry {
				HashedKey = prop.Key,
				index = (uint)(runtimeProps.Count - 1),
			};
			runtimePropsIndex.Add (new StructureInstance<RuntimePropertyIndexEntry> (runtimePropertyIndexEntryStructureInfo, indexEntry));
		}

		return (runtimeProps, runtimePropsIndex, propsBlob);
	}

	void AddAssemblyStores (LlvmIrModule module)
	{
		ulong itemCount = (ulong)(NumberOfAssembliesInApk);
		var assembly_store_bundled_assemblies = new LlvmIrGlobalVariable (typeof(List<StructureInstance<AssemblyStoreSingleAssemblyRuntimeData>>), "assembly_store_bundled_assemblies", LlvmIrVariableOptions.GlobalWritable) {
			ZeroInitializeArray = true,
			ArrayItemCount = itemCount,
		};
		module.Add (assembly_store_bundled_assemblies);

		var storeRuntimeData = new AssemblyStoreRuntimeData {
			data_start = 0,
			assembly_count = 0,
		};

		var assembly_store = new LlvmIrGlobalVariable (
			new StructureInstance<AssemblyStoreRuntimeData>(assemblyStoreRuntimeDataStructureInfo, storeRuntimeData),
			"assembly_store",
			LlvmIrVariableOptions.GlobalWritable
		);
		module.Add (assembly_store);
	}

	string? GetPreloadIndicesLibraryName (LlvmIrVariable v, LlvmIrModuleTarget target, ulong index, object? value, object? callerState)
	{
		// Instead of throwing for such a triviality like a comment, we will return error messages as comments instead
		var dsoState = callerState as DsoCacheState;
		if (dsoState == null) {
			return " Internal error: DSO state not present.";
		}

		if (index >= (ulong)dsoState.JniPreloadNames.Count) {
			return $" Invalid index {index}";
		}

		return $" {dsoState.JniPreloadNames[(int)index]}";
	}

	void PopulateDsoApkEntries (LlvmIrVariable variable, LlvmIrModuleTarget target, object? state)
	{
		var dso_apk_entries = variable.Value as List<StructureInstance<DSOApkEntry>>;
		if (dso_apk_entries == null) {
			throw new InvalidOperationException ("Internal error: DSO apk entries list not present.");
		}

		if (dso_apk_entries.Capacity != NativeLibraries.Count) {
			throw new InvalidOperationException ($"Internal error: DSO apk entries count ({dso_apk_entries.Count}) is different to the native libraries count ({NativeLibraries.Count}).");
		}

		bool is64Bit = target.Is64Bit;
		foreach (ITaskItem item in NativeLibraries) {
			string name = Path.GetFileName (item.ItemSpec);
			var entry = new DSOApkEntry {
				Name = name,

				name_hash = MonoAndroidHelper.GetXxHash (name, is64Bit),
				offset = 0,
				fd = -1,
			};
			dso_apk_entries.Add (new StructureInstance<DSOApkEntry> (dsoApkEntryStructureInfo, entry));
		}

		dso_apk_entries.Sort ((StructureInstance<DSOApkEntry> a, StructureInstance<DSOApkEntry> b) => {
			return a.Instance!.name_hash.CompareTo (b.Instance!.name_hash);
		});
	}

	void PopulatePreloadIndices (LlvmIrVariable variable, LlvmIrModuleTarget target, object? state)
	{
		var dsoState = state as DsoCacheState;
		if (dsoState == null) {
			throw new InvalidOperationException ("Internal error: DSO state not present.");
		}

		var dsoNames = new List<string> ();

		// Indices array MUST NOT be sorted, since it groups alias entries together with the main entry
		var indices = new List<uint> ();
		variable.Value = indices;
		foreach (DSOCacheEntry preload in dsoState.JniPreloadDSOs) {
			int dsoIdx = dsoState.DsoCache.FindIndex (entry => {
				if (entry.Instance == null) {
					return false;
				}

				return entry.Instance.hash == preload.hash && entry.Instance.real_name_hash == preload.real_name_hash;
			});

			if (dsoIdx == -1) {
				throw new InvalidOperationException ($"Internal error: DSO entry in JNI preload list not found in the DSO cache list.");
			}

			indices.Add ((uint)dsoIdx);
			dsoNames.Add (preload.HashedName ?? String.Empty);
		}
		dsoState.JniPreloadNames = dsoNames;
	}

	void HashAndSortDSOCache (LlvmIrVariable variable, LlvmIrModuleTarget target, object? state)
	{
		var cache = variable.Value as List<StructureInstance<DSOCacheEntry>>;
		if (cache == null) {
			throw new InvalidOperationException ($"Internal error: DSO cache must not be empty");
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

			entry.hash = MonoAndroidHelper.GetXxHash (entry.HashedName ?? "", is64Bit);
			entry.real_name_hash = MonoAndroidHelper.GetXxHash (entry.RealName ?? "", is64Bit);
		}

		cache.Sort ((StructureInstance<DSOCacheEntry> a, StructureInstance<DSOCacheEntry> b) => {
			if (a.Instance == null || b.Instance == null) return 0;
			return a.Instance.hash.CompareTo (b.Instance.hash);
		});
	}

	DsoCacheState InitDSOCache ()
	{
		var dsos = new List<(string name, string nameLabel, bool ignore, ITaskItem item)> ();
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

			dsos.Add ((name, $"dsoName{dsos.Count.ToString (CultureInfo.InvariantCulture)}", ELFHelper.IsEmptyAOTLibrary (Log, item.ItemSpec), item));
		}

		var dsoCache = new List<StructureInstance<DSOCacheEntry>> ();
		var jniPreloads = new List<DSOCacheEntry> ();
		var aotDsoCache = new List<StructureInstance<DSOCacheEntry>> ();
		var nameMutations = new List<string> ();
		var dsoNamesBlob = new LlvmIrStringBlob ();
		int nameMutationsCount = -1;
		ICollection<string> ignorePreload = MakeJniPreloadIgnoreCollection (Log, NativeLibrarysAlwaysJniPreload, NativeLibrariesNoJniPreload);

		for (int i = 0; i < dsos.Count; i++) {
			string name = dsos[i].name;
			(int nameOffset, _) = dsoNamesBlob.Add (name);

			bool isJniLibrary = ELFHelper.IsJniLibrary (Log, dsos[i].item.ItemSpec);
			bool ignore = dsos[i].ignore;
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

					hash = 0, // Hash is arch-specific, we compute it before writing
					ignore = ignore,
					is_jni_library = isJniLibrary,
					name_index = (uint)nameOffset,
				};

				var item = new StructureInstance<DSOCacheEntry> (dsoCacheEntryStructureInfo, entry);
				if (name.StartsWith ("libaot-", StringComparison.OrdinalIgnoreCase)) {
					aotDsoCache.Add (item);
					continue;
				}

				// We must add all aliases to the preloads indices array so that all of them have their handle
				// set when the library is preloaded.
				if (entry.is_jni_library && !ignore_for_preload) {
					jniPreloads.Add (entry);
				}

				dsoCache.Add (item);
			}
		}

		return new DsoCacheState {
			DsoCache = dsoCache,
			JniPreloadDSOs = jniPreloads,
			AotDsoCache = aotDsoCache,
			NamesBlob = dsoNamesBlob,
			NameMutationsCount = (uint)(nameMutationsCount <= 0 ? 1 : nameMutationsCount),
		};

		void AddNameMutations (string name)
		{
			nameMutations.Add (name);
			if (name.EndsWith (".dll.so", StringComparison.OrdinalIgnoreCase)) {
				string nameNoExt = Path.GetFileNameWithoutExtension (Path.GetFileNameWithoutExtension (name))!;
				nameMutations.Add (nameNoExt);

				// This helps us at runtime, because sometimes MonoVM will ask for "AssemblyName" and sometimes for "AssemblyName.dll".
				// In the former case, the runtime would ask for the "libaot-AssemblyName.so" image, which doesn't exist - we have
				// "libaot-AssemblyName.dll.so" instead and, thus, we are forced to check for and append the missing ".dll" extension when
				// loading the assembly, unnecessarily wasting time.
				nameMutations.Add ($"{nameNoExt}.so");
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
		applicationConfigStructureInfo = module.MapStructure<ApplicationConfigCLR> ();
		module.MapStructure<AssemblyStoreAssemblyDescriptor> ();
		assemblyStoreSingleAssemblyRuntimeDataStructureinfo = module.MapStructure<AssemblyStoreSingleAssemblyRuntimeData> ();
		assemblyStoreRuntimeDataStructureInfo = module.MapStructure<AssemblyStoreRuntimeData> ();
		xamarinAndroidBundledAssemblyStructureInfo = module.MapStructure<XamarinAndroidBundledAssembly> ();
		dsoCacheEntryStructureInfo = module.MapStructure<DSOCacheEntry> ();
		dsoApkEntryStructureInfo = module.MapStructure<DSOApkEntry> ();
		runtimePropertyStructureInfo = module.MapStructure<RuntimeProperty> ();
		runtimePropertyIndexEntryStructureInfo = module.MapStructure<RuntimePropertyIndexEntry> ();
		appEnvironmentVariableStructureInfo = module.MapStructure<LlvmIrHelpers.AppEnvironmentVariable> ();
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
