using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

public class MaybeUseLocalCLR : AndroidTask
{
	//
	// Lists of items below must together create a full CoreCLR
	// runtime pack.  All components must be listed.  This is just
	// a temporary measure until there exist CoreCLR packs.
	// When we have the packs, we will simply make sure that all the
	// required CoreCLR pack items found in ResolvedFilesToPublish are
	// also in our local directory.
	//

	// Full file names, no path.
	static readonly string[] RequiredClrComponents = [
		"System.Private.CoreLib.dll",
		"libSystem.Security.Cryptography.Native.Android.dex",
		"libSystem.Security.Cryptography.Native.Android.jar",
	];

	// Just stems, without extension. Extensions we'll check for are:
	static readonly string[] RequiredClrLibraries = [
		"libSystem.IO.Compression.Native",
		"libSystem.Globalization.Native",
		"libSystem.IO.Ports.Native",
		"libSystem.Native",
		"libSystem.Security.Cryptography.Native.Android",
		"libcoreclr",
	];

	static readonly string[] ClrLibraryExtensions = [
		".so",
		".so.dbg",
		".a",
	];

	public override string TaskPrefix => "MULC";

	public string LocalClrDirectory { get; set; } = String.Empty;

	[Required]
	public ITaskItem[] ResolvedFilesToPublish { get; set; }

	[Required]
	public string AndroidRuntime { get; set; } = String.Empty;

	[Output]
	public ITaskItem[] SharedLibrariesToAdd { get; set; }

	[Output]
	public ITaskItem[] SharedLibrariesToIgnore { get; set; }

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
		var supportedArchitectures = new HashSet<AndroidTargetArch> ();
		foreach (ITaskItem lib in ResolvedFilesToPublish) {
			ProcessItem (lib, itemsToRemove, supportedArchitectures);
		}

		var itemsToAdd = new List<ITaskItem> ();
		foreach (string required in RequiredClrComponents) {
			MakeLocalPackItem (required, itemsToAdd, supportedArchitectures);
		}

		foreach (string required in RequiredClrLibraries) {
			foreach (string ext in ClrLibraryExtensions) {
				MakeLocalPackItem ($"{required}{ext}", itemsToAdd, supportedArchitectures);
			}
		}

		SharedLibrariesToAdd = itemsToAdd.ToArray ();
		SharedLibrariesToIgnore = itemsToRemove.ToArray ();
		return !Log.HasLoggedErrors;
	}

	void MakeLocalPackItem (string required, List<ITaskItem> itemsToAdd, HashSet<AndroidTargetArch> supportedArchitectures)
	{
		foreach (AndroidTargetArch arch in MonoAndroidHelper.SupportedTargetArchitectures) {
			if (!supportedArchitectures.Contains (arch)) {
				continue;
			}

			string rid = MonoAndroidHelper.ArchToRid (arch);
			string filePath = Path.Combine (LocalClrDirectory, "runtimes", rid, "native", required);

			if (!File.Exists (filePath)) {
				Log.LogWarning ($"Local CoreCLR pack file '{filePath}' does not exist.");
				continue;
			}

			string fileName = Path.GetFileName (filePath);
			var item = new TaskItem (filePath);
			item.SetMetadata ("AssetType", "native");
			item.SetMetadata ("CopyLocal", "true");
			item.SetMetadata ("CopyToPublishDirectory", "PreserveNewest");
			item.SetMetadata ("DestinationSubPath", fileName);
			item.SetMetadata ("DropFromSingleFile", "true");
			item.SetMetadata ("NuGetPackageId", $"Local.App.Runtime.CoreCLR.{rid}");
			item.SetMetadata ("NuGetPackageVersion", "0.0.0.0");
			item.SetMetadata ("RelativePath", fileName);
			item.SetMetadata ("RuntimeIdentifier", rid);

			Log.LogDebugMessage ($"Creating local CoreCLR runtime package item: {item}");
			itemsToAdd.Add (item);
		}
	}

	void ProcessItem (ITaskItem item, List<ITaskItem> itemsToRemove, HashSet<AndroidTargetArch> supportedArchitectures)
	{
		if (LinuxBionicHack (item, itemsToRemove) || CoreClrItem (item, itemsToRemove)) {
			return;
		}

		string? rid = item.GetMetadata ("RuntimeIdentifier");
		if (String.IsNullOrEmpty (rid)) {
			return;
		}

		supportedArchitectures.Add (MonoAndroidHelper.RidToArch (rid));
	}

	bool CoreClrItem (ITaskItem item, List<ITaskItem> itemsToRemove)
	{
		// TODO: implement once CoreCLR runtime packs are available
		return false;
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
