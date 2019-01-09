using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Creates __AndroidLibraryProjects__.zip, $(AndroidApplication) should be False!
	/// </summary>
	public class CreateManagedLibraryResourceArchive : Task
	{
		[Required]
		public string OutputDirectory { get; set; }

		public ITaskItem[] AndroidAssets { get; set; }

		public string MonoAndroidAssetsPrefix { get; set; }

		public ITaskItem[] AndroidJavaSources { get; set; }

		public ITaskItem[] AndroidJavaLibraries { get; set; }
		
		public ITaskItem[] AndroidResourcesInThisExactProject { get; set; }

		public ITaskItem [] RemovedAndroidResourceFiles { get; set; }

		public string FlatArchivesDirectory { get; set; }

		[Required]
		public string ResourceDirectory { get; set; }

		public CreateManagedLibraryResourceArchive ()
		{
		}
		
		public override bool Execute ()
		{
			var outDirInfo = new DirectoryInfo (OutputDirectory);
			
			// Copy files into _LibraryProjectImportsDirectoryName (library_project_imports) dir.
			if (!outDirInfo.Exists)
				outDirInfo.Create ();
			foreach (var sub in new string [] {"assets", "res", "java", "bin"}) {
				var subdirInfo = new DirectoryInfo (Path.Combine (outDirInfo.FullName, sub));
				if (!subdirInfo.Exists)
					subdirInfo.Create ();
			}

			var compiledArchive = Path.Combine (FlatArchivesDirectory, "compiled.flata");
			if (File.Exists (compiledArchive)) {
				Log.LogDebugMessage ($"Coping: {compiledArchive} to {outDirInfo.FullName}");
				MonoAndroidHelper.CopyIfChanged (compiledArchive, Path.Combine (outDirInfo.FullName, "compiled.flata"));
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
			foreach (var srcsub in Directory.GetDirectories (ResourceDirectory)) {
				var dstsub = Path.Combine (outDirInfo.FullName, "res", Path.GetFileName (srcsub));
				if (!Directory.Exists (dstsub))
					Directory.CreateDirectory (dstsub);
				foreach (var file in Directory.GetFiles (srcsub)) {
					var filename = Path.GetFileName (file);
					MonoAndroidHelper.CopyIfChanged (file, Path.Combine (dstsub, Path.GetFileName (file)));
				}
			}
			if (RemovedAndroidResourceFiles != null) {
				foreach (var removedFile in RemovedAndroidResourceFiles) {
					var removed = Path.Combine (outDirInfo.FullName, removedFile.ItemSpec);
					if (File.Exists (removed)) {
						File.Delete (removed);
						Log.LogDebugMessage ($"Removed: {removed}");
					}

				}
			}
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
				File.WriteAllText (Path.Combine (outDirInfo.FullName, "__res_name_case_map.txt"), nameCaseMap.ToString ());
			}

			var outpath = Path.Combine (outDirInfo.Parent.FullName, "__AndroidLibraryProjects__.zip");
			var fileMode = File.Exists (outpath) ? FileMode.Open : FileMode.CreateNew;
			if (Files.ArchiveZipUpdate (outpath, f => {
				using (var zip = new ZipArchiveEx (f, fileMode)) {
					zip.AddDirectory (outDirInfo.FullName, "library_project_imports");
					if (RemovedAndroidResourceFiles != null) {
						foreach (var r in RemovedAndroidResourceFiles) {
							Log.LogDebugMessage ($"Removed {r.ItemSpec} from {outpath}");
							zip.RemoveFile ("library_project_imports", r.ItemSpec);
						}
					}
				}
			})) {
				Log.LogDebugMessage ("Saving contents to " + outpath);
			}

			return !Log.HasLoggedErrors ;
		}
	}
}
