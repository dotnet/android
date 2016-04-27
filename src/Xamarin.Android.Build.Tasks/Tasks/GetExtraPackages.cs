using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class GetExtraPackages : Task
	{
		[Required]
		public string IntermediateOutputPath { get; set; }

		[Output]
		public string ExtraPackages { get; set; }

		[Required]
		public string LibraryProjectImportsDirectoryName { get; set; }

		public override bool Execute ()
		{
			var extraPackages = new List<string> ();
			var libProjects = Path.Combine (IntermediateOutputPath, "__library_projects__");
			if (Directory.Exists (libProjects)) {
				foreach (var assemblyDir in Directory.GetDirectories (libProjects)) {
					foreach (var importBaseDir in new string [] { LibraryProjectImportsDirectoryName, "library_project_imports", }) {
						string importsDir = Path.Combine (assemblyDir, importBaseDir);
						string libpkg = GetPackageNameForLibrary (importsDir, Path.GetDirectoryName (assemblyDir));
						if (libpkg != null)
							extraPackages.Add (libpkg);
					}
				}
			}

			ExtraPackages = String.Join (":", extraPackages.Distinct ().ToArray ());

			Log.LogDebugMessage ("CreateTemporaryDirectory Task");
			Log.LogDebugMessage ("  OUTPUT: ExtraPackages: {0}", ExtraPackages);

			return true;
		}

		string GetPackageNameForLibrary (string path, string assemblyName)
		{
			// It looks for:
			// 1) bin/AndroidManifest.xml
			// 2) AndroidManifest.xml
			// and then tries to get /manifest/@package value
			// if not found, return null.
			
			string manifest = "AndroidManifest.xml";
			foreach (var file in new string [] {Path.Combine (path, "bin", manifest), Path.Combine (path, manifest)}) {
				try {
					if (File.Exists (file))
						return XDocument.Load (file).Element (XName.Get ("manifest")).Attribute (XName.Get ("package")).Value;
				} catch (Exception ex) {
					throw new InvalidOperationException ("Failed to read 'package' attribute in 'manifest' element in AndroidManifest.xml from LibraryProject resource in {0}" + assemblyName, ex);
				}
			}
			return null;
		}
	}
}

