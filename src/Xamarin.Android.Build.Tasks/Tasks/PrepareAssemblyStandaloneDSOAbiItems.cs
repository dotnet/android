using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

public class PrepareAssemblyStandaloneDSOAbiItems : AndroidTask
{
	public override string TaskPrefix => "PASDAI";

	[Required]
	public ITaskItem[] Assemblies { get; set; }

	[Required]
	public ITaskItem[] FastPathAssemblies { get; set; }

	[Required]
	public string SharedLibraryOutputDir { get; set; }

	[Output]
	public ITaskItem[] SharedLibraries { get; set; }

	public override bool RunTask ()
	{
		SharedLibraries = PrepareItems ().ToArray ();
		return !Log.HasLoggedErrors;
	}

	List<ITaskItem> PrepareItems ()
	{
		var sharedLibraries = new List<ITaskItem> ();
		var seenAbis = new HashSet<string> (StringComparer.Ordinal);
		var satelliteAssemblies = new List<ITaskItem> ();
		ushort dsoIndexCounter = 0;
		var assemblyIndexes = new Dictionary<string, ushort> (StringComparer.OrdinalIgnoreCase);
		var fastPathItems = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		if (FastPathAssemblies.Length != 0) {
			foreach (ITaskItem item in FastPathAssemblies) {
				fastPathItems.Add (item.ItemSpec);
			}
		}

		foreach (ITaskItem assembly in Assemblies) {
			if (fastPathItems.Contains (Path.GetFileName (assembly.ItemSpec))) {
				continue;
			}

			if (MonoAndroidHelper.IsSatelliteAssembly (assembly)) {
				satelliteAssemblies.Add (assembly);
				continue;
			}

			string? abi = assembly.GetMetadata ("Abi");
			if (String.IsNullOrEmpty (abi)) {
				throw new InvalidOperationException ($"Assembly item for '{assembly.ItemSpec}' is missing ABI metadata");
			}
			seenAbis.Add (abi);

			sharedLibraries.Add (MakeTaskItem (assembly, abi, MakeBaseName (assembly)));
		}

		if (satelliteAssemblies.Count > 0) {
			foreach (ITaskItem assembly in satelliteAssemblies) {
				string culture = GetCultureName (assembly);
				string baseName = $"{culture}-{MakeBaseName (assembly)}";

				foreach (string abi in seenAbis) {
					var newItem = MakeTaskItem (assembly, abi, baseName);
					newItem.SetMetadata (DSOMetadata.SatelliteAssemblyCulture, culture);
					sharedLibraries.Add (newItem);
				}
			}
		}

		return sharedLibraries;

		ITaskItem MakeTaskItem (ITaskItem assembly, string abi, string baseName)
		{
			if (!assemblyIndexes.TryGetValue (baseName, out ushort index)) {
				index = dsoIndexCounter++;
				assemblyIndexes.Add (baseName, index);
			}

			// the 'XA' infix is to make it harder to produce library names that clash with 3rd party libraries
			// If the infix changes, the `assembly_dso_prefix` constant in src/monodroid/jni/embedded-assemblies.hh must
			// be changed as well.  Index must be encoded as a hexadecimal number, without the 0x prefix and using capital
			// letters.  If this changes then code in src/monodroid/jni/embedded-assemblies-zip.cc must be adjusted accordingly.
			string dsoName = $"libXA{baseName}.{index:X04}.so";

			var item = new TaskItem (Path.Combine (SharedLibraryOutputDir, abi, dsoName));
			item.SetMetadata (DSOMetadata.Abi, abi);
			item.SetMetadata (DSOMetadata.InputAssemblyPath, assembly.ItemSpec);
			item.SetMetadata (DSOMetadata.SourceFileBaseName, baseName);
			item.SetMetadata (DSOMetadata.AssemblyLoadInfoIndex, MonoAndroidHelper.CultureInvariantToString (index));

			string skipCompression = assembly.GetMetadata ("AndroidSkipCompression");
			if (!String.IsNullOrEmpty (skipCompression)) {
				item.SetMetadata (DSOMetadata.AndroidSkipCompression, skipCompression);
			}

			return item;
		}

		string MakeBaseName (ITaskItem assembly) => Path.GetFileNameWithoutExtension (assembly.ItemSpec);

		string GetCultureName (ITaskItem assembly)
		{
			string? culture = assembly.GetMetadata ("Culture");
			if (String.IsNullOrEmpty (culture)) {
				throw new InvalidOperationException ($"Satellite assembly '{assembly.ItemSpec}' has no culture metadata item");
			}

			string path = Path.Combine (culture, Path.GetFileName (assembly.ItemSpec));
			if (!assembly.ItemSpec.EndsWith (path, StringComparison.OrdinalIgnoreCase)) {
				throw new InvalidOperationException ($"Invalid metadata in satellite assembly '{assembly.ItemSpec}', culture metadata ('{culture}') doesn't match file path");
			}

			return culture;
		}
	}
}
