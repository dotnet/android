#nullable enable
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

	public override bool RunTask ()
	{
		// We need to include any special assemblies in the Assemblies list
		var mainFileName = Path.GetFileName (MainAssembly);

		using (var pkgmgr = MemoryStreamPool.Shared.CreateStreamWriter ()) {
			pkgmgr.WriteLine ("package mono;");

			// Write all the user assemblies
			pkgmgr.WriteLine ("public class MonoPackageManager_Resources {");
			pkgmgr.WriteLine ("\tpublic static String[] Assemblies = new String[]{");

			pkgmgr.WriteLine ("\t\t/* \"{0}\" should be the only entry in this list. */", mainFileName);
			pkgmgr.WriteLine ("\t\t\"" + mainFileName + "\",");
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
