using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

//
// Optionally replaces all the CoreCLR items with corresponding ones found in a local
// directory, instead of a nuget.  This is used whenever a developer wants to quickly
// iterate over changes to CoreCLR without publishing packages.
//
public class MaybeUseLocalCLR : AndroidTask
{
	public override string TaskPrefix => "MULC";

	public string LocalClrDirectory { get; set; } = String.Empty;

	[Required]
	public ITaskItem[] ResolvedFilesToPublish { get; set; }

	[Required]
	public string AndroidRuntime { get; set; } = String.Empty;

	[Output]
	public ITaskItem[] ResolvedFilesToAdd { get; set; }

	[Output]
	public ITaskItem[] ResolvedFilesToIgnore { get; set; }

	public override bool RunTask ()
	{
		if (String.Compare ("CoreCLR", AndroidRuntime, StringComparison.OrdinalIgnoreCase) != 0) {
			Log.LogDebugMessage ("Not considering custom CLR runtime since target runtime is not CoreCLR.");
			return true;
		}

		if (String.IsNullOrEmpty (LocalClrDirectory)) {
			Log.LogDebugMessage ("Local CLR directory not specified, will not use custom CLR runtime.");
			return true;
		}

		if (!Directory.Exists (LocalClrDirectory)) {
			Log.LogWarning ($"Local CLR directory not found: ${LocalClrDirectory}");
			return true;
		}

		var itemsToRemove = new List<ITaskItem> ();
		var requiredClrItems = new List<ITaskItem> ();
		var supportedArchitectures = new HashSet<AndroidTargetArch> ();
		foreach (ITaskItem lib in ResolvedFilesToPublish) {
			ProcessItem (lib, itemsToRemove, requiredClrItems, supportedArchitectures);
		}

		var itemsToAdd = new List<ITaskItem> ();
		foreach (ITaskItem required in requiredClrItems) {
			MakeLocalPackItem (required, itemsToAdd, supportedArchitectures);
		}

		ResolvedFilesToAdd = itemsToAdd.ToArray ();
		ResolvedFilesToIgnore = itemsToRemove.ToArray ();
		return !Log.HasLoggedErrors;
	}

	bool IsNativeAsset (ITaskItem item)
	{
		string? assetType = item.GetMetadata ("AssetType");
		if (String.IsNullOrEmpty (assetType)) {
			return false;
		}

		// System.Private.CoreLib.dll is an exception - it has `AssetType` set to `runtime`, but it's actually in the `native`
		// portion of the runtime pack.
		if (String.Compare ("System.Private.CoreLib.dll", Path.GetFileName (item.ItemSpec), StringComparison.OrdinalIgnoreCase) == 0) {
			return true;
		}

		return String.Compare (assetType, "native", StringComparison.OrdinalIgnoreCase) == 0;
	}

	void MakeLocalPackItem (ITaskItem required, List<ITaskItem> itemsToAdd, HashSet<AndroidTargetArch> supportedArchitectures)
	{
		foreach (AndroidTargetArch arch in MonoAndroidHelper.SupportedTargetArchitectures) {
			if (!supportedArchitectures.Contains (arch)) {
				continue;
			}

			string rid = MonoAndroidHelper.ArchToRid (arch);
			string fileName = Path.GetFileName (required.ItemSpec);
			string basePath = Path.Combine (LocalClrDirectory, "runtimes", rid);
			string filePath;

			if (IsNativeAsset (required)) {
				filePath = Path.Combine (basePath, "native", fileName);
			} else {
				// TODO: don't hardcode framework name (`net10.0`), figure out how to compute it
				filePath = Path.Combine (basePath, "lib", "net10.0", fileName);
			}

			if (!File.Exists (filePath)) {
				Log.LogWarning ($"Local CoreCLR pack file '{filePath}' does not exist.");
				continue;
			}

			var item = new TaskItem (filePath);
			required.CopyMetadataTo (item);
			item.SetMetadata ("NuGetPackageId", $"Local.App.Runtime.CoreCLR.{rid}");
			item.SetMetadata ("NuGetPackageVersion", "0.0.0.0");

			Log.LogDebugMessage ($"Creating local CoreCLR runtime package item: {item}");
			itemsToAdd.Add (item);
		}
	}

	void ProcessItem (ITaskItem item, List<ITaskItem> itemsToRemove, List<ITaskItem> requiredClrItems, HashSet<AndroidTargetArch> supportedArchitectures)
	{
		if (LinuxBionicHack (item, itemsToRemove) || CoreClrItem (item, itemsToRemove, requiredClrItems)) {
			return;
		}

		string? rid = item.GetMetadata ("RuntimeIdentifier");
		if (String.IsNullOrEmpty (rid)) {
			return;
		}

		supportedArchitectures.Add (MonoAndroidHelper.RidToArch (rid));
	}

	bool CoreClrItem (ITaskItem item, List<ITaskItem> itemsToRemove, List<ITaskItem> requiredClrItems)
	{
		string? nugetId = item.GetMetadata ("NuGetPackageId");
		if (String.IsNullOrEmpty (nugetId)) {
			return false;
		}

		const string BionicNugetIdPrefix = "Microsoft.NETCore.App.Runtime.android-";
		if (!nugetId.StartsWith (BionicNugetIdPrefix, StringComparison.OrdinalIgnoreCase)) {
			return false;
		}

		itemsToRemove.Add (item);
		requiredClrItems.Add (item);
		return true;
	}

	bool LinuxBionicHack (ITaskItem item, List<ITaskItem> itemsToRemove)
	{
		string? nugetId = item.GetMetadata ("NuGetPackageId");
		if (String.IsNullOrEmpty (nugetId)) {
			return false;
		}

		const string BionicNugetIdPrefix = "Microsoft.NETCore.App.Runtime.linux-bionic-";
		if (!nugetId.StartsWith (BionicNugetIdPrefix, StringComparison.OrdinalIgnoreCase)) {
			return false;
		}

		Log.LogWarning ($"[MaybeUseLocalClr] HACK! HACK! Ignoring linux-bionic item {item}. Remove once SDK is updated!");
		itemsToRemove.Add (item);

		return true;
	}
}
