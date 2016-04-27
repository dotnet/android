using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Mono.Cecil;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using Ionic.Zip;

using Java.Interop.Tools.Cecil;

namespace Xamarin.Android.Tasks
{
	public class StripEmbeddedLibraries : Task
	{
		[Required]
		public ITaskItem[] Assemblies { get; set; }

		public StripEmbeddedLibraries ()
		{
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("StripEmbeddedLibraries Task");
			Log.LogDebugTaskItems ("  Assemblies: ", Assemblies);

			var res = new DirectoryAssemblyResolver (Log.LogWarning, true);
			foreach (var assembly in Assemblies)
				res.Load (Path.GetFullPath (assembly.ItemSpec));

			foreach (var assemblyName in Assemblies) {
				var suffix = assemblyName.ItemSpec.EndsWith (".dll") ? String.Empty : ".dll";
				string hintPath = assemblyName.GetMetadata ("HintPath").Replace (Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
				string fileName = assemblyName.ItemSpec + suffix;
				if (!String.IsNullOrEmpty (hintPath) && !File.Exists (hintPath)) // ignore invalid HintPath
					hintPath = null;
				string assemblyPath = String.IsNullOrEmpty (hintPath) ? fileName : hintPath;
				if (MonoAndroidHelper.IsFrameworkAssembly (fileName) && !MonoAndroidHelper.FrameworkEmbeddedJarLookupTargets.Contains (Path.GetFileName (fileName)))
					continue;

				var assembly = res.GetAssembly (assemblyPath);
				bool assembly_modified = false;
				foreach (var mod in assembly.Modules) {
					// embedded jars
					var resjars = mod.Resources.Where (r => r.Name.EndsWith (".jar", StringComparison.InvariantCultureIgnoreCase)).Select (r => (EmbeddedResource) r);
					foreach (var resjar in resjars.ToArray ()) {
						Log.LogDebugMessage ("    Stripped {0}", resjar.Name);
						mod.Resources.Remove (resjar);
						assembly_modified = true;
					}
					// embedded AndroidNativeLibrary archive
					var nativezip = mod.Resources.FirstOrDefault (r => r.Name == "__AndroidNativeLibraries__.zip") as EmbeddedResource;
					if (nativezip != null) {
						Log.LogDebugMessage ("    Stripped {0}", nativezip.Name);
						mod.Resources.Remove (nativezip);
						assembly_modified = true;
					}
					// embedded AndroidResourceLibrary archive
					var reszip = mod.Resources.FirstOrDefault (r => r.Name == "__AndroidLibraryProjects__.zip") as EmbeddedResource;
					if (reszip != null) {
						Log.LogDebugMessage ("    Stripped {0}", reszip.Name);
						mod.Resources.Remove (reszip);
						assembly_modified = true;
					}
				}
				if (assembly_modified) {
					Log.LogDebugMessage ("    The stripped library is saved as {0}", assemblyPath);

					// Output assembly needs to regenerate symbol file even if no IL/metadata was touched
					// because Cecil still rewrites all assembly types in Cecil order (type A, nested types of A, type B, etc)
					// and not in the original order causing symbols if original order doesn't match Cecil order
					var wp = new WriterParameters () {
						WriteSymbols = assembly.MainModule.HasSymbols
					};

					assembly.Write (assemblyPath, wp);
				}
			}
			return true;
		}
	}
}

