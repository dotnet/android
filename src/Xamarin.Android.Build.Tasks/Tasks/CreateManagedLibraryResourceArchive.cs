using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using System.IO.Compression;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class CreateManagedLibraryResourceArchive : Task
	{
		public bool IsApplication { get; set; }
		
		[Required]
		public string OutputDirectory { get; set; }

		public ITaskItem[] AndroidAssets { get; set; }

		public string MonoAndroidAssetsPrefix { get; set; }

		public ITaskItem[] AndroidJavaSources { get; set; }

		public ITaskItem[] AndroidJavaLibraries { get; set; }
		
		public ITaskItem[] AndroidResourcesInThisExactProject { get; set; }

		[Required]
		public string ResourceDirectory { get; set; }

		public CreateManagedLibraryResourceArchive ()
		{
		}
		
		public override bool Execute ()
		{
			if (IsApplication)
				return true;

			Log.LogDebugMessage ("CreateManagedLibraryResourceArchive Task");
			Log.LogDebugMessage ("  OutputDirectory: {0}", OutputDirectory);
			Log.LogDebugMessage ("  ResourceDirectory: {0}", ResourceDirectory);
			Log.LogDebugTaskItems ("  AndroidAssets:", AndroidAssets);
			Log.LogDebugTaskItems ("  AndroidJavaSources:", AndroidJavaSources);
			Log.LogDebugTaskItems ("  AndroidJavaLibraries:", AndroidJavaLibraries);

			var outDirInfo = new DirectoryInfo (OutputDirectory);
			
			// Copy files into _LibraryProjectImportsDirectoryName (library_project_imports) dir.
			if (!outDirInfo.Exists)
				outDirInfo.Create ();
			foreach (var sub in new string [] {"assets", "res", "java", "bin"}) {
				var subdirInfo = new DirectoryInfo (Path.Combine (outDirInfo.FullName, sub));
				if (!subdirInfo.Exists)
					subdirInfo.Create ();
			}

			var dir_sep = new char [] {Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar};
			if (AndroidAssets != null) {
				var dstsub = Path.Combine (outDirInfo.FullName, "assets");
				if (!Directory.Exists (dstsub))
					Directory.CreateDirectory (dstsub);
				foreach (var item in AndroidAssets) {
					var path = item.GetMetadata ("Link");
					path = !string.IsNullOrWhiteSpace (path) ? path : item.ItemSpec;
					var head = string.Join ("\\", path.Split (dir_sep).TakeWhile (s => !s.Equals (MonoAndroidAssetsPrefix, StringComparison.OrdinalIgnoreCase)));
					path = head.Length == path.Length ? path : path.Substring ((head.Length == 0 ? 0 : head.Length + Path.DirectorySeparatorChar) + MonoAndroidAssetsPrefix.Length).TrimStart (dir_sep);
					MonoAndroidHelper.CopyIfChanged (item.ItemSpec, Path.Combine (dstsub, path));
				}
			}
			// resources folders are converted to the structure that aapt accepts.
			bool hasInvalidName = false;
			foreach (var srcsub in Directory.GetDirectories (ResourceDirectory)) {
				var dstsub = Path.Combine (outDirInfo.FullName, "res", Path.GetFileName (srcsub));
				if (!Directory.Exists (dstsub))
					Directory.CreateDirectory (dstsub);
				foreach (var file in Directory.GetFiles (srcsub)) {
					var filename = Path.GetFileName (file);
					MonoAndroidHelper.CopyIfChanged (file, Path.Combine (dstsub, Path.GetFileName (file)));
				}
			}
			if (hasInvalidName)
				return false;
			if (AndroidJavaSources != null)
				foreach (var item in AndroidJavaSources)
					MonoAndroidHelper.CopyIfChanged (item.ItemSpec, Path.Combine (outDirInfo.FullName, item.ItemSpec));
			if (AndroidJavaLibraries != null)
				foreach (var item in AndroidJavaLibraries)
					MonoAndroidHelper.CopyIfChanged (item.ItemSpec, Path.Combine (outDirInfo.FullName, item.ItemSpec));
			
			var nameCaseMap = new StringWriter ();

			// add resource case mapping descriptor to the archive.
			if (AndroidResourcesInThisExactProject != null && AndroidResourcesInThisExactProject.Any ()) {
				Log.LogMessage ("writing __res_name_case_map.txt...");
				foreach (var res in AndroidResourcesInThisExactProject)
					nameCaseMap.WriteLine ("{0};{1}", res.GetMetadata ("LogicalName").Replace ('\\', '/'), Path.Combine (Path.GetFileName (Path.GetDirectoryName (res.ItemSpec)), Path.GetFileName (res.ItemSpec)).Replace ('\\', '/'));
				File.WriteAllText (Path.Combine (OutputDirectory, "__res_name_case_map.txt"), nameCaseMap.ToString ());
			}

			// Archive them in a zip.
			using (var stream = new MemoryStream ()) {
				using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, true, new System.Text.UTF8Encoding (false))) {
					zip.AddDirectory (OutputDirectory, outDirInfo.Name);
				}
				stream.Position = 0;
				string outpath = Path.Combine (outDirInfo.Parent.FullName, "__AndroidLibraryProjects__.zip");
				if (Files.ArchiveZip (outpath, f => {
					using (var fs = new FileStream (f, FileMode.CreateNew)) {
						stream.CopyTo (fs);
					}
				}))
					Log.LogDebugMessage ("Saving contents to " + outpath);
			}

			return true;
		}
	}
}
