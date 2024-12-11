#nullable enable

using System;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Collects Dalvik classes to be added to the final archive.
/// </summary>
public class CollectDalvikFilesForArchive : AndroidTask
{
	public override string TaskPrefix => "CDF";

	public string AndroidPackageFormat { get; set; } = "";

	[Required]
	public ITaskItem [] DalvikClasses { get; set; } = [];

	[Output]
	public ITaskItem [] FilesToAddToArchive { get; set; } = [];

	public override bool RunTask ()
	{
		var dalvikPath = AndroidPackageFormat.Equals ("aab", StringComparison.InvariantCultureIgnoreCase) ? "dex/" : "";
		var files = new PackageFileListBuilder ();

		foreach (var dex in DalvikClasses) {
			var apkName = dex.GetMetadata ("ApkName");
			var dexPath = string.IsNullOrWhiteSpace (apkName) ? Path.GetFileName (dex.ItemSpec) : apkName;

			files.AddItem (dex.ItemSpec, dalvikPath + dexPath);
		}

		FilesToAddToArchive = files.ToArray ();

		return !Log.HasLoggedErrors;
	}
}
