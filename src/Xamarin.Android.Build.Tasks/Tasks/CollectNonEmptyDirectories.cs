using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks {
	
	public class CollectNonEmptyDirectories : AndroidTask {
		public override string TaskPrefix => "CNE";

		List<ITaskItem> output = new List<ITaskItem> ();
		List<ITaskItem> libraryResourceFiles = new List<ITaskItem> ();

		[Required]
		public ITaskItem[] Directories { get; set; }

		[Required]
		public string LibraryProjectIntermediatePath { get; set; }

		[Required]
		public string StampDirectory { get; set; }

		[Output]
		public ITaskItem[] Output => output.ToArray ();

		[Output]
		public ITaskItem[] LibraryResourceFiles => libraryResourceFiles.ToArray ();

		public override bool RunTask ()
		{
			var libraryProjectDir = Path.GetFullPath (LibraryProjectIntermediatePath);
			foreach (var directory in Directories) {
				if (!Directory.Exists (directory.ItemSpec)) {
					Log.LogDebugMessage ($"Directory does not exist, skipping: {directory.ItemSpec}");
					continue;
				}
				string stampFile = directory.GetMetadata ("StampFile");
				if (string.IsNullOrEmpty (stampFile)) {
					if (Path.GetFullPath (directory.ItemSpec).StartsWith (libraryProjectDir)) {
						// If inside the `lp` directory
						stampFile = Path.GetFullPath (Path.Combine (directory.ItemSpec, "..", "..")) + ".stamp";
					} else {
						// Otherwise use a hashed stamp file
						stampFile = Path.Combine (StampDirectory, Files.HashString (directory.ItemSpec) + ".stamp");
					}
				}

				bool generateArchive = false;
				bool.TryParse (directory.GetMetadata (ResolveLibraryProjectImports.AndroidSkipResourceProcessing), out generateArchive);

				IEnumerable<string> files;
				string fileCache = Path.Combine (directory.ItemSpec, "..", "files.cache");
				DateTime lastwriteTime = File.Exists (stampFile) ? File.GetLastWriteTimeUtc (stampFile) : DateTime.MinValue;
				DateTime cacheLastWriteTime = File.Exists (fileCache) ? File.GetLastWriteTimeUtc (fileCache) : DateTime.MinValue;

				if (File.Exists (fileCache) && cacheLastWriteTime >= lastwriteTime) {
					Log.LogDebugMessage ($"Reading cached Library resources list from  {fileCache}");
					files = File.ReadAllLines (fileCache);
				} else {
					if (!File.Exists (fileCache))
						Log.LogDebugMessage ($"Cached Library resources list {fileCache} does not exist.");
					else
						Log.LogDebugMessage ($"Cached Library resources list {fileCache} is out of date.");
					if (generateArchive) {
						files = new string[1] { stampFile };
					} else {
						files = Directory.EnumerateFiles(directory.ItemSpec, "*.*", SearchOption.AllDirectories);
					}
				}

				if (files.Any ()) {
					if (!File.Exists (fileCache) || cacheLastWriteTime < lastwriteTime)
						File.WriteAllLines (fileCache, files, Encoding.UTF8);
					var taskItem = new TaskItem (directory.ItemSpec, new Dictionary<string, string> () {
						{"FileFound", files.First () },
					});
					directory.CopyMetadataTo (taskItem);

					if (string.IsNullOrEmpty (directory.GetMetadata ("StampFile"))) {
						taskItem.SetMetadata ("StampFile", stampFile);
					} else {
						Log.LogDebugMessage ($"%(StampFile) already set: {stampFile}");
					}
					output.Add (taskItem);
					foreach (var file in files) {
						var fileTaskItem = new TaskItem (file, new Dictionary<string, string> () {
							{ "ResourceDirectory", directory.ItemSpec },
							{ "StampFile", generateArchive ? stampFile : file },
							{ "Hash", stampFile },
							{ "_ArchiveDirectory", Path.Combine (directory.ItemSpec, "..", "flat" + Path.DirectorySeparatorChar) },
							{ "_FlatFile", generateArchive ?  $"{Path.GetFileNameWithoutExtension (stampFile)}.flata"  : Monodroid.AndroidResource.CalculateAapt2FlatArchiveFileName (file) },
						});
						libraryResourceFiles.Add (fileTaskItem);
					}
				}
			}
			return !Log.HasLoggedErrors;
		}
	}
}
