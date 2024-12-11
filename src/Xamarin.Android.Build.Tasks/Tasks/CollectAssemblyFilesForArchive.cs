#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Collects TypeMap to be added to the final archive.
/// </summary>
public class CollectAssemblyFilesForArchive : AndroidTask
{
	const string ArchiveAssembliesPath = "lib";
	const string ArchiveLibPath = "lib";

	public override string TaskPrefix => "CAF";

	[Required]
	public string AndroidBinUtilsDirectory { get; set; } = "";

	[Required]
	public string ApkOutputPath { get; set; } = "";

	[Required]
	public string AppSharedLibrariesDir { get; set; } = "";

	public bool EmbedAssemblies { get; set; }

	[Required]
	public bool EnableCompression { get; set; }

	public bool IncludeDebugSymbols { get; set; }

	[Required]
	public string IntermediateOutputPath { get; set; } = "";

	[Required]
	public string ProjectFullPath { get; set; } = "";

	[Required]
	public ITaskItem [] ResolvedFrameworkAssemblies { get; set; } = [];

	[Required]
	public ITaskItem [] ResolvedUserAssemblies { get; set; } = [];

	[Required]
	public string [] SupportedAbis { get; set; } = [];

	public bool UseAssemblyStore { get; set; }

	[Output]
	public ITaskItem [] FilesToAddToArchive { get; set; } = [];

	public override bool RunTask ()
	{
		// If we aren't embedding assemblies, we don't need to do anything
		if (!EmbedAssemblies)
			return !Log.HasLoggedErrors;

		var files = new PackageFileListBuilder ();

		DSOWrapperGenerator.Config dsoWrapperConfig = DSOWrapperGenerator.GetConfig (Log, AndroidBinUtilsDirectory, IntermediateOutputPath);
		bool compress = !IncludeDebugSymbols && EnableCompression;
		IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>? compressedAssembliesInfo = null;

		if (compress) {
			string key = CompressedAssemblyInfo.GetKey (ProjectFullPath);
			Log.LogDebugMessage ($"Retrieving assembly compression info with key '{key}'");
			compressedAssembliesInfo = BuildEngine4.UnregisterTaskObjectAssemblyLocal<IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>> (key, RegisteredTaskObjectLifetime.Build);
			if (compressedAssembliesInfo == null)
				throw new InvalidOperationException ($"Assembly compression info not found for key '{key}'. Compression will not be performed.");
		}

		AddAssemblies (dsoWrapperConfig, files, IncludeDebugSymbols, compress, compressedAssembliesInfo, assemblyStoreApkName: null);

		FilesToAddToArchive = files.ToArray ();

		return !Log.HasLoggedErrors;
	}

	void AddAssemblies (DSOWrapperGenerator.Config dsoWrapperConfig, PackageFileListBuilder files, bool debug, bool compress, IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>? compressedAssembliesInfo, string? assemblyStoreApkName)
	{
		string compressedOutputDir = Path.GetFullPath (Path.Combine (Path.GetDirectoryName (ApkOutputPath), "..", "lz4"));
		AssemblyStoreBuilder? storeBuilder = null;

		if (UseAssemblyStore) {
			storeBuilder = new AssemblyStoreBuilder (Log);
		}

		// Add user assemblies
		AssemblyPackagingHelper.AddAssembliesFromCollection (Log, SupportedAbis, ResolvedUserAssemblies, (TaskLoggingHelper log, AndroidTargetArch arch, ITaskItem assembly) => DoAddAssembliesFromArchCollection (log, arch, assembly, dsoWrapperConfig, files, debug, compress, compressedAssembliesInfo, compressedOutputDir, storeBuilder));

		// Add framework assemblies
		AssemblyPackagingHelper.AddAssembliesFromCollection (Log, SupportedAbis, ResolvedFrameworkAssemblies, (TaskLoggingHelper log, AndroidTargetArch arch, ITaskItem assembly) => DoAddAssembliesFromArchCollection (log, arch, assembly, dsoWrapperConfig, files, debug, compress, compressedAssembliesInfo, compressedOutputDir, storeBuilder));

		if (!UseAssemblyStore) {
			return;
		}

		Dictionary<AndroidTargetArch, string> assemblyStorePaths = storeBuilder!.Generate (AppSharedLibrariesDir);

		if (assemblyStorePaths.Count == 0) {
			throw new InvalidOperationException ("Assembly store generator did not generate any stores");
		}

		if (assemblyStorePaths.Count != SupportedAbis.Length) {
			throw new InvalidOperationException ("Internal error: assembly store did not generate store for each supported ABI");
		}

		string inArchivePath;
		foreach (var kvp in assemblyStorePaths) {
			string abi = MonoAndroidHelper.ArchToAbi (kvp.Key);
			inArchivePath = MakeArchiveLibPath (abi, "lib" + Path.GetFileName (kvp.Value));
			string wrappedSourcePath = DSOWrapperGenerator.WrapIt (Log, dsoWrapperConfig, kvp.Key, kvp.Value, Path.GetFileName (inArchivePath));
			files.AddItem (wrappedSourcePath, inArchivePath);
		}
	}

	void DoAddAssembliesFromArchCollection (TaskLoggingHelper log, AndroidTargetArch arch, ITaskItem assembly, DSOWrapperGenerator.Config dsoWrapperConfig, PackageFileListBuilder files, bool debug, bool compress, IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>? compressedAssembliesInfo, string compressedOutputDir, AssemblyStoreBuilder? storeBuilder)
	{
		// In the "all assemblies are per-RID" world, assemblies, pdb and config are disguised as shared libraries (that is,
		// their names end with the .so extension) so that Android allows us to put them in the `lib/{ARCH}` directory.
		// For this reason, they have to be treated just like other .so files, as far as compression rules are concerned.
		// Thus, we no longer just store them in the apk but we call the `GetCompressionMethod` method to find out whether
		// or not we're supposed to compress .so files.
		var sourcePath = CompressAssembly (assembly, compress, compressedAssembliesInfo, compressedOutputDir);
		if (UseAssemblyStore) {
			storeBuilder!.AddAssembly (sourcePath, assembly, includeDebugSymbols: debug);
			return;
		}

		// Add assembly
		(string assemblyPath, string assemblyDirectory) = GetInArchiveAssemblyPath (assembly);
		string wrappedSourcePath = DSOWrapperGenerator.WrapIt (Log, dsoWrapperConfig, arch, sourcePath, Path.GetFileName (assemblyPath));
		files.AddItem (wrappedSourcePath, assemblyPath);

		// Try to add config if exists
		var config = Path.ChangeExtension (assembly.ItemSpec, "dll.config");
		AddAssemblyConfigEntry (dsoWrapperConfig, files, arch, assemblyDirectory, config);

		// Try to add symbols if Debug
		if (!debug) {
			return;
		}

		string symbols = Path.ChangeExtension (assembly.ItemSpec, "pdb");
		if (!File.Exists (symbols)) {
			return;
		}

		string archiveSymbolsPath = assemblyDirectory + MonoAndroidHelper.MakeDiscreteAssembliesEntryName (Path.GetFileName (symbols));
		string wrappedSymbolsPath = DSOWrapperGenerator.WrapIt (Log, dsoWrapperConfig, arch, symbols, Path.GetFileName (archiveSymbolsPath));
		files.AddItem (wrappedSymbolsPath, archiveSymbolsPath);
	}

	/// <summary>
	/// Returns the in-archive path for an assembly
	/// </summary>
	(string assemblyFilePath, string assemblyDirectoryPath) GetInArchiveAssemblyPath (ITaskItem assembly)
	{
		var parts = new List<string> ();

		// The PrepareSatelliteAssemblies task takes care of properly setting `DestinationSubDirectory`, so we can just use it here.
		string? subDirectory = assembly.GetMetadata ("DestinationSubDirectory")?.Replace ('\\', '/');
		if (string.IsNullOrEmpty (subDirectory)) {
			throw new InvalidOperationException ($"Internal error: assembly '{assembly}' lacks the required `DestinationSubDirectory` metadata");
		}

		string assemblyName = Path.GetFileName (assembly.ItemSpec);
		// For discrete assembly entries we need to treat assemblies specially.
		// All of the assemblies have their names mangled so that the possibility to clash with "real" shared
		// library names is minimized. All of the assembly entries will start with a special character:
		//
		//   `_` - for regular assemblies (e.g. `_Mono.Android.dll.so`)
		//   `-` - for satellite assemblies (e.g. `-es-Mono.Android.dll.so`)
		//
		// Second of all, we need to treat satellite assemblies with even more care.
		// If we encounter one of them, we will return the culture as part of the path transformed
		// so that it forms a `-culture-` assembly file name prefix, not a `culture/` subdirectory.
		// This is necessary because Android doesn't allow subdirectories in `lib/{ABI}/`
		//
		string [] subdirParts = subDirectory!.TrimEnd ('/').Split ('/');
		if (subdirParts.Length == 1) {
			// Not a satellite assembly
			parts.Add (subDirectory);
			parts.Add (MonoAndroidHelper.MakeDiscreteAssembliesEntryName (assemblyName));
		} else if (subdirParts.Length == 2) {
			parts.Add (subdirParts [0]);
			parts.Add (MonoAndroidHelper.MakeDiscreteAssembliesEntryName (assemblyName, subdirParts [1]));
		} else {
			throw new InvalidOperationException ($"Internal error: '{assembly}' `DestinationSubDirectory` metadata has too many components ({parts.Count} instead of 1 or 2)");
		}

		string assemblyFilePath = MonoAndroidHelper.MakeZipArchivePath (ArchiveAssembliesPath, parts);
		return (assemblyFilePath, Path.GetDirectoryName (assemblyFilePath) + "/");
	}

	void AddAssemblyConfigEntry (DSOWrapperGenerator.Config dsoWrapperConfig, PackageFileListBuilder files, AndroidTargetArch arch, string assemblyPath, string configFile)
	{
		string inArchivePath = MonoAndroidHelper.MakeDiscreteAssembliesEntryName (assemblyPath + Path.GetFileName (configFile));

		if (!File.Exists (configFile)) {
			return;
		}

		string wrappedConfigFile = DSOWrapperGenerator.WrapIt (Log, dsoWrapperConfig, arch, configFile, Path.GetFileName (inArchivePath));

		files.AddItem (wrappedConfigFile, inArchivePath);
	}

	string CompressAssembly (ITaskItem assembly, bool compress, IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>? compressedAssembliesInfo, string compressedOutputDir)
	{
		if (!compress) {
			return assembly.ItemSpec;
		}

		// NRT: compressedAssembliesInfo is guaranteed to be non-null if compress is true
		return AssemblyCompression.Compress (Log, assembly, compressedAssembliesInfo!, compressedOutputDir);
	}

	static string MakeArchiveLibPath (string abi, string fileName) => MonoAndroidHelper.MakeZipArchivePath (ArchiveLibPath, abi, fileName);
}
