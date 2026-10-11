#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

sealed class CoreClrBootstrapBuilder
{
	sealed class DsoCacheEntry
	{
		public string HashedName = "";
		public string RealName = "";
		public uint Hash;
		public bool IsJniLibrary;
	}

	sealed class DsoCacheState
	{
		public List<DsoCacheEntry> Entries = [];
		public List<List<DsoCacheEntry>> JniPreloadGroups = [];
		public int PreloadStride = 1;
	}

	readonly SortedDictionary<string, string> environmentVariables;
	readonly SortedDictionary<string, string> systemProperties;
	readonly SortedDictionary<string, string> runtimeProperties;
	readonly TaskLoggingHelper log;

	public string AndroidPackageName { get; set; } = "";
	public int NumberOfAssembliesInApk { get; set; }
	public int BundledAssemblyNameWidth { get; set; }
	public PackageNamingPolicy PackageNamingPolicy { get; set; }
	public List<ITaskItem> NativeLibraries { get; set; } = [];
	public ICollection<ITaskItem>? NativeLibrariesNoJniPreload { get; set; }
	public ICollection<ITaskItem>? NativeLibrariesAlwaysJniPreload { get; set; }
	public bool IgnoreSplitConfigs { get; set; }
	public bool HaveAssemblyStore { get; set; }

	public CoreClrBootstrapBuilder (IDictionary<string, string> environmentVariables, IDictionary<string, string> systemProperties,
		IDictionary<string, string>? runtimeProperties, TaskLoggingHelper log)
	{
		this.environmentVariables = new SortedDictionary<string, string> (environmentVariables, StringComparer.Ordinal);
		this.systemProperties = new SortedDictionary<string, string> (systemProperties, StringComparer.Ordinal);
		this.runtimeProperties = runtimeProperties == null
			? new SortedDictionary<string, string> (StringComparer.Ordinal)
			: new SortedDictionary<string, string> (runtimeProperties, StringComparer.Ordinal);
		this.log = log;

		// The host fills these slots before CLR initialization. Other host callbacks must not
		// be supplied by runtimeconfig.json.
		this.runtimeProperties ["HOST_RUNTIME_CONTRACT"] = "";
		this.runtimeProperties ["RUNTIME_IDENTIFIER"] = "";
		this.runtimeProperties ["APP_CONTEXT_BASE_DIRECTORY"] = "";
		this.runtimeProperties.Remove ("PINVOKE_OVERRIDE");
		this.runtimeProperties.Remove ("BUNDLE_PROBE");
	}

	public byte [] Create ()
	{
		var state = CreateDsoCache ();
		foreach (var entry in state.Entries) {
			entry.Hash = TypeMapHelper.HashNameForCLR (entry.HashedName);
		}
		state.Entries.Sort ((a, b) => a.Hash.CompareTo (b.Hash));

		var libraries = new List<(uint Hash, bool Ignore, bool IsJniLibrary, string Name)> ();
		foreach (var entry in state.Entries) {
			libraries.Add ((entry.Hash, false, entry.IsJniLibrary, entry.RealName));
		}
		var preloads = new List<uint> ();
		foreach (var group in state.JniPreloadGroups) {
			// XCFG uses one stride. Repeat this group's first index for padding so the
			// runtime cannot assign its library handle to another DSO's aliases.
			for (int i = 0; i < state.PreloadStride; i++) {
				var entry = group [i < group.Count ? i : 0];
				int index = state.Entries.IndexOf (entry);
				if (index < 0) {
					throw new InvalidOperationException ("The CoreCLR JNI preload entry was not found in the DSO cache.");
				}
				preloads.Add ((uint)index);
			}
		}
		return CoreClrBootstrapBlob.Create (
			IgnoreSplitConfigs, HaveAssemblyStore, (uint)PackageNamingPolicy,
			(uint)NumberOfAssembliesInApk, (uint)BundledAssemblyNameWidth, (uint)NativeLibraries.Count,
			AndroidPackageName, environmentVariables, systemProperties, runtimeProperties,
			libraries, preloads, (uint)state.PreloadStride
		);
	}

	DsoCacheState CreateDsoCache ()
	{
		var dsos = new List<(string name, ITaskItem item)> ();
		foreach (ITaskItem item in NativeLibraries) {
			string name = item.GetMetadata ("ArchiveFileName");
			if (String.IsNullOrEmpty (name)) {
				name = item.ItemSpec;
			}
			name = Path.GetFileName (name);
			dsos.Add ((name, item));
		}

		var state = new DsoCacheState ();
		var nameMutations = new List<string> ();
		ICollection<string> ignorePreload = MakeJniPreloadIgnoreCollection (log, NativeLibrariesAlwaysJniPreload, NativeLibrariesNoJniPreload);
		foreach (var dso in dsos) {
			bool isJniLibrary = ELFHelper.IsJniLibrary (log, dso.item.ItemSpec);
			bool ignoreForPreload = ShouldIgnoreForJniPreload (log, ignorePreload, dso.item);
			nameMutations.Clear ();
			AddNameMutations (dso.name);
			var preloadGroup = new List<DsoCacheEntry> ();

			foreach (string entryName in nameMutations) {
				var entry = new DsoCacheEntry {
					HashedName = entryName,
					RealName = dso.name,
					IsJniLibrary = isJniLibrary,
				};
				if (isJniLibrary && !ignoreForPreload) {
					preloadGroup.Add (entry);
				}
				state.Entries.Add (entry);
			}
			if (preloadGroup.Count > 0) {
				state.JniPreloadGroups.Add (preloadGroup);
				state.PreloadStride = Math.Max (state.PreloadStride, preloadGroup.Count);
			}
		}
		return state;

		void AddNameMutations (string name)
		{
			// Keep in sync with MonodroidDl::name_is_mutation_of, which disambiguates CRC32 collisions.
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

	static bool ShouldIgnoreForJniPreload (TaskLoggingHelper log, ICollection<string> libsToIgnore, ITaskItem libItem)
	{
		if (libsToIgnore.Count == 0) {
			return false;
		}
		string? libFileName = GetFileName (log, libItem);
		if (libFileName == null) {
			return false;
		}
		return libsToIgnore.Contains (libFileName);
	}

	static ICollection<string> MakeJniPreloadIgnoreCollection (TaskLoggingHelper log, ICollection<ITaskItem>? alwaysPreload, ICollection<ITaskItem>? ignorePreload)
	{
		var libsToIgnore = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		if (ignorePreload == null || ignorePreload.Count == 0) {
			return libsToIgnore;
		}
		var neverIgnore = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		if (alwaysPreload != null) {
			foreach (ITaskItem item in alwaysPreload) {
				string? fileName = GetFileName (log, item);
				if (fileName != null) {
					neverIgnore.Add (fileName);
				}
			}
		}
		foreach (ITaskItem item in ignorePreload) {
			string? fileName = GetFileName (log, item);
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
