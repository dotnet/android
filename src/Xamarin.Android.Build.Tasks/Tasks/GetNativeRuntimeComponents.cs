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

	public ITaskItem[] MonoComponents { get; set; }

	[Required]
	public ITaskItem[] ResolvedNativeArchives { get; set; }

	[Required]
	public ITaskItem[] ResolvedNativeObjectFiles { get; set; }

	[Output]
	public ITaskItem[] NativeArchives { get; set; }

	[Output]
	public ITaskItem[] RequiredLibraries { get; set; }

	[Output]
	public ITaskItem[] LinkStartFiles { get; set; }

	[Output]
	public ITaskItem[] LinkEndFiles { get; set; }

	public override bool RunTask ()
	{
		var components = new NativeRuntimeComponents (MonoComponents);
		var uniqueAbis = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		var archives = new List<ITaskItem> ();

		foreach (NativeRuntimeComponents.Archive archiveItem in components.KnownArchives) {
			if (!archiveItem.Include) {
				continue;
			}
			MakeArchiveItem (archiveItem, archives, uniqueAbis);
		}
		NativeArchives = archives.ToArray ();

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

			ITaskItem newItem = DoMakeItem ("_ResolvedNativeArchive", resolvedArchive, uniqueAbis);
			newItem.SetMetadata (KnownMetadata.NativeLinkWholeArchive, archive.WholeArchive.ToString ());
			if (archive.DontExportSymbols) {
				newItem.SetMetadata (KnownMetadata.NativeDontExportSymbols, "true");
			}
			archives.Add (newItem);
		}
	}

	ITaskItem DoMakeItem (string msbuildItemName, ITaskItem sourceItem, HashSet<string> uniqueAbis)
	{
		var ret = new TaskItem (sourceItem.ItemSpec);
		string rid = sourceItem.GetRequiredMetadata (msbuildItemName, KnownMetadata.RuntimeIdentifier, Log);
		string abi = MonoAndroidHelper.RidToAbi (rid);
		uniqueAbis.Add (abi);
		ret.SetMetadata (KnownMetadata.Abi, abi);
		ret.SetMetadata (KnownMetadata.RuntimeIdentifier, rid);

		return ret;
	}
}
