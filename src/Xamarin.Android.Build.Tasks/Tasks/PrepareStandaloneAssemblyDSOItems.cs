using System;
using System.IO;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks;

public class PrepareStandaloneAssemblyDSOItems : AndroidTask
{
	public override string TaskPrefix => "PSADI";

	[Required]
	public ITaskItem[] Assemblies { get; set; }

	[Required]
	public string[] SupportedAbis { get; set; }

	[Required]
	public string NativeSourcesDir { get; set; }

	[Required]
	public ITaskItem[] FastPathAssemblies { get; set; }

	[Output]
	public ITaskItem[] AssemblySources { get; set; }

	public override bool RunTask ()
	{
		var fastAssemblies = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		foreach (ITaskItem asm in FastPathAssemblies) {
			fastAssemblies.Add (Path.GetFileName (asm.ItemSpec));
		}

		var seenAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		var sources = new List<ITaskItem> ();
		foreach (ITaskItem asm in Assemblies) {
			string asmName = Path.GetFileName (asm.ItemSpec);
			if (fastAssemblies.Contains (asmName) || seenAssemblyNames.Contains (asmName)) {
				continue;
			}
			seenAssemblyNames.Add (asmName);

			string baseName = Path.GetFileNameWithoutExtension (asmName);
			foreach (string abi in SupportedAbis) {
				var item = new TaskItem (Path.Combine (NativeSourcesDir, MonoAndroidHelper.MakeNativeAssemblyFileName (baseName, abi)));
				item.SetMetadata ("abi", abi);

				sources.Add (item);
			}
		}

		AssemblySources = sources.ToArray ();
		return !Log.HasLoggedErrors;
	}
}
