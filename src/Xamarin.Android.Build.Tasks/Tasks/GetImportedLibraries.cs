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

namespace Xamarin.Android.Tasks
{
	public class GetImportedLibraries : Task
	{
		static readonly string [] IgnoredManifestDirectories = new [] {
			"bin",
			"manifest",
			"aapt",
		};

		[Required]
		public string TargetDirectory { get; set; }

		public string CacheFile { get; set;} 

		[Output]
		public ITaskItem [] Jars { get; set; }

		[Output]
		public ITaskItem [] NativeLibraries { get; set; }

		[Output]
		public ITaskItem [] ManifestDocuments { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("GetImportedLibraries Task");
			Log.LogDebugMessage ("  TargetDirectory: {0}", TargetDirectory);
			if (!Directory.Exists (TargetDirectory)) {
				Log.LogDebugMessage ("Target directory was not found");
				return true;
			}
			// there could be ./AndroidManifest.xml and bin/AndroidManifest.xml, which will be the same. So, ignore "bin" ones.
			var manifestDocuments = new List<ITaskItem> ();
			foreach (var f in Directory.EnumerateFiles (TargetDirectory, "AndroidManifest.xml", SearchOption.AllDirectories)) {
				var directory = Path.GetFileName (Path.GetDirectoryName (f));
				if (IgnoredManifestDirectories.Contains (directory))
					continue;
				manifestDocuments.Add (new TaskItem (f));
			}
			ManifestDocuments = manifestDocuments.ToArray ();
			NativeLibraries = Directory.GetFiles (TargetDirectory, "*.so", SearchOption.AllDirectories)
				.Where (p => MonoAndroidHelper.GetNativeLibraryAbi (p) != null)
				.Select (p => new TaskItem (p)).ToArray ();
			Jars = Directory.GetFiles (TargetDirectory, "*.jar", SearchOption.AllDirectories).Select (p => new TaskItem (p)).ToArray ();
			Log.LogDebugTaskItems ("  NativeLibraries: ", NativeLibraries);
			Log.LogDebugTaskItems ("  Jars: ", Jars);
			Log.LogDebugTaskItems ("  ManifestDocuments: ", ManifestDocuments);

			if (!string.IsNullOrEmpty (CacheFile)) {
				var document = new XDocument (
							new XDeclaration ("1.0", "UTF-8", null),
							new XElement ("Paths",
									new XElement ("ManifestDocuments", ManifestDocuments.Select(e => new XElement ("ManifestDocument", e.ItemSpec))),
									new XElement ("NativeLibraries", NativeLibraries.Select(e => new XElement ("NativeLibrary", e.ItemSpec))),
									new XElement ("Jars", Jars.Select(e => new XElement ("Jar", e.ItemSpec)))
						));
				document.SaveIfChanged (CacheFile);
			}

			return true;
		}
	}


}
