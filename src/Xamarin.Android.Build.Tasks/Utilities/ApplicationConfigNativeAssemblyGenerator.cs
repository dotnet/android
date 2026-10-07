#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks;

class ApplicationConfigNativeAssemblyGenerator : LlvmIrComposer
{
	// From host_runtime_contract.h in dotnet/runtime
	const string HOST_PROPERTY_RUNTIME_CONTRACT   = "HOST_RUNTIME_CONTRACT";
	const string HOST_PROPERTY_BUNDLE_PROBE       = "BUNDLE_PROBE";
	const string HOST_PROPERTY_PINVOKE_OVERRIDE   = "PINVOKE_OVERRIDE";
	const string HOST_PROPERTY_RUNTIME_IDENTIFIER = "RUNTIME_IDENTIFIER";
	const string HOST_PROPERTY_APP_CONTEXT_BASE_DIRECTORY = "APP_CONTEXT_BASE_DIRECTORY";

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
		public uint hash;

		public bool ignore;
		public bool is_jni_library;

		[NativeAssembler (UsesDataProvider = true)]
		public uint name_index;
		public IntPtr handle = IntPtr.Zero;
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
	DsoCacheState? bootstrapDsoState;
	StructureInstance? application_config;

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value - assigned conditionally by build process
	List<StructureInstance<XamarinAndroidBundledAssembly>>? xamarinAndroidBundledAssemblies;
#pragma warning restore CS0649

	StructureInfo? applicationConfigStructureInfo;
	StructureInfo? dsoCacheEntryStructureInfo;
	StructureInfo? xamarinAndroidBundledAssemblyStructureInfo;
	StructureInfo? appEnvironmentVariableStructureInfo;

	public string AndroidPackageName { get; set; } = "";
	public int NumberOfAssembliesInApk { get; set; }
	public int BundledAssemblyNameWidth { get; set; } // including the trailing NUL
	public PackageNamingPolicy PackageNamingPolicy { get; set; }
	public List<ITaskItem> NativeLibraries { get; set; } = [];
	public ICollection<ITaskItem>? NativeLibrariesNoJniPreload { get; set; }
	public ICollection<ITaskItem>? NativeLibrariesAlwaysJniPreload { get; set; }
	public bool IgnoreSplitConfigs { get; set; }
	public bool HaveAssemblyStore { get; set; }
	public bool CoreClrBootstrap { get; set; }

	public ApplicationConfigNativeAssemblyGenerator (IDictionary<string, string> environmentVariables, IDictionary<string, string> systemProperties,
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

		// These will be filled in by the native host.
		this.runtimeProperties[HOST_PROPERTY_RUNTIME_CONTRACT] = String.Empty;
		this.runtimeProperties[HOST_PROPERTY_RUNTIME_IDENTIFIER] = String.Empty;
		this.runtimeProperties[HOST_PROPERTY_APP_CONTEXT_BASE_DIRECTORY] = String.Empty;

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
			CoreClrBootstrap ? null : environmentVariables,
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
			CoreClrBootstrap ? null : systemProperties,
			sysPropsBlob,
			appEnvironmentVariableStructureInfo
		);

		var sysProps = new LlvmIrGlobalVariable (appSysProps, "app_system_properties") {
			Comment = " System properties defined by the application",
			Options = LlvmIrVariableOptions.GlobalConstant,
		};
		module.Add (sysProps);
		module.AddGlobalVariable ("app_system_property_contents", sysPropsBlob, LlvmIrVariableOptions.GlobalConstant);

		bootstrapDsoState = InitDSOCache ();
		DsoCacheState dsoState = CoreClrBootstrap ? new DsoCacheState {
			NamesBlob = new LlvmIrStringBlob (),
		} : bootstrapDsoState;
		var app_cfg = new ApplicationConfig {
			ignore_split_configs = !CoreClrBootstrap && IgnoreSplitConfigs,
			number_of_runtime_properties = CoreClrBootstrap ? 3u : (uint)(runtimeProperties == null ? 0 : runtimeProperties.Count),
			package_naming_policy = CoreClrBootstrap ? 0u : (uint)PackageNamingPolicy,
			environment_variable_count = (uint)(CoreClrBootstrap || environmentVariables == null ? 0 : environmentVariables.Count),
			system_property_count = (uint)(appSysProps.Count),
			number_of_assemblies_in_apk = CoreClrBootstrap ? 0u : (uint)NumberOfAssembliesInApk,
			number_of_shared_libraries = CoreClrBootstrap ? 0u : (uint)NativeLibraries.Count,
			bundled_assembly_name_width = CoreClrBootstrap ? 0u : (uint)BundledAssemblyNameWidth,
			number_of_dso_cache_entries = (uint)dsoState.DsoCache.Count,
			android_package_name = CoreClrBootstrap ? "com.xamarin.test" : AndroidPackageName,
			have_assembly_store = !CoreClrBootstrap && HaveAssemblyStore,
		};
		application_config = new StructureInstance<ApplicationConfig> (applicationConfigStructureInfo, app_cfg);
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

		module.AddGlobalVariable ("dso_names_data", dsoState.NamesBlob, LlvmIrVariableOptions.GlobalConstant);

		string bundledBuffersSize = xamarinAndroidBundledAssemblies == null ? "empty (unused when assembly stores are enabled)" : $"{BundledAssemblyNameWidth} bytes long";
		var bundled_assemblies = new LlvmIrGlobalVariable (typeof(List<StructureInstance<XamarinAndroidBundledAssembly>>), "bundled_assemblies", LlvmIrVariableOptions.GlobalWritable) {
			Value = xamarinAndroidBundledAssemblies,
			Comment = $" Bundled assembly name buffers, all {bundledBuffersSize}",
		};
		module.Add (bundled_assemblies);

		// HOST_PROPERTY_RUNTIME_CONTRACT, HOST_PROPERTY_RUNTIME_IDENTIFIER and
		// HOST_PROPERTY_APP_CONTEXT_BASE_DIRECTORY will come first, in that order, our native runtime
		// requires that since it needs to set their values in the values array and we don't want to
		// spend time searching for the indices, nor we want to add yet another variable storing the
		// index to the entry. KISS.
		var runtime_property_names = new List<string> {
			HOST_PROPERTY_RUNTIME_CONTRACT,
			HOST_PROPERTY_RUNTIME_IDENTIFIER,
			HOST_PROPERTY_APP_CONTEXT_BASE_DIRECTORY,
		};
		var runtime_property_values = new List<string?> {
			null,
			null,
			null,
		};

		if (runtimeProperties != null && !CoreClrBootstrap) {
			foreach (var kvp in runtimeProperties) {
				if (MonoAndroidHelper.StringEquals (kvp.Key, HOST_PROPERTY_RUNTIME_CONTRACT) ||
						MonoAndroidHelper.StringEquals (kvp.Key, HOST_PROPERTY_RUNTIME_IDENTIFIER) ||
						MonoAndroidHelper.StringEquals (kvp.Key, HOST_PROPERTY_APP_CONTEXT_BASE_DIRECTORY)) {
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

	}

	public byte [] CreateCoreClrBootstrap ()
	{
		if (!CoreClrBootstrap || bootstrapDsoState == null || environmentVariables == null ||
			systemProperties == null || runtimeProperties == null) {
			throw new InvalidOperationException ("CoreCLR bootstrap configuration has not been constructed.");
		}

		var state = bootstrapDsoState;
		foreach (var entry in state.DsoCache) {
			if (entry.Instance == null) {
				throw new InvalidOperationException ("The CoreCLR DSO cache contains a null entry.");
			}
			entry.Instance.hash = TypeMapHelper.HashNameForCLR (entry.Instance.HashedName ?? "");
		}
		state.DsoCache.Sort ((a, b) => {
			var left = a.Instance;
			var right = b.Instance;
			if (left == null || right == null) {
				throw new InvalidOperationException ("The CoreCLR DSO cache contains a null entry.");
			}
			return left.hash.CompareTo (right.hash);
		});
		var libraries = new List<(uint Hash, bool Ignore, bool IsJniLibrary, string Name)> ();
		foreach (var entry in state.DsoCache) {
			var item = entry.Instance;
			if (item == null || item.RealName == null) {
				throw new InvalidOperationException ("The CoreCLR DSO cache contains a library without a name.");
			}
			libraries.Add ((item.hash, item.ignore, item.is_jni_library, item.RealName));
		}
		var preloads = new List<uint> ();
		foreach (var entry in state.JniPreloadDSOs) {
			int index = state.DsoCache.FindIndex (item => ReferenceEquals (item.Instance, entry));
			if (index < 0) {
				throw new InvalidOperationException ("The CoreCLR JNI preload entry was not found in the DSO cache.");
			}
			preloads.Add ((uint)index);
		}
		return CoreClrBootstrapBlob.Create (
			IgnoreSplitConfigs, HaveAssemblyStore, (uint)PackageNamingPolicy,
			(uint)NumberOfAssembliesInApk, (uint)BundledAssemblyNameWidth, (uint)NativeLibraries.Count,
			AndroidPackageName, environmentVariables, systemProperties, runtimeProperties,
			libraries, preloads, state.NameMutationsCount
		);
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
			int dsoIdx = dsoState.DsoCache.FindIndex (entry => ReferenceEquals (entry.Instance, preload));

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

		foreach (StructureInstance instance in cache) {
			if (instance.Obj == null) {
				throw new InvalidOperationException ("Internal error: DSO cache must not contain null entries");
			}

			var entry = instance.Obj as DSOCacheEntry;
			if (entry == null) {
				throw new InvalidOperationException ($"Internal error: DSO cache entry has unexpected type {instance.Obj.GetType ()}");
			}

			entry.hash = TypeMapHelper.HashNameForCLR (entry.HashedName ?? "");
		}

		cache.Sort ((StructureInstance<DSOCacheEntry> a, StructureInstance<DSOCacheEntry> b) => {
			if (a.Instance == null || b.Instance == null) return 0;
			return a.Instance.hash.CompareTo (b.Instance.hash);
		});
	}

	DsoCacheState InitDSOCache ()
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

		var dsoCache = new List<StructureInstance<DSOCacheEntry>> ();
		var jniPreloads = new List<DSOCacheEntry> ();
		var nameMutations = new List<string> ();
		var dsoNamesBlob = new LlvmIrStringBlob ();
		int nameMutationsCount = -1;
		ICollection<string> ignorePreload = MakeJniPreloadIgnoreCollection (Log, NativeLibrariesAlwaysJniPreload, NativeLibrariesNoJniPreload);

		for (int i = 0; i < dsos.Count; i++) {
			string name = dsos[i].name;

			(int nameOffset, _) = dsoNamesBlob.Add (name);

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

					hash = 0, // Hash is arch-specific, we compute it before writing
					ignore = false,
					is_jni_library = isJniLibrary,
					name_index = (uint)nameOffset,
				};

				var item = new StructureInstance<DSOCacheEntry> (dsoCacheEntryStructureInfo, entry);

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
			NamesBlob = dsoNamesBlob,
			NameMutationsCount = (uint)(nameMutationsCount <= 0 ? 1 : nameMutationsCount),
		};

		void AddNameMutations (string name)
		{
			// NOTE: The CoreCLR runtime re-derives these mutations to disambiguate CRC32 hash
			// collisions in the DSO cache (see MonodroidDl::name_is_mutation_of in
			// src/native/clr/include/runtime-base/monodroid-dl.hh). Keep the two in sync.
			nameMutations.Add (name);
			if (name.EndsWith (".dll.so", StringComparison.OrdinalIgnoreCase)) {
				string nameNoExt = Path.GetFileNameWithoutExtension (Path.GetFileNameWithoutExtension (name))!;
				nameMutations.Add (nameNoExt);
				nameMutations.Add ($"{nameNoExt}.so");
			} else {
				nameMutations.Add (Path.GetFileNameWithoutExtension (name)!);
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
