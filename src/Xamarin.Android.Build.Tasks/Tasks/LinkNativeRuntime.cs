using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks;

public class LinkNativeRuntime : AsyncTask
{
	public override string TaskPrefix => "LNR";

	public ITaskItem[]? MonoComponents { get; set; }

	[Required]
	public string AndroidBinUtilsDirectory { get; set; } = "";

	public string? AndroidNdkDirectory { get; set; }
	public string? AndroidApiLevel { get; set; }

	[Required]
	public string IntermediateOutputPath { get; set; } = "";

	[Required]
	public ITaskItem[] LinkLibraries { get; set; } = null!;

	[Required]
	public ITaskItem[] NativeArchives { get; set; } = null!;

	[Required]
	public ITaskItem[] NativeObjectFiles { get; set; } = null!;

	[Required]
	public ITaskItem[] NativeLinkStartFiles { get; set; } = null!;

	[Required]
	public ITaskItem[] NativeLinkEndFiles { get; set; } = null!;

	[Required]
	public ITaskItem[] NativeSymbolsToExport { get; set; } = null!;

	[Required]
	public ITaskItem[] OutputRuntimes { get; set; } = null!;

	[Required]
	public ITaskItem[] SupportedAbis { get; set; } = null!;

	[Required]
	public ITaskItem[] RuntimePackLibraryDirectories { get; set; } = null!;

	public bool SaveDebugSymbols { get; set; } = true;
	public bool StripDebugSymbols { get; set; } = true;

	public override System.Threading.Tasks.Task RunTaskAsync ()
	{
		return this.WhenAll (SupportedAbis, LinkRuntime);
	}

	void LinkRuntime (ITaskItem abiItem)
	{
		string abi = abiItem.ItemSpec;
		Log.LogDebugMessage ($"LinkRuntime ({abi})");
		ITaskItem outputRuntime = GetFirstAbiItem (OutputRuntimes, "_UnifiedNativeRuntime", abi);
		string soname = Path.GetFileNameWithoutExtension (outputRuntime.ItemSpec);
		if (soname.StartsWith ("lib", StringComparison.OrdinalIgnoreCase)) {
			soname = soname.Substring (3);
		}

		var linker = new NativeLinker (Log, abi, soname, AndroidBinUtilsDirectory, IntermediateOutputPath, RuntimePackLibraryDirectories, CancellationToken, Cancel) {
			StripDebugSymbols = StripDebugSymbols,
			SaveDebugSymbols = SaveDebugSymbols,
			UseNdkLibraries = true,
			UseSymbolic = true,
			NdkRootPath = AndroidNdkDirectory,
			NdkApiLevel = AndroidApiLevel,
		};

		List<ITaskItem> items = OrganizeCommandLineItemsCLR (abi);
		bool success = linker.Link (
			outputRuntime,
			items,
			GetAbiItems (NativeLinkStartFiles, "_NativeLinkStartFiles", abi),
			GetAbiItems (NativeLinkEndFiles, "_NativeLinkEndFiles", abi),
			GetAbiItems (NativeSymbolsToExport, "_NativeSymbolsToExport", abi)
		);

		if (!success) {
			Log.LogError ($"Failed to link native runtime {outputRuntime}");
		}
	}

	// Puts object files, static archives in the correct order. This is a bit clumsy, but unfortunately necessary
	List<ITaskItem> OrganizeCommandLineItemsCLR (string abi)
	{
		// Code farther down the method does NOT check whether a set is present, it assumes that. This is on purpose, to
		// let the exception be thrown should a required (and assumed to be present) set be missing.
		var sets = new Dictionary<string, List<ITaskItem>> (StringComparer.Ordinal);
		foreach (ITaskItem item in GetAbiItems (NativeArchives, "_SelectedNativeArchive", abi)) {
			string setName = item.GetRequiredMetadata ("_SelectedNativeArchive", KnownMetadata.NativeLinkItemSet, Log) ?? String.Empty;
			if (!sets.TryGetValue (setName, out List<ITaskItem>? items)) {
				items = new List<ITaskItem> ();
				sets.Add (setName, items);
			}

			items.Add (item);
		}

		var ret = new List<ITaskItem> ();

		// First go our own object files...
		ret.AddRange (GetAbiItems (NativeObjectFiles, "_NativeAssemblyTarget", abi));

		// ...then go our runtime archives...
		ret.AddRange (sets[NativeRuntimeComponents.KnownSets.XamarinAndroidRuntime]);

		// ...followed by CoreCLR components...
		ret.AddRange (sets[NativeRuntimeComponents.KnownSets.CoreClrRuntime]);

		// ...and after them the BCL PAL libraries...
		ret.AddRange (sets[NativeRuntimeComponents.KnownSets.BCL]);

		// ...and then the C/C++ runtime libraries
		var systemLibs = new Dictionary<string, ITaskItem> (StringComparer.Ordinal);
		foreach (ITaskItem item in GetAbiItems (LinkLibraries, "_RequiredLinkLibraries", abi)) {
			systemLibs.Add (Path.GetFileName (item.ItemSpec), item);
		}

		ret.Add (systemLibs["log"]);
		ret.AddRange (sets[NativeRuntimeComponents.KnownSets.CplusPlusRuntime]);
		ret.Add (systemLibs["z"]);
		ret.Add (systemLibs["m"]);
		ret.Add (systemLibs["dl"]);
		ret.Add (systemLibs["c"]);

		return ret;
	}

	List<ITaskItem> GetAbiItems (ITaskItem[] source, string itemName, string abi)
	{
		var ret = new List<ITaskItem> ();

		foreach (ITaskItem item in source) {
			if (AbiMatches (abi, item, itemName)) {
				ret.Add (item);
			}
		}

		return ret;
	}

	ITaskItem GetFirstAbiItem (ITaskItem[] source, string itemName, string abi)
	{
		foreach (ITaskItem item in source) {
			if (AbiMatches (abi, item, itemName)) {
				return item;
			}
		}

		throw new InvalidOperationException ($"Internal error: item '{itemName}' for ABI '{abi}' not found");
	}

	bool AbiMatches (string abi, ITaskItem item, string itemName)
	{
		return String.Compare (abi, item.GetRequiredMetadata (itemName, "Abi", Log), StringComparison.OrdinalIgnoreCase) == 0;
	}
}
