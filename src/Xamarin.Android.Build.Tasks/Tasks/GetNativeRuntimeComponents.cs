using System;
using System.IO;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks;

public class GetNativeRuntimeComponents : AndroidTask
{
	public override string TaskPrefix => "GNRC";

	public ITaskItem[]? MonoComponents { get; set; }

	[Required]
	public ITaskItem[] ResolvedNativeArchives { get; set; } = null!;

	[Required]
	public ITaskItem[] ResolvedNativeObjectFiles { get; set; } = null!;

	[Required]
	public string HackLocalClrRepoPath { get; set; } = "";

	[Output]
	public ITaskItem[] NativeArchives { get; set; } = null!;

	[Output]
	public ITaskItem[] RequiredLibraries { get; set; } = null!;

	[Output]
	public ITaskItem[] LinkStartFiles { get; set; } = null!;

	[Output]
	public ITaskItem[] LinkEndFiles { get; set; } = null!;

	// TODO: more research, for now it seems `--export-dynamic-symbol=name` options generated from
	//       this array don't work as expected.
	[Output]
	public ITaskItem[] NativeSymbolsToExport { get; set; } = [];

	public override bool RunTask ()
	{
		var components = new NativeRuntimeComponents (MonoComponents);
		var uniqueAbis = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		var archives = new List<ITaskItem> ();
		var symbolsToExport = new List<ITaskItem> ();

		Log.LogDebugMessage ($"Generating list of native files for linking");
		foreach (NativeRuntimeComponents.Archive archiveItem in components.KnownArchives) {
			Log.LogDebugMessage ($"  archive '{archiveItem.Name}'");
			if (!archiveItem.Include) {
				Log.LogDebugMessage ("    will not be included");
				continue;
			}
			MakeArchiveItem (archiveItem, archives, uniqueAbis);
			if (archiveItem.SymbolsToPreserve == null || archiveItem.SymbolsToPreserve.Count == 0) {
				continue;
			}

			foreach (string symbolName in archiveItem.SymbolsToPreserve) {
				MakeLibItem (symbolName, symbolsToExport, uniqueAbis);
			}
		}

		// HACK! START: until CoreCLR runtime pack has the necessary .a archives
		var discoveredItemNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		foreach (ITaskItem item in archives) {
			discoveredItemNames.Add (Path.GetFileName (item.ItemSpec));
		}

		Log.LogWarning ("[HACK] Looking for native archives which require CoreCLR hack");
		foreach (NativeRuntimeComponents.Archive archiveItem in components.KnownArchives) {
			if (!archiveItem.Include || !archiveItem.NeedsClrHack) {
				continue;
			}

			Log.LogDebugMessage ($"  [HACK] archive {archiveItem.Name}");
			if (discoveredItemNames.Contains (archiveItem.Name)) {
				Log.LogDebugMessage ("    [HACK] already found elsewhere");
				continue;
			}
			HackMakeArchiveItem (archiveItem, archives, uniqueAbis);
		}
		// HACK! END

		NativeArchives = archives.ToArray ();
		NativeSymbolsToExport = symbolsToExport.ToArray ();

		var items = new List<ITaskItem> ();
		foreach (string lib in components.NativeLibraries) {
			MakeLibItem (lib, items, uniqueAbis);
		}
		RequiredLibraries = items.ToArray ();

		items = new List<ITaskItem> ();
		foreach (string startFile in components.LinkStartFiles) {
			MakeFileItem ("_NativeLinkStartFiles", startFile, ResolvedNativeObjectFiles, items, uniqueAbis);
		}
		LinkStartFiles = items.ToArray ();

		items = new List<ITaskItem> ();
		foreach (string endFile in components.LinkEndFiles) {
			MakeFileItem ("_NativeLinkEndFiles", endFile, ResolvedNativeObjectFiles, items, uniqueAbis);
		}
		LinkEndFiles = items.ToArray ();

		return !Log.HasLoggedErrors;
	}

	void MakeLibItem (string libName, List<ITaskItem> libraries, HashSet<string> uniqueAbis)
	{
		foreach (string abi in uniqueAbis) {
			var item = new TaskItem (libName);
			item.SetMetadata (KnownMetadata.Abi, abi);
			item.SetMetadata (KnownMetadata.NativeSharedLibrary, "true");
			libraries.Add (item);
		}
	}

	void MakeFileItem (string msbuildItemName, string fileName, ITaskItem[] inputItems, List<ITaskItem> outputItems, HashSet<string> uniqueAbis)
	{
		foreach (ITaskItem item in inputItems) {
			string name = Path.GetFileName (item.ItemSpec);
			if (String.Compare (name, fileName, StringComparison.OrdinalIgnoreCase) == 0) {
				outputItems.Add (DoMakeItem (msbuildItemName, item, uniqueAbis));
			}
		}
	}

	void MakeArchiveItem (NativeRuntimeComponents.Archive archive, List<ITaskItem> archives, HashSet<string> uniqueAbis)
	{
		foreach (ITaskItem resolvedArchive in ResolvedNativeArchives) {
			string fileName = Path.GetFileName (resolvedArchive.ItemSpec);
			if (String.Compare (fileName, archive.Name, StringComparison.OrdinalIgnoreCase) != 0) {
				continue;
			}

			Log.LogDebugMessage ($"  creating msbuild item for archive '{archive.Name}'");
			ITaskItem newItem = DoMakeItem ("_ResolvedNativeArchive", resolvedArchive, uniqueAbis);
			newItem.SetMetadata (KnownMetadata.NativeLinkWholeArchive, archive.WholeArchive.ToString ());
			newItem.SetMetadata (KnownMetadata.NativeLinkItemSet, archive.SetName);
			if (archive.DontExportSymbols) {
				newItem.SetMetadata (KnownMetadata.NativeDontExportSymbols, "true");
			}
			archives.Add (newItem);
		}
	}

	ITaskItem DoMakeItem (string msbuildItemName, ITaskItem sourceItem, HashSet<string> uniqueAbis)
	{
		var ret = new TaskItem (sourceItem.ItemSpec);
		string rid = sourceItem.GetRequiredMetadata (msbuildItemName, KnownMetadata.RuntimeIdentifier, Log) ?? String.Empty;
		string abi = MonoAndroidHelper.RidToAbi (rid);
		uniqueAbis.Add (abi);
		ret.SetMetadata (KnownMetadata.Abi, abi);
		ret.SetMetadata (KnownMetadata.RuntimeIdentifier, rid);

		return ret;
	}

	void HackMakeArchiveItem (NativeRuntimeComponents.Archive archive, List<ITaskItem> archives, HashSet<string> uniqueAbis)
	{
		var relativeArtifactPaths = new List<(string path, string abi)> ();
		string archiveName = Path.GetFileName (archive.Name);
		string commonClrObjDir = Path.Combine ("artifacts", "obj", "coreclr");
		const string config = "Release";

		if (IsArchive ("libcoreclr.a")) {
			archiveName = "libcoreclr_static.a";
			MakeRelativeArtifactPaths ((string clrArch) => Path.Combine (commonClrObjDir, $"android.{clrArch}.{config}", "dlls", "mscoree", "coreclr"));
		} else if (IsArchive ("libcoreclrpal.a")) {
			MakeRelativeArtifactPaths ((string clrArch) => Path.Combine (commonClrObjDir, $"android.{clrArch}.{config}", "pal", "src"));
		} else if (IsArchive ("libminipal.a")) {
			MakeRelativeArtifactPaths ((string clrArch) => Path.Combine (commonClrObjDir, $"android.{clrArch}.{config}", "shared_minipal"));
		} else if (IsArchive ("libcoreclrminipal.a")) {
			MakeRelativeArtifactPaths ((string clrArch) => Path.Combine (commonClrObjDir, $"android.{clrArch}.{config}", "minipal", "Unix"));
		} else if (IsArchive ("libgc_pal.a")) {
			MakeRelativeArtifactPaths ((string clrArch) => Path.Combine (commonClrObjDir, $"android.{clrArch}.{config}", "gc", "unix"));
		} else if (IsArchive ("libeventprovider.a")) {
			MakeRelativeArtifactPaths ((string clrArch) => Path.Combine (commonClrObjDir, $"android.{clrArch}.{config}", "pal", "src", "eventprovider", "dummyprovider"));
		} else if (IsArchive ("libnativeresourcestring.a")) {
			MakeRelativeArtifactPaths ((string clrArch) => Path.Combine (commonClrObjDir, $"android.{clrArch}.{config}", "nativeresources"));
		} else {
			foreach (string abi in uniqueAbis) {
				string clrArch = GetClrArch (abi);
				relativeArtifactPaths.Add ((Path.Combine ("artifacts", "bin", $"microsoft.netcore.app.runtime.android-{clrArch}", config, "runtimes", $"android-{clrArch}", "native"), abi));
			}
		}

		foreach ((string relPath, string abi) in relativeArtifactPaths) {
			string filePath = Path.Combine (HackLocalClrRepoPath, relPath, archiveName);
			if (!File.Exists (filePath)) {
				Log.LogError ($"    [HACK] file {filePath} not found");
				continue;
			}
			Log.LogWarning ($"   [HACK] adding runtime component '{filePath}'");
			var tempItem = new TaskItem (filePath);
			tempItem.SetMetadata (KnownMetadata.Abi, abi);
			tempItem.SetMetadata (KnownMetadata.RuntimeIdentifier, MonoAndroidHelper.AbiToRid (abi));
			ITaskItem newItem = DoMakeItem ("_ResolvedNativeArchive", tempItem, uniqueAbis);
			newItem.SetMetadata (KnownMetadata.NativeLinkWholeArchive, archive.WholeArchive.ToString ());
			newItem.SetMetadata (KnownMetadata.NativeLinkItemSet, archive.SetName);
			if (archive.DontExportSymbols) {
				newItem.SetMetadata (KnownMetadata.NativeDontExportSymbols, "true");
			}
			archives.Add (newItem);
		}

		string GetClrArch (string abi)
		{
			return abi switch {
				"arm64-v8a" => "arm64",
				"x86_64"    => "x64",
				_ => throw new NotSupportedException ($"ABI {abi} is not supported for CoreCLR")
			};
		}

		void MakeRelativeArtifactPaths (Func<string, string> create)
		{
			foreach (string abi in uniqueAbis) {
				string clrArch = GetClrArch (abi);
				relativeArtifactPaths.Add ((create (clrArch), abi));
			}
		}

		bool IsArchive (string name) => String.Compare (name, archiveName, StringComparison.OrdinalIgnoreCase) == 0;
	}
}
