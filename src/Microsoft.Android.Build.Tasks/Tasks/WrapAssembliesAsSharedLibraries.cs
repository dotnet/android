using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Tasks;

public class WrapAssembliesAsSharedLibraries : AndroidTask
{
	public override string TaskPrefix => "WAS";

	[Required]
	public string IntermediateOutputPath { get; set; } = "";

	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];

	[Output]
	public ITaskItem [] WrappedAssemblies { get; set; } = [];

	[Output]
	public ITaskItem [] DirectoriesToDelete { get; set; } = [];

	public override bool RunTask ()
	{
		var files = new PackageFileListBuilder ();
		var directories = new HashSet<string> (StringComparer.Ordinal);
		foreach (var store in ResolvedAssemblies) {
			string? abi = store.GetRequiredMetadata ("ResolvedAssemblies", "Abi", Log);
			if (abi is null) {
				return false;
			}
			var arch = MonoAndroidHelper.AbiToTargetArch (abi);
			string name = "lib" + Path.GetFileName (store.ItemSpec);
			string library = DlopenAssemblyStoreGenerator.WrapIt (Log, IntermediateOutputPath, arch, store.ItemSpec, name);
			files.AddItem (library, MonoAndroidHelper.MakeZipArchivePath ("lib", abi, name));
			directories.Add (Path.GetDirectoryName (library) ?? throw new InvalidOperationException ("Assembly-store output directory is missing."));
		}
		WrappedAssemblies = files.ToArray ();
		DirectoriesToDelete = directories.Select (path => new TaskItem (path)).ToArray ();
		return !Log.HasLoggedErrors;
	}
}
