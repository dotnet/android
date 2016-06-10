using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Mono.Cecil;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;

using Java.Interop.Tools.Cecil;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class ResolveLibraryProjectImports : Task
	{
		[Required]
		public string ImportsDirectory { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		[Required]
		public string OutputImportDirectory { get; set; }

		[Required]
		public ITaskItem[] Assemblies { get; set; }

		[Required]
		public bool UseShortFileNames { get; set; }

		public string CacheFile { get; set;} 

		[Output]
		public string [] Jars { get; set; }
		
		[Output]
		public string [] ResolvedAssetDirectories { get; set; }

		[Output]
		public ITaskItem [] ResolvedResourceDirectories { get; set; }

		[Output]
		public string [] ResolvedEnvironmentFiles { get; set; }

		[Output]
		public ITaskItem [] ResolvedResourceDirectoryStamps { get; set; }

		string        imports_dir = "library_project_imports";

		public ResolveLibraryProjectImports ()
		{
		}

		// Extracts library project contents under e.g. obj/Debug/[__library_projects__/*.jar | res/*/*]
		public override bool Execute ()
		{
			Log.LogDebugMessage ("ResolveLibraryProjectImports Task");
			Log.LogDebugMessage ("  ImportsDirectory: {0}", ImportsDirectory);
			Log.LogDebugMessage ("  OutputDirectory: {0}", OutputDirectory);
			Log.LogDebugMessage ("  OutputImportDirectory: {0}", OutputImportDirectory);
			Log.LogDebugMessage ("  UseShortFileNames: {0}", UseShortFileNames);
			Log.LogDebugTaskItems ("  Assemblies: ", Assemblies);

			var jars                          = new List<string> ();
			var resolvedResourceDirectories   = new List<string> ();
			var resolvedAssetDirectories      = new List<string> ();
			var resolvedEnvironmentFiles      = new List<string> ();
			Extract (jars, resolvedResourceDirectories, resolvedAssetDirectories, resolvedEnvironmentFiles);

			Jars                        = jars.ToArray ();
			ResolvedResourceDirectories = resolvedResourceDirectories
				.Select (s => new TaskItem (Path.GetFullPath (s)))
				.ToArray ();
			ResolvedAssetDirectories    = resolvedAssetDirectories.ToArray ();
			ResolvedEnvironmentFiles    = resolvedEnvironmentFiles.ToArray ();

			ResolvedResourceDirectoryStamps = ResolvedResourceDirectories
				.Select (s => new TaskItem (Path.GetFullPath (Path.Combine (s.ItemSpec, "../..")) + ".stamp"))
				.ToArray ();

			foreach (var directory in ResolvedResourceDirectories) {
				MonoAndroidHelper.SetDirectoryWriteable (directory.ItemSpec);
			}

			foreach (var directory in ResolvedAssetDirectories) {
				MonoAndroidHelper.SetDirectoryWriteable (directory);
			}

			if (!string.IsNullOrEmpty (CacheFile)) {
				var document = new XDocument (
					new XDeclaration ("1.0", "UTF-8", null),
					new XElement ("Paths",
						new XElement ("Jars", string.Join (";", Jars)),
						new XElement ("ResolvedResourceDirectories",
							ResolvedResourceDirectories.Select(e => new XElement ("ResolvedResourceDirectory", e))),
						new XElement ("ResolvedAssetDirectories", 
							ResolvedAssetDirectories.Select(e => new XElement ("ResolvedAssetDirectory", e))),
						new XElement ("ResolvedEnvironmentFiles", 
							ResolvedEnvironmentFiles.Select(e => new XElement ("ResolvedEnvironmentFile", e))),
						new XElement ("ResolvedResourceDirectoryStamps",
							ResolvedResourceDirectoryStamps.Select(e => new XElement ("ResolvedResourceDirectoryStamp", e)))
					));
				document.Save (CacheFile);
			}

			Log.LogDebugTaskItems ("  Jars: ", Jars.Select (s => new TaskItem (s)).ToArray ());
			Log.LogDebugTaskItems ("  ResolvedResourceDirectories: ", ResolvedResourceDirectories.Select (s => new TaskItem (s)).ToArray ());
			Log.LogDebugTaskItems ("  ResolvedAssetDirectories: ", ResolvedAssetDirectories.Select (s => new TaskItem (s)).ToArray ());
			Log.LogDebugTaskItems ("  ResolvedEnvironmentFiles: ", ResolvedEnvironmentFiles.Select (s => new TaskItem (s)).ToArray ());
			Log.LogDebugTaskItems ("  ResolvedResourceDirectoryStamps: ", ResolvedResourceDirectoryStamps);

			return !Log.HasLoggedErrors;
		}

		static string GetTargetAssembly (ITaskItem assemblyName)
		{
			var suffix = assemblyName.ItemSpec.EndsWith (".dll") ? String.Empty : ".dll";
			string hintPath = assemblyName.GetMetadata ("HintPath").Replace (Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
			string fileName = assemblyName.ItemSpec + suffix;
			if (!String.IsNullOrEmpty (hintPath) && !File.Exists (hintPath))
				hintPath = null;
			string assemblyPath = String.IsNullOrEmpty (hintPath) ? fileName : hintPath;
			if (MonoAndroidHelper.IsFrameworkAssembly (fileName) && !MonoAndroidHelper.FrameworkEmbeddedJarLookupTargets.Contains (Path.GetFileName (fileName)))
				return null;
			return Path.GetFullPath (assemblyPath);
		}

		// Extracts library project contents under e.g. obj/Debug/[__library_projects__/*.jar | res/*/*]
		void Extract (
				ICollection<string> jars,
				ICollection<string> resolvedResourceDirectories,
				ICollection<string> resolvedAssetDirectories,
				ICollection<string> resolvedEnvironments)
		{
			var outdir = new DirectoryInfo (OutputImportDirectory);
			if (!outdir.Exists)
				outdir.Create ();

			var res = new DirectoryAssemblyResolver (Log.LogWarning, loadDebugSymbols: false);
			foreach (var assembly in Assemblies)
				res.Load (assembly.ItemSpec);

			// FIXME: reorder references by import priority (not sure how to do that yet)
			foreach (var assemblyPath in Assemblies
					.Select (a => GetTargetAssembly (a))
					.Where (a => a != null)
					.Distinct ()) {
				foreach (var imp in new string [] {imports_dir, "library_project_imports"}.Distinct ()) {
					string assemblyIdentName = Path.GetFileNameWithoutExtension (assemblyPath);
					if (UseShortFileNames) {
						assemblyIdentName = Xamarin.Android.Tasks.MonoAndroidHelper.GetLibraryImportDirectoryNameForAssembly (assemblyIdentName);
					}
					string outDirForDll = Path.Combine (OutputImportDirectory, assemblyIdentName);
					string importsDir = Path.Combine (outDirForDll, imp);
#if SEPARATE_CRUNCH
					// FIXME: review these binResDir thing and enable this. Eclipse does this.
					// Enabling these blindly causes build failure on ActionBarSherlock.
					//string binResDir = Path.Combine (importsDir, "bin", "res");
					//string binAssemblyDir = Path.Combine (importsDir, "bin", "assets");
#endif
					string resDir = Path.Combine (importsDir, "res");
					string assemblyDir = Path.Combine (importsDir, "assets");

					// Skip already-extracted resources.
					var stamp = new FileInfo (Path.Combine (outdir.FullName, assemblyIdentName + ".stamp"));
					if (stamp.Exists && stamp.LastWriteTime > new FileInfo (assemblyPath).LastWriteTime) {
						Log.LogDebugMessage ("Skipped resource lookup for {0}: extracted files are up to date", assemblyPath);
#if SEPARATE_CRUNCH
						// FIXME: review these binResDir/binAssemblyDir thing and enable this. Eclipse does this.
						// Enabling these blindly causes build failure on ActionBarSherlock.
						if (Directory.Exists (binResDir))
							resolvedResourceDirectories.Add (binResDir);
						if (Directory.Exists (binAssemblyDir))
							resolvedAssetDirectories.Add (binAssemblyDir);
#endif
						if (Directory.Exists (resDir))
							resolvedResourceDirectories.Add (resDir);
						if (Directory.Exists (assemblyDir))
							resolvedAssetDirectories.Add (assemblyDir);
						continue;
					}

					if (Directory.Exists (outDirForDll))
						Directory.Delete (outDirForDll, true);

					Directory.CreateDirectory (importsDir);

					var assembly = res.GetAssembly (assemblyPath);

					foreach (var mod in assembly.Modules) {
						// android environment files
						foreach (var envtxt in mod.Resources
								.Where (r => r.Name.StartsWith ("__AndroidEnvironment__", StringComparison.OrdinalIgnoreCase))
								.Where (r => r is EmbeddedResource)
								.Cast<EmbeddedResource> ()) {
							if (!Directory.Exists (outDirForDll))
								Directory.CreateDirectory (outDirForDll);
							var finfo = new FileInfo (Path.Combine (outDirForDll, envtxt.Name));
							using (var fs = finfo.Create ()) {
								var data = envtxt.GetResourceData ();
								fs.Write (data, 0, data.Length);
							}
							resolvedEnvironments.Add (finfo.FullName);
						}

						// embedded jars (EmbeddedJar, EmbeddedReferenceJar)
						var resjars = mod.Resources
							.Where (r => r.Name.EndsWith (".jar", StringComparison.InvariantCultureIgnoreCase))
							.Select (r => (EmbeddedResource) r);
						foreach (var resjar in resjars) {
							var data = resjar.GetResourceData ();
							using (var outfs = File.Create (Path.Combine (importsDir, resjar.Name)))
								outfs.Write (data, 0, data.Length);
						}

						// embedded AndroidResourceLibrary archive
						var reszip = mod.Resources.FirstOrDefault (r => r.Name == "__AndroidLibraryProjects__.zip") as EmbeddedResource;
						if (reszip != null) {
							if (!Directory.Exists (outDirForDll))
								Directory.CreateDirectory (outDirForDll);
							var finfo = new FileInfo (Path.Combine (outDirForDll, reszip.Name));
							using (var fs = finfo.Create ()) {
								var data = reszip.GetResourceData ();
								fs.Write (data, 0, data.Length);
							}

							// temporarily extracted directory will look like:
							//    __library_projects__/[dllname]/[library_project_imports | jlibs]/bin
							using (var zip = MonoAndroidHelper.ReadZipFile (finfo.FullName))
								Files.ExtractAll (zip, outDirForDll);

							// We used to *copy* the resources to overwrite other resources,
							// which resulted in missing resource issue.
							// Here we replaced copy with use of '-S' option and made it to work.
#if SEPARATE_CRUNCH
							// FIXME: review these binResDir/binAssemblyDir thing and enable this. Eclipse does this.
							// Enabling these blindly causes build failure on ActionBarSherlock.
							if (Directory.Exists (binResDir))
								resolvedResourceDirectories.Add (binResDir);
							if (Directory.Exists (binAssemblyDir))
								resolvedAssetDirectories.Add (binAssemblyDir);
#endif
							if (Directory.Exists (resDir))
								resolvedResourceDirectories.Add (resDir);
							if (Directory.Exists (assemblyDir))
								resolvedAssetDirectories.Add (assemblyDir);

							finfo.Delete ();
						}
					}

					stamp.Create ().Close ();
				}
			}

			foreach (var f in outdir.GetFiles ("*.jar")
					.Select (fi => fi.FullName))
				jars.Add (f);
		}
	}
}
