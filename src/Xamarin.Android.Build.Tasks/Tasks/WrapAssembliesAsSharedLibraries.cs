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
/// In the "all assemblies are per-RID" world, assembly stores, assemblies, pdb and config are disguised as shared libraries (that is,
/// their names end with the .so extension) so that Android allows us to put them in the `lib/{ARCH}` directory.
/// </summary>
public class WrapAssembliesAsSharedLibraries : AndroidTask
{
	const string ArchiveAssembliesPath = "lib";
	const string ArchiveLibPath = "lib";

	public override string TaskPrefix => "WAS";

	[Required]
	public string AndroidBinUtilsDirectory { get; set; } = "";

	[Required]
	public ITaskItem[] RuntimePackLibraryDirectories { get; set; } = Array.Empty<ITaskItem> ();

	public bool IncludeDebugSymbols { get; set; }

	[Required]
	public string IntermediateOutputPath { get; set; } = "";

	public bool UseAssemblyStore { get; set; }

	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];

	[Required]
	public string [] SupportedAbis { get; set; } = [];

	[Output]
	public ITaskItem [] WrappedAssemblies { get; set; } = [];

	public override bool RunTask ()
	{
		var wrapper_config = DSOWrapperGenerator.GetConfig (Log, AndroidBinUtilsDirectory, RuntimePackLibraryDirectories, IntermediateOutputPath);
		var files = new PackageFileListBuilder ();

		if (UseAssemblyStore)
			WrapAssemblyStores (wrapper_config, files);
		else
			AssemblyPackagingHelper.AddAssembliesFromCollection (Log, SupportedAbis, ResolvedAssemblies, (TaskLoggingHelper log, AndroidTargetArch arch, ITaskItem assembly) => WrapAssembly (log, arch, assembly, wrapper_config, files));

		WrappedAssemblies = files.ToArray ();

		return !Log.HasLoggedErrors;
	}

	void WrapAssemblyStores (DSOWrapperGenerator.Config dsoWrapperConfig, PackageFileListBuilder files)
	{
		foreach (var store in ResolvedAssemblies) {
			var store_path = store.ItemSpec;
			var abi = store.GetRequiredMetadata ("ResolvedAssemblies", "Abi", Log);

			// An error will already have been logged in GetRequiredMetadata
			if (abi is null)
				return;

			var arch = MonoAndroidHelper.AbiToTargetArch (abi);
			var archive_path = MakeArchiveLibPath (abi, "lib" + Path.GetFileName (store_path));
			var wrapped_source_path = DSOWrapperGenerator.WrapIt (Log, dsoWrapperConfig, arch, store_path, Path.GetFileName (archive_path));

			files.AddItem (wrapped_source_path, archive_path);
		}
	}

	void WrapAssembly (TaskLoggingHelper log, AndroidTargetArch arch, ITaskItem assembly, DSOWrapperGenerator.Config dsoWrapperConfig, PackageFileListBuilder files)
	{
		// In the "all assemblies are per-RID" world, assemblies, pdb and config are disguised as shared libraries (that is,
		// their names end with the .so extension) so that Android allows us to put them in the `lib/{ARCH}` directory.
		// For this reason, they have to be treated just like other .so files, as far as compression rules are concerned.
		// Thus, we no longer just store them in the apk but we call the `GetCompressionMethod` method to find out whether
		// or not we're supposed to compress .so files.
		var sourcePath = assembly.GetMetadataOrDefault ("CompressedAssembly", assembly.ItemSpec);

		// Add assembly
		(var assemblyPath, var assemblyDirectory) = GetInArchiveAssemblyPath (assembly);
		var wrappedSourcePath = DSOWrapperGenerator.WrapIt (log, dsoWrapperConfig, arch, sourcePath, Path.GetFileName (assemblyPath));
		files.AddItem (wrappedSourcePath, assemblyPath);

		// Try to add config if exists
		var config = Path.ChangeExtension (assembly.ItemSpec, "dll.config");
		AddAssemblyConfigEntry (dsoWrapperConfig, files, arch, assemblyDirectory, config);

		// Try to add symbols if Debug
		if (!IncludeDebugSymbols) {
			return;
		}

		var symbols = Path.ChangeExtension (assembly.ItemSpec, "pdb");
		if (!File.Exists (symbols)) {
			return;
		}

		var archiveSymbolsPath = assemblyDirectory + MonoAndroidHelper.MakeDiscreteAssembliesEntryName (Path.GetFileName (symbols));
		var wrappedSymbolsPath = DSOWrapperGenerator.WrapIt (log, dsoWrapperConfig, arch, symbols, Path.GetFileName (archiveSymbolsPath));
		files.AddItem (wrappedSymbolsPath, archiveSymbolsPath);
	}

	static string MakeArchiveLibPath (string abi, string fileName) => MonoAndroidHelper.MakeZipArchivePath (ArchiveLibPath, abi, fileName);

	/// <summary>
	/// Returns the in-archive path for an assembly
	/// </summary>
	(string assemblyFilePath, string assemblyDirectoryPath) GetInArchiveAssemblyPath (ITaskItem assembly)
	{
		var parts = new List<string> ();

		// The PrepareSatelliteAssemblies task takes care of properly setting `DestinationSubDirectory`, so we can just use it here.
		var subDirectory = assembly.GetMetadata ("DestinationSubDirectory")?.Replace ('\\', '/');

		if (string.IsNullOrEmpty (subDirectory)) {
			throw new InvalidOperationException ($"Internal error: assembly '{assembly}' lacks the required `DestinationSubDirectory` metadata");
		}

		var assemblyName = Path.GetFileName (assembly.ItemSpec);
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
		var subdirParts = subDirectory!.TrimEnd ('/').Split ('/');

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

		var assemblyFilePath = MonoAndroidHelper.MakeZipArchivePath (ArchiveAssembliesPath, parts);
		return (assemblyFilePath, Path.GetDirectoryName (assemblyFilePath) + "/");
	}

	void AddAssemblyConfigEntry (DSOWrapperGenerator.Config dsoWrapperConfig, PackageFileListBuilder files, AndroidTargetArch arch, string assemblyPath, string configFile)
	{
		var inArchivePath = MonoAndroidHelper.MakeDiscreteAssembliesEntryName (assemblyPath + Path.GetFileName (configFile));

		if (!File.Exists (configFile)) {
			return;
		}

		var wrappedConfigFile = DSOWrapperGenerator.WrapIt (Log, dsoWrapperConfig, arch, configFile, Path.GetFileName (inArchivePath));

		files.AddItem (wrappedConfigFile, inArchivePath);
	}
}
