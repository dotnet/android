using System;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

public class GeneratePackageManagerJava : AndroidTask
{
	public override string TaskPrefix => "GPM";

	[Required]
	public string MainAssembly { get; set; } = "";

	[Required]
	public string OutputDirectory { get; set; } = "";

	[Required]
	public ITaskItem [] ResolvedUserAssemblies { get; set; } = [];

	public override bool RunTask ()
	{
		// We need to include any special assemblies in the Assemblies list
		var mainFileName = Path.GetFileName (MainAssembly);

		using (var pkgmgr = MemoryStreamPool.Shared.CreateStreamWriter ()) {
			pkgmgr.WriteLine ("package mono;");

			// Write all the user assemblies
			pkgmgr.WriteLine ("public class MonoPackageManager_Resources {");
			pkgmgr.WriteLine ("\tpublic static String[] Assemblies = new String[]{");
			pkgmgr.WriteLine ("\t\t/* We need to ensure that \"{0}\" comes first in this list. */", mainFileName);
			pkgmgr.WriteLine ("\t\t\"" + mainFileName + "\",");
			foreach (var assembly in ResolvedUserAssemblies) {
				if (string.Compare (Path.GetFileName (assembly.ItemSpec), mainFileName, StringComparison.OrdinalIgnoreCase) == 0)
					continue;
				pkgmgr.WriteLine ("\t\t\"" + Path.GetFileName (assembly.ItemSpec) + "\",");
			}

			// Write the assembly dependencies
			pkgmgr.WriteLine ("\t};");
			pkgmgr.WriteLine ("\tpublic static String[] Dependencies = new String[]{");

			//foreach (var assembly in assemblies.Except (args.Assemblies)) {
			//        if (args.SharedRuntime && !Toolbox.IsInSharedRuntime (assembly))
			//                pkgmgr.WriteLine ("\t\t\"" + Path.GetFileName (assembly) + "\",");
			//}

			pkgmgr.WriteLine ("\t};");

			pkgmgr.WriteLine ("}");
			pkgmgr.Flush ();

			// Only copy to the real location if the contents actually changed
			var dest = Path.GetFullPath (Path.Combine (OutputDirectory, "MonoPackageManager_Resources.java"));
			Files.CopyIfStreamChanged (pkgmgr.BaseStream, dest);
		}

		return !Log.HasLoggedErrors;
	}
}
